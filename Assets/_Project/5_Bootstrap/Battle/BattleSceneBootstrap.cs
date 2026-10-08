using System;
using System.IO;
using Card.Application.Match;
using Card.Application.Match.Agents;
using Card.Core;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Presentation.Battle;
using Card.Presentation.Battle.Feedback;
using Card.Presentation.Battle.Input;
using Card.Presentation.Battle.Targeting;
using UnityEngine;
using UnityEngine.UI;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 对战场景引导器（M6-T1/T3）：唯一挂在 Battle 场景里的脚本。
    /// Start：读 StreamingAssets 配置 → MatchFactory 开局 → 代码构建 UI → 装配
    /// 事件泵/反馈/日志/输入/指向/热座处理器 → 首帧同步。Update：泵事件后同步视图、回收浮动数字。
    /// 热座切换与胜负结算委托 <see cref="HotSeatHandler"/>。
    /// </summary>
    public sealed class BattleSceneBootstrap : MonoBehaviour
    {
        public const int LocalSeat = 0;
        public const int EnemySeat = 1;
        private const string ConfigFolder = "CardConfig";
        private const string LocalizationFile = "localization.csv";

        private MatchController? _controller;
        private MatchEventPump? _pump;
        private BattleUi? _ui;
        private BattleViewSynchronizer? _synchronizer;
        private ICommandSink? _sink;
        private PlayerInputController? _input;
        private TargetingController? _targeting;
        private FloatingTextPool? _textPool;
        private BattleFeedbackPlayer? _feedback;
        private UiTargetPicker? _picker;
        private TableFeedbackLocator? _locator;
        private HotSeatHandler? _hotSeat;
        private PendingIntent _pendingIntent;
        private int _pendingCardId;

        private enum PendingIntent
        {
            None = 0,
            PlayCard = 1,
            HeroPower = 2,
        }

        private void Start()
        {
            SceneCamera.Ensure();
            BattleUi ui = BattleUiFactory.Create();
            _ui = ui;

            // 全限定：本文件命名空间在 Card.* 下，裸 Application 会被 Card.Application 命名空间遮蔽。
            string configDir = Path.Combine(UnityEngine.Application.streamingAssetsPath, ConfigFolder);
            (CardDatabase? database, var errors) = BattleComposition.LoadDatabase(configDir);
            if (database == null)
            {
                ShowError(ui, "配置加载失败：\n" + string.Join("\n", errors)
                    + "\n\n请先执行菜单 Card/M6/部署运行时配置。");
                return;
            }

            MatchController controller = BattleComposition.StartMatch(
                database, BattleComposition.BuildDeckKeys(database));
            _controller = controller;

            ITextResolver texts = LoadTextResolver(configDir);
            _synchronizer = new BattleViewSynchronizer(ui, database, texts, LocalSeat, EnemySeat);
            _pump = new MatchEventPump(controller.Events);
            BindFeedback(ui, _pump);
            BindInput(ui, controller);

            _pump.EventAppended += ui.Log.Append;
            _synchronizer.Push(controller.View);

            _hotSeat = new HotSeatHandler(
                ui, controller, _pump, _synchronizer!,
                _input!, _targeting!, _picker!, _locator!, _feedback!, LocalSeat);
            _hotSeat.BindOverlays();
        }

        private void BindFeedback(BattleUi ui, MatchEventPump pump)
        {
            var locator = new TableFeedbackLocator(
                ui.LocalBoard, ui.EnemyBoard,
                LocalSeat, ui.LocalHeroAnchor, EnemySeat, ui.EnemyHeroAnchor);
            _locator = locator;
            var settings = new FeedbackSettings();
            _textPool = ui.FloatingTextPool;
            var feedback = new BattleFeedbackPlayer(
                locator, ui.FloatingTextPool, ui.FloatingTextLayer,
                ui.Banner, new NullAudioCuePlayer(), settings, LocalSeat);
            _feedback = feedback;
            feedback.Bind(pump);
        }

        private void BindInput(BattleUi ui, MatchController controller)
        {
            _input = gameObject.AddComponent<PlayerInputController>();
            _input._localPlayerId = LocalSeat;

            _targeting = gameObject.AddComponent<TargetingController>();
            _targeting._localPlayerId = LocalSeat;
            _targeting._arrow = ui.Arrow;

            // M7-T1：权威包一层 AgentMatchRunner，两个座位各注册一个人类 agent；
            // 输入组件共用，本地座位由激活回调路由，交棒屏仍为输入门控。
            var runner = new AgentMatchRunner(
                controller, controller.View,
                new IPlayerAgent[]
                {
                    new HumanPlayerAgent(LocalSeat, _input, _targeting),
                    new HumanPlayerAgent(EnemySeat, _input, _targeting),
                });

            _sink = new MatchControllerCommandSink(runner);
            _input.Initialize(_sink);
            _input.CommandRejected += OnInputRejected;

            var picker = new UiTargetPicker(
                ui.LocalBoard, ui.EnemyBoard,
                new (int, RectTransform)[]
                {
                    (LocalSeat, ui.LocalHeroAnchor),
                    (EnemySeat, ui.EnemyHeroAnchor),
                });
            _picker = picker;
            _targeting.Initialize(new UnityInputSource(), picker, _sink);
            _targeting.CommandRejected += error =>
                GameLog.Warn(LogChannel.Ui, "指向命令被权威侧拒绝：" + error);

            ui.LocalHand.CardClicked += OnHandCardClicked;
            ui.LocalBoard.CardClicked += OnBoardMinionClicked;
            ui.EndTurnButton.onClick.AddListener(OnEndTurnClicked);
            ui.HeroPowerButton.onClick.AddListener(OnHeroPowerClicked);

            runner.Start();
        }

        private void OnHandCardClicked(int instanceId)
        {
            _pendingIntent = PendingIntent.PlayCard;
            _pendingCardId = instanceId;
            _input!.NotifyHandCardClicked(instanceId);
        }

        private void OnBoardMinionClicked(int attackerInstanceId)
        {
            _targeting!.BeginAttackTargeting(attackerInstanceId, () => CardScreenPosition(attackerInstanceId));
        }

        private void OnHeroPowerClicked()
        {
            _pendingIntent = PendingIntent.HeroPower;
            _input!.NotifyHeroPowerClicked();
        }

        private void OnEndTurnClicked()
        {
            _sink!.Submit(new EndTurnCommand(_controller!.View.ActivePlayerId));
        }

        private void OnInputRejected(CommandError error)
        {
            if (error == CommandError.TargetRequired)
            {
                BeginTargetingForPendingIntent();
            }

            _pendingIntent = PendingIntent.None;
        }

        private void BeginTargetingForPendingIntent()
        {
            switch (_pendingIntent)
            {
                case PendingIntent.PlayCard:
                    int cardId = _pendingCardId;
                    _targeting!.BeginPlayCardTargeting(cardId, () => CardScreenPosition(cardId));
                    break;
                case PendingIntent.HeroPower:
                    _targeting!.BeginHeroPowerTargeting(HeroPowerScreenPosition);
                    break;
            }
        }

        private Vector2 CardScreenPosition(int instanceId)
        {
            if (_ui != null
                && (_ui.LocalHand.TryGetCardView(instanceId, out CardView? view)
                    || _ui.LocalBoard.TryGetCardView(instanceId, out view))
                && view != null)
            {
                return RectTransformUtility.WorldToScreenPoint(null, view.transform.position);
            }

            return Vector2.zero;
        }

        private Vector2 HeroPowerScreenPosition()
        {
            return _ui == null
                ? Vector2.zero
                : RectTransformUtility.WorldToScreenPoint(null, _ui.HeroPowerButton.transform.position);
        }

        private void Update()
        {
            if (_controller == null || _pump == null || _synchronizer == null)
                return;

            _hotSeat?.CheckGameEnd();
            if (_hotSeat is { IsBlocked: true })
                return;

            if (_hotSeat != null && _hotSeat.CheckTurnChange())
                return;

            if (_pump.Pump() > 0)
                _synchronizer.Push(_controller.View);

            _textPool?.ReclaimFinished();
        }

        /// <summary>
        /// 加载最小中文文本表（M6-T4）：文件缺失/损坏不阻断开局，
        /// 回退直通解析器（卡面显示 Key）并 Warn；M8 由正式本地化系统替换。
        /// </summary>
        private static ITextResolver LoadTextResolver(string configDir)
        {
            string path = Path.Combine(configDir, LocalizationFile);
            try
            {
                if (!File.Exists(path))
                {
                    GameLog.Warn(LogChannel.Boot, "本地化表不存在，卡面将显示 Key：" + path);
                    return KeyPassthroughTextResolver.Instance;
                }

                string csv = File.ReadAllText(path);
                return new CsvTextResolver(csv);
            }
            catch (Exception e)
            {
                GameLog.Warn(LogChannel.Boot, "本地化表加载失败，卡面将显示 Key：" + e.Message);
                return KeyPassthroughTextResolver.Instance;
            }
        }

        private static void ShowError(BattleUi ui, string message)
        {
            if (ui.ErrorPanel != null)
            {
                ui.ErrorPanel.gameObject.SetActive(true);
            }

            if (ui.ErrorText != null)
            {
                ui.ErrorText.text = message;
            }

            GameLog.Error(LogChannel.Boot, message);
        }
    }
}

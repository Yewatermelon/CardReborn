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
    ///
    /// M7-OBS-1：读 PlayerPrefs("GameMode") 区分 PVP/PVE。
    /// PVE 下 seat1 = GreedyAiAgent(stepMode:true)，由 <see cref="AiTurnRunner"/> 分帧驱动；
    /// 禁用 HotSeatHandler 的交棒屏，HumanPlayerAgent 固定 seat0。
    /// 输入回调与 ScreenPosition 辅助方法见 <see cref="BattleSceneBootstrap.Input.cs"/>（partial）。
    /// </summary>
    public sealed partial class BattleSceneBootstrap : MonoBehaviour
    {
        public const int LocalSeat = 0;
        public const int EnemySeat = 1;
        public const string GameModePrefKey = "GameMode";
        public const string PvpMode = "PVP";
        public const string PveMode = "PVE";
        public const string AiDifficultyPrefKey = "AiDifficulty";
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
        private AiTurnRunner? _aiRunner;
        private bool _isPve;

        private void Start()
        {
            _isPve = PlayerPrefs.GetString(GameModePrefKey, PvpMode) == PveMode;

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
            BindInput(ui, controller, database);

            _pump.EventAppended += ui.Log.Append;
            _synchronizer.Push(controller.View);

            // 胜负结算走 HotSeatHandler：PVP 全量绑交棒屏+胜负面板；PVE 只绑胜负与再来一局。
            _hotSeat = new HotSeatHandler(
                ui, controller, _pump, _synchronizer!,
                _input!, _targeting!, _picker!, _locator!, _feedback!, LocalSeat);
            if (_isPve)
            {
                _hotSeat.BindVictoryOverlay();
            }
            else
            {
                _hotSeat.BindOverlays();
            }
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

        private void BindInput(BattleUi ui, MatchController controller, CardDatabase database)
        {
            _input = gameObject.AddComponent<PlayerInputController>();
            _input._localPlayerId = LocalSeat;

            _targeting = gameObject.AddComponent<TargetingController>();
            _targeting._localPlayerId = LocalSeat;
            _targeting._arrow = ui.Arrow;

            IPlayerAgent[] agents;
            GreedyAiAgent? aiAgent = null;

            if (_isPve)
            {
                string diffKey = PlayerPrefs.GetString(AiDifficultyPrefKey, "Normal");
                aiAgent = new GreedyAiAgent(EnemySeat, database, stepMode: true,
                    difficulty: AiDifficultyProfile.FromKey(diffKey));
                agents = new IPlayerAgent[]
                {
                    new HumanPlayerAgent(LocalSeat, _input, _targeting),
                    aiAgent,
                };
            }
            else
            {
                agents = new IPlayerAgent[]
                {
                    new HumanPlayerAgent(LocalSeat, _input, _targeting),
                    new HumanPlayerAgent(EnemySeat, _input, _targeting),
                };
            }

            var runner = new AgentMatchRunner(controller, controller.View, agents);

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

            if (_isPve && aiAgent != null)
            {
                var runnerGo = gameObject.AddComponent<AiTurnRunner>();
                runnerGo.Bind(runner, aiAgent, controller, ui);
                _aiRunner = runnerGo;
            }

            runner.Start();
        }

        private void Update()
        {
            if (_controller == null || _pump == null || _synchronizer == null)
                return;

            _hotSeat?.CheckGameEnd();

            if (_isPve)
            {
                if (_controller.IsFinished) return;
            }
            else
            {
                if (_hotSeat is { IsBlocked: true })
                    return;
                if (_hotSeat != null && _hotSeat.CheckTurnChange())
                    return;
            }

            if (_pump.Pump() > 0)
                _synchronizer.Push(_controller.View);

            _textPool?.ReclaimFinished();
        }

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
                ui.ErrorPanel.gameObject.SetActive(true);
            if (ui.ErrorText != null)
                ui.ErrorText.text = message;
            GameLog.Error(LogChannel.Boot, message);
        }
    }
}

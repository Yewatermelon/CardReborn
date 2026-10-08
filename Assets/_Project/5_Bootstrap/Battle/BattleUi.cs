using Card.Presentation.Battle;
using Card.Presentation.Battle.Feedback;
using Card.Presentation.Battle.Log;
using Card.Presentation.Battle.Targeting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 战斗 UI 引用聚合（M6-T1）：由 <see cref="BattleUiFactory"/> 代码构建，
    /// 交给 <see cref="BattleSceneBootstrap"/> 装配。纯引用，不含逻辑。
    /// </summary>
    public sealed class BattleUi
    {
        public RectTransform CanvasRoot = null!;

        public HandView LocalHand = null!;
        public HandView EnemyHand = null!;
        public BoardView LocalBoard = null!;
        public BoardView EnemyBoard = null!;

        public HeroView LocalHero = null!;
        public HeroView EnemyHero = null!;
        public ManaView LocalMana = null!;
        public ManaView EnemyMana = null!;

        public RectTransform LocalHeroAnchor = null!;
        public RectTransform EnemyHeroAnchor = null!;

        public Button EndTurnButton = null!;
        public Button HeroPowerButton = null!;
        public TMP_Text EndTurnLabel = null!;
        public TMP_Text TurnLabel = null!;

        public BattleLogView Log = null!;
        public TurnBannerView Banner = null!;
        public RectTransform FloatingTextLayer = null!;
        public FloatingTextPool FloatingTextPool = null!;
        public TargetingArrowView Arrow = null!;
        public CanvasGroup? ErrorPanel;
        public TMP_Text? ErrorText;

        // M6-T3 热座切换
        public CanvasGroup? PassScreen;
        public TMP_Text? PassScreenLabel;
        public Button? PassScreenButton;

        // M6-T3 胜负结算
        public CanvasGroup? VictoryPanel;
        public TMP_Text? VictoryLabel;
        public Button? VictoryButton;
    }
}

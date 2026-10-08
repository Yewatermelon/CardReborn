using System.Collections.Generic;
using Card.Domain.Match;
using Card.Presentation.Battle;
using Card.Presentation.Battle.Targeting;
using UnityEngine;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// UI 目标拾取器（M6-T1）：实现 <see cref="ITargetPicker"/>，在屏幕点处按
    /// 矩形命中双战场随从与双英雄锚点，产出 <see cref="TargetRef"/>。
    /// 只识别不过滤——合法性由权威侧 RuleEngine 裁定（03 §5.6）。
    /// </summary>
    public sealed class UiTargetPicker : ITargetPicker
    {
        private readonly IReadOnlyList<BoardView> _boards;
        private List<HeroAnchor> _heroes;

        public UiTargetPicker(
            BoardView localBoard,
            BoardView enemyBoard,
            IReadOnlyList<(int Seat, RectTransform Anchor)> heroes)
        {
            _boards = new[] { localBoard, enemyBoard };
            var list = new List<HeroAnchor>(heroes.Count);
            for (int i = 0; i < heroes.Count; i++)
            {
                list.Add(new HeroAnchor(heroes[i].Seat, heroes[i].Anchor));
            }

            _heroes = list;
        }

        /// <summary>热座切换（M6-T3）：交换英雄锚点映射，确保点击命中正确座位。</summary>
        public void SwitchHeroAnchors()
        {
            if (_heroes.Count == 2)
            {
                var tmp = _heroes[0].Anchor;
                _heroes[0] = new HeroAnchor(_heroes[0].Seat, _heroes[1].Anchor);
                _heroes[1] = new HeroAnchor(_heroes[1].Seat, tmp);
            }
        }

        public bool TryPickTarget(Vector2 screenPosition, out TargetRef target)
        {
            for (int i = 0; i < _boards.Count; i++)
            {
                IReadOnlyList<CardView> children = _boards[i].Children;
                for (int j = 0; j < children.Count; j++)
                {
                    CardView card = children[j];
                    if (card.InstanceId.HasValue
                        && Contains((RectTransform)card.transform, screenPosition))
                    {
                        target = TargetRef.ForMinion(card.InstanceId.Value);
                        return true;
                    }
                }
            }

            for (int i = 0; i < _heroes.Count; i++)
            {
                if (Contains(_heroes[i].Anchor, screenPosition))
                {
                    target = TargetRef.ForHero(_heroes[i].Seat);
                    return true;
                }
            }

            target = default;
            return false;
        }

        private static bool Contains(RectTransform rect, Vector2 screenPosition)
        {
            // Overlay 画布：渲染相机传 null。
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null);
        }

        private readonly struct HeroAnchor
        {
            public readonly int Seat;
            public readonly RectTransform Anchor;

            public HeroAnchor(int seat, RectTransform anchor)
            {
                Seat = seat;
                Anchor = anchor;
            }
        }
    }
}

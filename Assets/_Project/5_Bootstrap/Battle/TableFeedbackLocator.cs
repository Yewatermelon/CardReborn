using System.Collections.Generic;
using Card.Presentation.Battle;
using Card.Presentation.Battle.Feedback;
using UnityEngine;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// 反馈定位器（M6-T1）：实现 <see cref="IFeedbackTargetLocator"/>，
    /// 按 InstanceId 在双战场视图中定位随从锚点，按座位定位英雄锚点。
    /// 纯表现查询，不读规则。战场容量 ≤ 7，线性扫描成本可忽略。
    /// </summary>
    public sealed class TableFeedbackLocator : IFeedbackTargetLocator
    {
        private readonly IReadOnlyList<BoardView> _boards;
        private readonly Dictionary<int, RectTransform> _heroAnchors;

        public TableFeedbackLocator(
            BoardView localBoard,
            BoardView enemyBoard,
            int localSeat,
            RectTransform localHeroAnchor,
            int enemySeat,
            RectTransform enemyHeroAnchor)
        {
            _boards = new[] { localBoard, enemyBoard };
            _heroAnchors = new Dictionary<int, RectTransform>
            {
                [localSeat] = localHeroAnchor,
                [enemySeat] = enemyHeroAnchor,
            };
        }

        public bool TryGetMinionAnchor(int instanceId, out Vector3 worldPosition)
        {
            if (TryGetMinionView(instanceId, out CardView? view))
            {
                worldPosition = view!.transform.position;
                return true;
            }

            worldPosition = default;
            return false;
        }

        public bool TryGetHeroAnchor(int seat, out Vector3 worldPosition)
        {
            if (_heroAnchors.TryGetValue(seat, out RectTransform? anchor))
            {
                worldPosition = anchor.position;
                return true;
            }

            worldPosition = default;
            return false;
        }

        public bool TryGetMinionView(int instanceId, out CardView? view)
        {
            for (int i = 0; i < _boards.Count; i++)
            {
                if (_boards[i].TryGetCardView(instanceId, out CardView? found))
                {
                    view = found;
                    return true;
                }
            }

            view = null;
            return false;
        }
    }
}

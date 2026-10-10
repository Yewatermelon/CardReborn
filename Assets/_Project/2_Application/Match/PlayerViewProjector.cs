using Card.Domain.Match;

namespace Card.Application.Match
{
    /// <summary>
    /// 视野裁剪框架（M11-T2）：按观察者视角裁剪权威状态。
    /// M11 实现 identity——直接返回原状态；M13 实现裁剪逻辑（对手手牌/牌库隐藏）。
    /// </summary>
    public static class PlayerViewProjector
    {
        /// <summary>
        /// 按观察者视角裁剪权威状态。M11 identity 实现：不裁，直接返回原对象。
        /// M13 将改为深拷贝 + 隐藏观察者不应看到的字段。
        /// </summary>
        public static MatchState Project(MatchState authoritative, int observerPlayerId)
        {
            // M11 identity：不做裁剪，直接返回原状态。
            // M13 改为深拷贝 + 隐藏 observer 视角外的手牌明细/牌库顺序等。
            return authoritative;
        }
    }
}

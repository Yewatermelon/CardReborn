namespace Card.Domain.Match
{
    /// <summary>
    /// 玩家/AI 意图命令的标记接口（M3-T4）。命令是不可变纯数据，
    /// 只描述“想做什么”、不含结算逻辑；来源座位由 <see cref="PlayerId"/> 自证。
    /// </summary>
    public interface IGameCommand
    {
        /// <summary>发起命令的玩家座位 Id（业务标识，不是集合下标）。</summary>
        int PlayerId { get; }
    }
}

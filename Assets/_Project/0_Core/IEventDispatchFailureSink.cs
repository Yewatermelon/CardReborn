namespace Card.Core
{
    /// <summary>
    /// 订阅者异常的上报出口。规则层不直接写日志，由外层（Infrastructure）
    /// 实现本接口并接入 <c>GameLog</c>（M1-T5），避免异常被静默吞掉。
    /// </summary>
    public interface IEventDispatchFailureSink
    {
        void OnHandlerFailed(EventDispatchFailure failure);
    }
}

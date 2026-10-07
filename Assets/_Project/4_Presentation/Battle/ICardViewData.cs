using Card.Domain.Config;

namespace Card.Presentation.Battle
{
    /// <summary>
    /// CardView 的只读数据契约（FR-8.1）。纯值类型，不含领域对象引用，
    /// PVP 时可由服务器下发的 JSON 直接反序列化。
    /// </summary>
    public interface ICardViewData
    {
        string Name { get; }

        string Description { get; }

        int Cost { get; }

        int Attack { get; }

        int Health { get; }

        /// <summary>美术资源键；仅存储，加载由 <c>IArtProvider</c> 负责（M6 接入）。</summary>
        string ArtKey { get; }

        CardType Type { get; }

        /// <summary>局内实例 Id（M5-T6）：配置态为 null；反馈定位与高亮按它匹配视图。</summary>
        int? InstanceId { get; }
    }
}

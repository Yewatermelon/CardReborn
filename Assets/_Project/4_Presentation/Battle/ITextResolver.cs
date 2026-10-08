namespace Card.Presentation.Battle
{
    /// <summary>
    /// 文本解析契约（M6-T4；M8 前的最小本地化接缝）：把配置中的文本 Key
    /// （如 <c>CARD_001_NAME</c>）解析为可显示文本。
    /// 实现必须保证：key 为 null/空或查无译文时不抛异常（回退 key 本身）。
    /// </summary>
    public interface ITextResolver
    {
        string Resolve(string key);
    }
}

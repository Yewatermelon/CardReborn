namespace Card.Presentation.Battle
{
    /// <summary>
    /// 直通解析器（M6-T4）：不翻译，原样返回 key。
    /// 用于测试与本地化表缺失时的安全回退；生产路径由 Bootstrap 的 CSV 解析器替换。
    /// </summary>
    public sealed class KeyPassthroughTextResolver : ITextResolver
    {
        /// <summary>共享实例（无状态，可安全复用）。</summary>
        public static readonly KeyPassthroughTextResolver Instance = new KeyPassthroughTextResolver();

        public string Resolve(string key)
        {
            return key ?? string.Empty;
        }
    }
}

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// 让 C# 9 的 init 访问器能在 Unity（netstandard2.1）下编译：
    /// 该类型是 .NET 5+ 才提供的编译器标记类型，Unity 的 BCL 里没有。
    ///
    /// 约定：
    /// 1. 只由本程序集提供（其他程序集引用 Card.Core 即可用），不要重复定义；
    /// 2. 用 NET5_0_OR_GREATER 条件编译——现代 .NET（Tools/Coverage 的 net9.0）已有该类型，
    ///    若不加条件会与之冲突（CS0433）。
    /// </summary>
    public static class IsExternalInit
    {
    }
}
#endif

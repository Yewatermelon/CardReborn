namespace Card.Core
{
    /// <summary>JSON 取值种类（极简模型只覆盖配置需要的子集）。</summary>
    public enum JsonKind
    {
        Null = 0,
        Bool = 1,
        Number = 2,
        String = 3,
        Array = 4,
        Object = 5
    }
}

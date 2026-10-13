namespace Card.Network
{
    /// <summary>
    /// Soak 回归参数（M14-T7）：门禁快测 3 局，完整 30 局走独立入口。
    /// </summary>
    public sealed class NetworkSoakOptions
    {
        /// <summary>局数（门禁快测 3，完整 30）。</summary>
        public int MatchCount = 3;

        /// <summary>基础随机种子；第 i 局 seed = BaseSeed + i。</summary>
        public int BaseSeed = 424242;

        /// <summary>单局命令步数上限（防死循环，超限记失败不中断批量）。</summary>
        public int MaxStepsPerMatch = 5000;

        /// <summary>单次等待的 tick 泵上限（防卡死）。</summary>
        public int MaxPumpsPerWait = 2000;

        /// <summary>座位 0（房主）英雄配置键。</summary>
        public string HostHeroKey = "HERO_MAGE";

        /// <summary>座位 1（对手）英雄配置键。</summary>
        public string PeerHeroKey = "HERO_WARRIOR";
    }
}

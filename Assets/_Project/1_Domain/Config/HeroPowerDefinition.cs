using System;
using System.Collections.Generic;

namespace Card.Domain.Config
{
    /// <summary>英雄技能配置（Docs/01 第 3.6 / 7.2 节）：默认 2 费造成 1 点伤害，每回合 1 次。</summary>
    public sealed class HeroPowerDefinition
    {
        public int Id { get; init; }

        public string Key { get; init; } = string.Empty;

        public int Cost { get; init; } = 2;

        public TargetRule TargetRule { get; init; } = TargetRule.Any;

        /// <summary>效果描述串；M4 解析为效果组件。</summary>
        public IReadOnlyList<string> Effects { get; init; } = Array.Empty<string>();
    }
}

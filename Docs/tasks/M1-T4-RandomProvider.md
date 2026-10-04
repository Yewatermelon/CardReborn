# 任务卡 · M1-T4 `IRandomProvider` ★

## 1. 基本信息

| 项 | 内容 |
| --- | --- |
| 任务 ID | M1-T4 ★（★ = 必须通过"无 Unity 依赖"检查） |
| 所属里程碑 | M1 核心基础层（Core） |
| 上游需求 | FR-4.6（随机可复现：支持注入种子，同种子结果一致）、FR-13.3（环境依赖可注入）、FR-5.14、NFR-4、NFR-6、NFR-11 |
| 规则依据 | [03 第 5.9.2 节](../03-开发规范文档.md)（环境依赖注入：接口示例、洗牌算法固定为 Fisher–Yates 并写明）、[03 第 11.3 节](../03-开发规范文档.md)（所有随机走 `IRandomProvider`；抽卡与洗牌必须可注入种子）、[05 第 14 节](../05-联网对战_状态同步_设计文档.md)（种子可注入降级为"可测试性 + 服务器随机源"要求）、[04 §12 案例 3](../04-代码复盘Review规范.md)（洗牌对象必须就是被索引的那个集合） |
| 预估 | 0.5 人日 / 1 会话 |
| 依赖任务 | M1-T1（复用 `Guard.NotNull`） |

## 2. 目标（一句话）

> 在 `Card.Core` 提供可注入种子的随机抽象与纯 BCL 实现：同一 seed 产生同一序列、同一洗牌结果，使对局与抽卡在测试和复盘时可完全复现，并让规则层永远不必碰 `UnityEngine.Random`。

## 3. 范围（做什么）

- `IRandomProvider`（接口，与 03 §5.9.2 的签名一致）：`int NextInt(int minInclusive, int maxExclusive)`、`void Shuffle<T>(IList<T> list)`。
- `SeededRandomProvider`（实现，纯 BCL）：
  - `Seed` 只读属性（便于日志与复盘记录"这一局的种子"）；
  - 内部使用 **xorshift32** 整数 PRNG：只有位移与异或，**不依赖 `System.Random` / Unity**，从而跨运行时与跨平台稳定复现；
  - `NextInt` 用**拒绝采样**消除取模偏置，返回 `[minInclusive, maxExclusive)`；
  - `Shuffle` 用 **Fisher–Yates（由后向前交换）** 原地洗牌，算法写进注释（03 §5.9.2 要求）；
  - 处理 xorshift 的"全零状态"陷阱：seed 扩散后若为 0 则替换为非零常量，保证 `seed = 0` 也有有效序列。
- EditMode 测试覆盖：序列可复现、范围/越界、空区间、极值区间、覆盖性、洗牌不变式（元素不丢不重）、原地洗牌、同种子同顺序、"洗牌即抽取顺序"的复现性（案例 3 的教训）。

## 4. 明确不做（防止范围蔓延）

- **不做** `NextDouble` / `NextBool` / 加权抽取：03 §5.9.2 的接口只定义了两个成员；加权抽取（抽卡稀有度权重，FR-4.2）可由 `NextInt(0, 100)` 组合，属 M2/M8 的配置与服务层。
- **不做**加密安全随机：阶段二已明示无反正作弊能力（05 第 15 节），不需要 `RandomNumberGenerator`。
- **不做** Unity 版实现（包装 `UnityEngine.Random`）：按 03 §5.9.2，Unity 适配实现放 `Card.Infrastructure`；本任务只交付可被服务端复用的纯 BCL 实现（`Card.Server` 复用 Core 源码）。
- **不做**线程安全：单线程 tick / 主线程约定。
- **不做**"双端逐位一致"的额外承诺：05 第 14 节已撤销帧同步时代的该约束；本任务只保证"同一实现 + 同一种子 ⇒ 同一序列"。
- **不做** `check.ps1` 的随机源扫描规则：那是 **M1-T9**（本任务先用独立扫描 + 测试证明 Core 内没有 `UnityEngine.Random`）。

## 5. 接口约定

```csharp
namespace Card.Core
{
    /// <summary>可注入种子的随机源；规则层只依赖本接口。</summary>
    public interface IRandomProvider
    {
        int NextInt(int minInclusive, int maxExclusive);
        void Shuffle<T>(IList<T> list);
    }

    /// <summary>纯 BCL 的确定性实现（xorshift32 + Fisher–Yates）。</summary>
    public sealed class SeededRandomProvider : IRandomProvider
    {
        public SeededRandomProvider(int seed);
        public int Seed { get; }
        public int NextInt(int minInclusive, int maxExclusive);
        public void Shuffle<T>(IList<T> list);
    }
}
```

## 6. 验收标准（必须可测）

| 编号 | 场景 | 期望结果 |
| --- | --- | --- |
| AC-1 | 同一种子两个实例 | 产生的整数序列完全一致 |
| AC-2 | 不同种子 | 序列不同（20 次抽样） |
| AC-3 | `NextInt(min, max)` | 结果始终落在 `[min, max)` |
| AC-4 | 区间只有 1 个值（`max = min + 1`） | 恒返回 `min` |
| AC-5 | 空区间 / 反向区间 | 抛 `ArgumentException`（ParamName=`minInclusive`）；不返回垃圾值 |
| AC-6 | 极值区间（含 `int.MinValue` / `int.MaxValue`） | 不溢出、不抛异常、结果仍在界内 |
| AC-7 | 小范围多次抽样 | 区间内每个值都出现过（覆盖性） |
| AC-8 | `seed = 0` | 仍产生有变化的序列（xorshift 全零状态陷阱的回归测试） |
| AC-9 | `Shuffle(null)` | 抛 `ArgumentNullException`（ParamName=`list`） |
| AC-10 | 空列表 / 单元素列表 | 不改内容、不抛；且**不消耗随机数**（后续序列与未洗牌时一致） |
| AC-11 | 洗牌结果 | 元素不丢、不重、不新增（多重集不变） |
| AC-12 | 洗牌原地生效 | 传入的实例本身被重排（不是对副本排序） |
| AC-13 | 同种子洗同一牌库 | 两次洗牌得到完全相同的顺序 |
| AC-14 | "洗牌即抽取顺序" | 同种子两次运行，按索引 0..n-1 读取的抽取顺序一致（[04 案例 3](../04-代码复盘Review规范.md) 的回归） |
| AC-15 | 洗牌确实消耗随机数 | 洗牌后的下一个 `NextInt` 与未洗牌的同种子实例不同 |
| AC-16 | 两个实例互不干扰 | 交替抽样不会互相影响（无静态状态） |
| AC-17 | 内核解耦 | `0_Core` 内不出现 `UnityEngine.Random` / `using UnityEngine` / `DateTime.Now`（独立扫描 + `check.ps1`） |
| AC-18 | `Seed` 属性 | 返回构造时传入的种子（便于复盘记录） |

## 7. 测试要求

- 测试类型：EditMode（`Card.Tests.EditMode.Core`）
- 最少用例数：20
- 必须覆盖的边界：空区间、反向区间、单值区间、极值区间、seed=0、空列表、单元素列表、重复元素、边界外索引、同种子复现、实例独立

## 8. 涉及文档与配置

- 需更新的文档：`Docs/PROGRESS.md`、本任务卡回填
- 需更新的配置表：无
- 是否影响既有模块：无（新增文件）

## 9. 完成定义（DoD 勾选）

- [x] 满足全部 AC 且附证据（见第 10.1 节）
- [x] 测试通过且覆盖边界（EditMode 127 passed / 0 failed，其中新增 23 例：`RandomProviderTests` 12 + `ShuffleTests` 11）
- [x] 编译 0 error / 0 warning（批处理日志 `warning CS` 命中 0 处）
- [x] 通过 `Docs/03` 铁律与禁止清单自查（见第 10.2 节）
- [x] 通过 [04-代码复盘Review规范](../04-代码复盘Review规范.md) 评审，无未关闭 P0/P1（见第 10.6 节）
- [x] 涉及文档已同步更新（PROGRESS）

---

## 10. 自检与评审结论（2026-10-04）

### 10.1 需求对齐（逐条 AC）

| AC | 是否满足 | 证据（测试名） |
| --- | --- | --- |
| AC-1 同种子同序列 | 已满足 | `NextInt_WhenSameSeed_ProducesSameSequence` |
| AC-2 不同种子序列不同 | 已满足 | `NextInt_WhenDifferentSeeds_ProduceDifferentSequences` |
| AC-3 结果落在 `[min, max)` | 已满足 | `NextInt_WhenCalledManyTimes_StaysWithinRange`（1000 次） |
| AC-4 单值区间 | 已满足 | `NextInt_WhenRangeHasSingleValue_AlwaysReturnsThatValue` |
| AC-5 空/反向区间被拒 | 已满足 | `NextInt_WhenRangeIsEmptyOrReversed_ThrowsArgumentException(5,5)/(10,5)/(int.MaxValue,int.MinValue)` |
| AC-6 极值区间不溢出 | 已满足 | `NextInt_WhenRangeTouchesExtremes_StaysWithinBounds` |
| AC-7 小范围覆盖性 | 已满足 | `NextInt_WhenSmallRange_CoversEveryValue` |
| AC-8 `seed = 0` 仍有效 | 已满足 | `NextInt_WhenSeedIsZero_ProducesVariedValues` |
| AC-9 `Shuffle(null)` | 已满足 | `Shuffle_WhenListIsNull_ThrowsArgumentNullException` |
| AC-10 空/单元素不消耗随机数 | 已满足 | `Shuffle_WhenListIsEmptyOrSingle_DoesNotConsumeRandomness`、`Shuffle_WhenSingleElement_LeavesListUnchanged` |
| AC-11 元素不丢不重 | 已满足 | `Shuffle_WhenCalled_KeepsEveryElementExactlyOnce`、`Shuffle_WhenDeckHasRepeatedCards_KeepsEachCount` |
| AC-12 原地重排 | 已满足 | `Shuffle_WhenCalled_ReordersThePassedInstanceInPlace` |
| AC-13 同种子同顺序 | 已满足 | `Shuffle_WhenSameSeed_ProducesSameOrder` |
| AC-14 "洗牌即抽取顺序"可复现 | 已满足 | `Shuffle_ThenReadInIndexOrder_IsReproducibleDrawOrder`（[04 案例 3](../04-代码复盘Review规范.md) 回归） |
| AC-15 洗牌消耗随机数 | 已满足 | `Shuffle_WhenCalled_ConsumesRandomnessFromProvider` |
| AC-16 实例互不干扰 | 已满足 | `Providers_WhenSameSeedUsedInterleaved_DoNotAffectEachOther` |
| AC-17 内核解耦 | 已满足 | `check.ps1` R1 PASS + 独立扫描（同门禁规则）命中 0 |
| AC-18 `Seed` 属性 | 已满足 | `Seed_WhenCreated_ExposesConstructorSeed`（含负数与 0） |

### 10.2 铁律扫描（命中 / 未命中）

| 铁律 | 结论 | 说明 |
| --- | --- | --- |
| 2 依赖只能向下 / 3 内核只依赖 BCL | 未命中 | 仅用 `System`、`System.Collections.Generic`；未使用 `System.Random`（见 10.3 边界表） |
| 4 表现层只读 | 不涉及 | 无表现层改动；洗牌只操作传入集合 |
| 6 数值来自配置 | 不涉及 | 本任务无玩法数值 |
| 8 禁隐式全局访问 | 未命中 | 无静态可变状态；实例隔离有测试（AC-16） |
| 9 新功能带测试 | 未命中 | 23 个新用例，先红（CS0246）后绿 |
| 10 行数 / 复杂度 / 0 warning | 未命中 | `SeededRandomProvider.cs` 91 行、`IRandomProvider.cs` 19 行；方法均 ≤ 50 行；0 warning |
| 11 内核与 Unity 解耦 | 未命中 | 无 Unity API；环境依赖走 `IRandomProvider` 注入（03 §5.9.2） |
| 禁止清单（`#region`、单行多语句、省略花括号、拼音命名） | 未命中 | 逐项确认 |
| 其余（1/5/7/12/13） | 不涉及 | 无玩法继承、命令入口、编辑器 API、网络 |

### 10.3 边界场景推演（≥5）

| 场景 | 期望行为 | 实际行为 | 对应测试 |
| --- | --- | --- | --- |
| 空区间 / 反向区间 | 抛 `ArgumentException`，不返回垃圾值 | 一致 | AC-5 |
| 极值区间（跨 `int.MinValue`..`int.MaxValue`） | 不溢出、仍在界内 | 一致 | AC-6（内部用 `long` 计算 range，拒绝采样避免取模偏置） |
| `seed = 0` | 仍产生有变化的序列 | 一致 | AC-8（xorshift 全零状态用非零常量兜底） |
| 相邻种子 | 起始状态不应高度相关 | 一致 | 种子先做乘法散列（`×2654435761`）再兜底；`NextInt_WhenDifferentSeeds_ProduceDifferentSequences` |
| 空列表 / 单元素列表 | 不抛、不消耗随机数 | 一致 | AC-10 |
| 含重复元素的牌库 | 各元素个数保持不变 | 一致 | AC-11、`Shuffle_WhenDeckHasRepeatedCards_KeepsEachCount` |
| 2 元素牌库 | 两种顺序都应出现（覆盖交换分支） | 一致 | `Shuffle_WhenTwoElements_ProducesBothOrdersAcrossSeeds` |
| 同一 provider 连续洗两次 | 两次都是合法排列且顺序不同 | 一致 | `Shuffle_WhenCalledTwiceWithSameProvider_ProducesDifferentValidPermutations` |

### 10.4 测试证据

运行命令：`Unity.exe -batchmode -nographics -projectPath <临时副本> -runTests -testPlatform EditMode`

| 阶段 | 结果 |
| --- | --- |
| 红（只同步测试） | `CS0246 'IRandomProvider' could not be found`，无结果 XML |
| 绿（全量同步） | `result=Passed total=127 passed=127 failed=0`；`warning CS` 0 处 |
| 最终复跑（注释微调后） | `result=Passed total=127 passed=127 failed=0`；`warning CS` 0 处 |
| 本次新增 | `RandomProviderTests` 12 + `ShuffleTests` 11 = 23 |

### 10.5 影响面

- 新增文件：`0_Core/IRandomProvider.cs`、`0_Core/SeededRandomProvider.cs` 与 2 个测试文件。
- 改动既有模块：**无**。
- 需要同步的文档 / 配置：无配置表；`Docs/PROGRESS.md` 已更新。
- **发现（转 M1-T9）**：`Tools/check.ps1` 的 R1 是纯文本扫描，**注释里出现 API 名字也会被判违规**（本次 `IRandomProvider.cs` 文档注释提到 Unity 随机 API 即触发 2 条 R1）。M1-T9 做扫描规则时应先剥离注释再匹配，否则"教人别用的注释"会反过来卡住自己。本次通过改写注释措辞规避。
- 回归风险：低，无调用方；M2/M8 的抽卡与 M4 的随机效果将注入本接口。

### 10.6 评审结论

**通过**（AI 自审）：无 P0/P1；门禁全绿（编译 0 error / 0 warning、127 用例全过、`check.ps1` PASS、内核零 Unity 依赖）；文档已同步。

| 编号 | 级别 | 描述 | 处理 |
| --- | --- | --- | --- |
| M1-R3 | P3 | `check.ps1` R1 扫描未剥离注释，导致文档注释中的 API 名称产生误报 | 并入 M1-T9（内核解耦检查规则）一并修复 |

遗留项仍为 M1-R1（启用 `<Nullable>`，P2）与 M1-R2（覆盖率统计，M1 门禁要求 Core ≥ 90%），按约定 M1 收尾统一处理。

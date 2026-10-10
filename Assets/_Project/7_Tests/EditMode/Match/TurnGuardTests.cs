using System;
using Card.Application.Match.Agents;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Match
{
    /// <summary>
    /// M7-T3 TurnGuard 单元测试（FR-6.4）：步数上限 / IClock 时间上限 / 无进展检测三层独立兜底，
    /// 构造校验、触发顺序（步数 → 时间 → 无进展）、幂等与激活重置。
    /// </summary>
    [TestFixture]
    public class TurnGuardTests
    {
        // ---------- AC-2 构造校验 ----------

        [TestCase(0, 0, 1)]
        [TestCase(10, -1, 1)]
        [TestCase(10, 0, 0)]
        public void Constructor_InvalidOptions_Throws(int maxSteps, int maxTicks, int noProgressLimit)
        {
            // M3 复盘教训：NUnit Assert.Throws<T> 为精确类型匹配；值域非法实现抛
            // ArgumentOutOfRangeException（ArgumentException 子类），故用 try/catch 断言基类。
            try
            {
                _ = new TurnGuard(new TurnGuardOptions
                {
                    MaxSteps = maxSteps,
                    MaxTicks = maxTicks,
                    NoProgressLimit = noProgressLimit,
                });
                Assert.Fail("非法选项应抛 ArgumentException（或其子类）。");
            }
            catch (ArgumentException)
            {
                // 断言到达：抛出的是 ArgumentException 系异常。
            }
        }

        [Test]
        public void Constructor_TimeLimitWithoutClock_Throws()
        {
            // 防呆：要启用时间上限就必须提供时钟，禁止静默不生效。
            Assert.Throws<ArgumentException>(() => new TurnGuard(
                new TurnGuardOptions { MaxTicks = 10 }, clock: null));
        }

        // ---------- AC-3 步数上限 ----------

        [Test]
        public void Steps_DefaultLimit500_TriggersAt500thRegistration()
        {
            TurnGuard guard = new TurnGuard();

            for (int i = 0; i < 499; i++)
            {
                guard.RegisterStep("sig-" + i);
            }

            Assert.That(guard.IsExhausted, Is.False, "499 步不应触发默认 500 上限。");

            guard.RegisterStep("sig-499");

            Assert.That(guard.IsExhausted, Is.True);
            Assert.That(guard.ExhaustReason, Is.EqualTo(TurnGuardOptions.ReasonStepLimit));
            Assert.That(guard.Steps, Is.EqualTo(500));
        }

        [Test]
        public void Steps_CustomLimit_Triggers()
        {
            TurnGuard guard = new TurnGuard(new TurnGuardOptions { MaxSteps = 3 });

            guard.RegisterStep("a");
            guard.RegisterStep("b");
            Assert.That(guard.IsExhausted, Is.False);

            guard.RegisterStep("c");

            Assert.That(guard.IsExhausted, Is.True);
            Assert.That(guard.ExhaustReason, Is.EqualTo(TurnGuardOptions.ReasonStepLimit));
            Assert.That(guard.Steps, Is.EqualTo(3));
        }

        [Test]
        public void Exhausted_RegisterStep_IsIdempotent()
        {
            TurnGuard guard = new TurnGuard(new TurnGuardOptions { MaxSteps = 2 });
            guard.RegisterStep("a");
            guard.RegisterStep("a");

            guard.RegisterStep("a");
            guard.RegisterStep("a");

            Assert.That(guard.Steps, Is.EqualTo(2), "已触发后注册不再累计。");
            Assert.That(guard.ExhaustReason, Is.EqualTo(TurnGuardOptions.ReasonStepLimit));
        }

        // ---------- AC-4 时间上限 ----------

        [Test]
        public void TimeLimit_Zero_NeverTriggers()
        {
            ManualClock clock = new ManualClock();
            TurnGuard guard = new TurnGuard(new TurnGuardOptions { MaxTicks = 0 }, clock);

            for (int i = 0; i < 20; i++)
            {
                clock.Advance(100);
                guard.RegisterStep("sig-" + i);
            }

            Assert.That(guard.IsExhausted, Is.False, "MaxTicks=0 表示不启用时间上限。");
        }

        [Test]
        public void TimeLimit_NotTriggered_WithinBudget()
        {
            ManualClock clock = new ManualClock();
            TurnGuard guard = new TurnGuard(new TurnGuardOptions { MaxTicks = 10 }, clock);

            guard.RegisterStep("a");
            clock.Advance(9);
            guard.RegisterStep("b");

            Assert.That(guard.IsExhausted, Is.False, "tick 差 9 < 预算 10，不应触发。");
        }

        [Test]
        public void TimeLimit_Triggers_WhenBudgetExhausted()
        {
            ManualClock clock = new ManualClock();
            TurnGuard guard = new TurnGuard(new TurnGuardOptions { MaxTicks = 10 }, clock);

            guard.RegisterStep("a");
            clock.Advance(10);
            guard.RegisterStep("b");

            Assert.That(guard.IsExhausted, Is.True, "tick 差 10 >= 预算 10，应触发。");
            Assert.That(guard.ExhaustReason, Is.EqualTo(TurnGuardOptions.ReasonTimeLimit));
        }

        // ---------- AC-5 无进展检测 ----------

        [Test]
        public void NoProgress_Triggers_AtLimit()
        {
            TurnGuard guard = new TurnGuard(new TurnGuardOptions { MaxSteps = 100, NoProgressLimit = 1 });

            guard.RegisterStep("same");
            Assert.That(guard.IsExhausted, Is.False, "首步无上一步可比，永不判无进展。");

            guard.RegisterStep("same");

            Assert.That(guard.IsExhausted, Is.True);
            Assert.That(guard.ExhaustReason, Is.EqualTo(TurnGuardOptions.ReasonNoProgress));
            Assert.That(guard.NoProgressSteps, Is.EqualTo(1));
        }

        [Test]
        public void NoProgress_FirstStep_NeverCompares()
        {
            TurnGuard guard = new TurnGuard(new TurnGuardOptions { NoProgressLimit = 1 });

            guard.RegisterStep("a");
            guard.RegisterStep("b");

            Assert.That(guard.IsExhausted, Is.False, "签名变化应重置无进展计数。");
        }

        [Test]
        public void NoProgress_ResetOnChange()
        {
            TurnGuard guard = new TurnGuard(new TurnGuardOptions { NoProgressLimit = 2 });

            guard.RegisterStep("a");
            guard.RegisterStep("a");
            Assert.That(guard.NoProgressSteps, Is.EqualTo(1));

            guard.RegisterStep("b");
            Assert.That(guard.NoProgressSteps, Is.EqualTo(0), "签名变化应归零。");

            guard.RegisterStep("b");
            Assert.That(guard.NoProgressSteps, Is.EqualTo(1));
            Assert.That(guard.IsExhausted, Is.False);

            guard.RegisterStep("b");
            Assert.That(guard.IsExhausted, Is.True);
            Assert.That(guard.ExhaustReason, Is.EqualTo(TurnGuardOptions.ReasonNoProgress));
        }

        // ---------- 触发顺序：步数 → 时间 → 无进展 ----------

        [Test]
        public void StepLimit_TakesPrecedence_OverNoProgress()
        {
            // 同一步既满足步数上限又满足无进展：Reason 必须记先判定的步数上限。
            TurnGuard guard = new TurnGuard(new TurnGuardOptions { MaxSteps = 2, NoProgressLimit = 1 });

            guard.RegisterStep("same");
            guard.RegisterStep("same");

            Assert.That(guard.IsExhausted, Is.True);
            Assert.That(guard.ExhaustReason, Is.EqualTo(TurnGuardOptions.ReasonStepLimit));
        }

        // ---------- AC-6 激活重置 ----------

        [Test]
        public void ActivationStart_ResetsAllState()
        {
            TurnGuard guard = new TurnGuard(new TurnGuardOptions { MaxSteps = 1 });
            guard.OnActivationStarted();
            guard.RegisterStep("a");
            Assert.That(guard.IsExhausted, Is.True);

            guard.OnActivationStarted();

            Assert.That(guard.IsExhausted, Is.False);
            Assert.That(guard.ExhaustReason, Is.Null);
            Assert.That(guard.Steps, Is.EqualTo(0));
            Assert.That(guard.NoProgressSteps, Is.EqualTo(0));
        }

        [Test]
        public void ActivationStart_RerecordsStartTick()
        {
            ManualClock clock = new ManualClock();
            TurnGuard guard = new TurnGuard(new TurnGuardOptions { MaxTicks = 5 }, clock);

            guard.OnActivationStarted();
            clock.Advance(100);
            guard.RegisterStep("a");
            Assert.That(guard.IsExhausted, Is.True, "激活 1：时钟已越过预算，应触发。");

            guard.OnActivationStarted();
            guard.RegisterStep("b");
            Assert.That(guard.IsExhausted, Is.False, "激活 2：起始 tick 重记后预算重新计算。");
        }
    }
}

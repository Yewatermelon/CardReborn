using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Card.Application.Match;
using Card.Domain.Config;
using Card.Domain.Match;
using Card.Presentation.Battle;
using Card.Tests.EditMode.Match;
using NUnit.Framework;

namespace Card.Tests.EditMode.Presentation
{
    /// <summary>
    /// M5-T8 ★ 门禁：表现层程序集表面不得出现可变对局状态/权威控制器类型；
    /// 反序列化（PVP 下发）状态与原状态经同一渲染管线产出逐字段相等的视图数据。
    /// </summary>
    [TestFixture]
    public sealed class ReadOnlySurfaceGateTests
    {
        private static readonly Type[] ForbiddenTypes =
        {
            typeof(MatchState), typeof(PlayerState), typeof(HeroState),
            typeof(ManaPool), typeof(Zone), typeof(CardInstance),
            typeof(KeywordSet), typeof(StatusSet),
            typeof(MatchController), typeof(RuleEngine),
        };

        [Test]
        public void PresentationSurface_ExposesNoMutableStateOrAuthority()
        {
            Assembly assembly = typeof(CardView).Assembly;
            var violations = new List<string>();

            foreach (Type type in assembly.GetTypes())
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                foreach (MethodInfo method in type.GetMethods(flags))
                {
                    if (method.IsPrivate || method.IsAssembly && !IsVisibleToTests(method))
                    {
                        continue;
                    }

                    Flag(method.ReturnType, $"{type.Name}.{method.Name} 返回", violations);
                    foreach (ParameterInfo p in method.GetParameters())
                    {
                        Flag(p.ParameterType, $"{type.Name}.{method.Name} 参数 {p.Name}", violations);
                    }
                }

                foreach (PropertyInfo prop in type.GetProperties(flags))
                {
                    Flag(prop.PropertyType, $"{type.Name}.{prop.Name} 属性", violations);
                }

                foreach (FieldInfo field in type.GetFields(flags))
                {
                    if (field.IsPrivate)
                    {
                        continue;
                    }

                    Flag(field.FieldType, $"{type.Name}.{field.Name} 字段", violations);
                }
            }

            Assert.That(violations, Is.Empty,
                "表现层表面出现可变状态/权威类型：\n" + string.Join("\n", violations));
        }

        [Test]
        public void FromInstance_DeserializedState_RendersIdenticallyToOriginal()
        {
            CardDatabase db = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            MatchState original = MatchControllerFixtures.NewMatch(db, deck, seed: 7);
            MatchState clone = MatchStateSerializer.Deserialize(MatchStateSerializer.Serialize(original));

            IReadOnlyCardInstance cardA = original.GetPlayer(original.ActivePlayerId).Hand.Cards[0];
            IReadOnlyCardInstance cardB = clone.GetPlayer(clone.ActivePlayerId).Hand.Cards[0];

            CardViewData dataA = CardViewData.FromInstance(db.RequireCard(cardA.CardKey), cardA);
            CardViewData dataB = CardViewData.FromInstance(db.RequireCard(cardB.CardKey), cardB);

            Assert.That(dataB.InstanceId, Is.EqualTo(dataA.InstanceId));
            Assert.That(dataB.Name, Is.EqualTo(dataA.Name));
            Assert.That(dataB.Cost, Is.EqualTo(dataA.Cost));
            Assert.That(dataB.Attack, Is.EqualTo(dataA.Attack));
            Assert.That(dataB.Health, Is.EqualTo(dataA.Health));
            Assert.That(dataB.Type, Is.EqualTo(dataA.Type));
        }

        [Test]
        public void CardViewData_FromInstance_AcceptsReadOnlyCard()
        {
            CardDatabase db = MatchControllerFixtures.BuildDatabase();
            IReadOnlyList<string> deck = MatchControllerFixtures.LoadEnabledDeckKeys();
            MatchState state = MatchControllerFixtures.NewMatch(db, deck, seed: 7);
            IReadOnlyCardInstance card = state.GetPlayer(state.ActivePlayerId).Hand.Cards[0];

            CardViewData data = CardViewData.FromInstance(db.RequireCard(card.CardKey), card);

            Assert.That(data.InstanceId, Is.EqualTo(card.InstanceId));
            Assert.That(data.Attack, Is.EqualTo(card.Attack));
            Assert.That(data.Health, Is.EqualTo(card.Health));
        }

        private static bool IsVisibleToTests(MethodInfo method) => method.IsAssembly;

        private static void Flag(Type type, string where, List<string> violations)
        {
            Type probe = type.IsByRef ? type.GetElementType()! : type;
            if (ForbiddenTypes.Contains(probe))
            {
                violations.Add($"{where}：{probe.Name}");
            }
        }
    }
}

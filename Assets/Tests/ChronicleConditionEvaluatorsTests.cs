using System.Collections.Generic;
using NUnit.Framework;
using Ale.Chronicle;
using Ale.Condition;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 角色条件判定器门槛：HasTrait / AttributeCompare（5 种比较）/ Age（区间 + 无上限）经 ConditionEngine 求值正确；
    /// 无数据源时安全失败；ParamSchema / Key 正确；被 AutoRegister 发现。
    /// </summary>
    public class ChronicleConditionEvaluatorsTests
    {
        private sealed class FakeSource : IChronicleConditionSource
        {
            public readonly HashSet<string> Traits = new HashSet<string>();
            public readonly Dictionary<string, float> Attrs = new Dictionary<string, float>();
            public int Age;
            public bool HasTrait(EConditionScope s, string t) => Traits.Contains(t);
            public float GetCoreAttribute(EConditionScope s, string a) => Attrs.TryGetValue(a, out var v) ? v : 0f;
            public int GetAge(EConditionScope s) => Age;
        }

        private sealed class Ctx : IConditionContext
        {
            public IChronicleConditionSource Source;
            public object Subject => null;
            public T GetService<T>() where T : class => Source as T;
        }

        private static ConditionRegistry Registry()
        {
            var r = new ConditionRegistry();
            r.Register(new HasTraitEvaluator());
            r.Register(new AttributeCompareEvaluator());
            r.Register(new AgeEvaluator());
            return r;
        }

        private static ConditionExpression Wrap(ConditionItem it)
        {
            var g = new ConditionGroup { itemOperator = ConditionLogicOp.And };
            g.items.Add(it);
            var e = new ConditionExpression { groupOperator = ConditionLogicOp.And };
            e.groups.Add(g);
            return e;
        }

        private static ConditionParam PInt(string id, long v)    { var p = new ConditionParam(id, ConditionParamType.Int);    p.SetInt(v);    return p; }
        private static ConditionParam PFloat(string id, double v) { var p = new ConditionParam(id, ConditionParamType.Float);  p.SetFloat(v);  return p; }
        private static ConditionParam PStr(string id, string v)   { var p = new ConditionParam(id, ConditionParamType.String); p.SetString(v); return p; }

        private readonly ConditionRegistry _reg = Registry();

        [Test]
        public void HasTrait_TrueWhenPresent()
        {
            var src = new FakeSource(); src.Traits.Add("brave");
            var ctx = new Ctx { Source = src };

            var it = new ConditionItem("Chronicle.HasTrait");
            it.parameters.Add(PInt("scope", (int)EConditionScope.Actor));
            it.parameters.Add(PStr("traitId", "brave"));

            Assert.IsTrue(ConditionEngine.Evaluate(Wrap(it), ctx, _reg).Passed);

            src.Traits.Clear();
            Assert.IsFalse(ConditionEngine.Evaluate(Wrap(it), ctx, _reg).Passed);
        }

        [Test]
        public void AttributeCompare_Operators()
        {
            var src = new FakeSource(); src.Attrs["str"] = 10f;
            var ctx = new Ctx { Source = src };

            ConditionExpression Cmp(int op, float val)
            {
                var it = new ConditionItem("Chronicle.AttributeCompare");
                it.parameters.Add(PInt("scope", (int)EConditionScope.Actor));
                it.parameters.Add(PStr("attrId", "str"));
                it.parameters.Add(PInt("op", op));
                it.parameters.Add(PFloat("value", val));
                return Wrap(it);
            }

            Assert.IsTrue (ConditionEngine.Evaluate(Cmp(ChronicleCompareOp.GreaterOrEqual, 5f),  ctx, _reg).Passed);
            Assert.IsFalse(ConditionEngine.Evaluate(Cmp(ChronicleCompareOp.Greater,        10f), ctx, _reg).Passed);
            Assert.IsTrue (ConditionEngine.Evaluate(Cmp(ChronicleCompareOp.Equal,          10f), ctx, _reg).Passed);
            Assert.IsTrue (ConditionEngine.Evaluate(Cmp(ChronicleCompareOp.LessOrEqual,    10f), ctx, _reg).Passed);
            Assert.IsTrue (ConditionEngine.Evaluate(Cmp(ChronicleCompareOp.Less,           20f), ctx, _reg).Passed);
        }

        [Test]
        public void Age_Range()
        {
            var src = new FakeSource(); src.Age = 25;
            var ctx = new Ctx { Source = src };

            ConditionExpression AgeExpr(int min, int max)
            {
                var it = new ConditionItem("Chronicle.Age");
                it.parameters.Add(PInt("scope", (int)EConditionScope.Actor));
                it.parameters.Add(PInt("min", min));
                it.parameters.Add(PInt("max", max));
                return Wrap(it);
            }

            Assert.IsTrue (ConditionEngine.Evaluate(AgeExpr(18, 34), ctx, _reg).Passed);
            Assert.IsFalse(ConditionEngine.Evaluate(AgeExpr(30, 40), ctx, _reg).Passed);
            Assert.IsTrue (ConditionEngine.Evaluate(AgeExpr(18, -1), ctx, _reg).Passed); // 无上限
        }

        [Test]
        public void NoSource_FailsClosed()
        {
            var ctx = new Ctx { Source = null };
            var it = new ConditionItem("Chronicle.HasTrait");
            it.parameters.Add(PInt("scope", 0));
            it.parameters.Add(PStr("traitId", "x"));
            Assert.IsFalse(ConditionEngine.Evaluate(Wrap(it), ctx, _reg).Passed);
        }

        [Test]
        public void Schema_And_Keys()
        {
            var e = new AttributeCompareEvaluator();
            Assert.AreEqual("Chronicle.AttributeCompare", e.Key);
            Assert.AreEqual("角色", e.Category);

            var ids = new List<string>();
            foreach (var d in e.ParamSchema) ids.Add(d.id);
            CollectionAssert.AreEqual(new[] { "scope", "attrId", "op", "value" }, ids);
        }

        [Test]
        public void AutoRegister_FindsChronicleEvaluators()
        {
            var r = new ConditionRegistry();
            r.AutoRegisterFromAssemblies();
            Assert.IsTrue(r.TryGet("Chronicle.HasTrait", out _));
            Assert.IsTrue(r.TryGet("Chronicle.AttributeCompare", out _));
            Assert.IsTrue(r.TryGet("Chronicle.Age", out _));
        }
    }
}

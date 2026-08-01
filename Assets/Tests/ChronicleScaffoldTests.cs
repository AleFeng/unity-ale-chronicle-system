using NUnit.Framework;
using Ale.Chronicle;
using Ale.Condition;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 脚手架 smoke 测试：确认 com.ale.chronicle 程序集编译通过，且能跨程序集引用 toolkit（Schema 引擎）
    /// 与 Ale.Condition（条件系统）核心。
    /// </summary>
    public class ChronicleScaffoldTests
    {
        [Test]
        public void Package_Compiles_And_References_Toolkit_And_Condition()
        {
            Assert.AreEqual("com.ale.chronicle", ChronicleInfo.PackageName);
            Assert.IsTrue(ChronicleInfo.ProbeReferences());

            var av = new AttributeValue();                    // Ale.Toolkit.Runtime
            Assert.IsNotNull(av);

            Assert.IsTrue(new ConditionExpression().IsEmpty); // Ale.Condition.Core
        }
    }
}

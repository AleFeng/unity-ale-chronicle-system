using Ale.Condition;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 编年史系统包信息（脚手架占位）。后续步骤在此程序集内填充属性系统 / 特质 / 组合装配 / 数据库 / 序列化等。
    /// </summary>
    public static class ChronicleInfo
    {
        public const string PackageName = "com.ale.chronicle";
        public const string Version     = "0.1.0";

        /// <summary>脚手架自检：证明本程序集可引用 toolkit（Schema 引擎）与 condition（条件系统）核心。</summary>
        public static bool ProbeReferences()
        {
            var av = new AttributeValue();      // Ale.Toolkit.Runtime
            var e  = new ConditionExpression(); // Ale.Condition.Core
            return av != null && e.IsEmpty;
        }
    }
}

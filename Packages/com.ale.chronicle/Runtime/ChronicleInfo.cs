using Ale.Condition;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 编年史系统包信息。版本常量应与 <c>package.json</c> 的 <c>version</c> 保持一致。
    /// </summary>
    public static class ChronicleInfo
    {
        public const string PackageName = "com.ale.chronicle";
        public const string Version     = "0.3.2";

        /// <summary>脚手架自检：证明本程序集可引用 toolkit（Schema 引擎）与 condition（条件系统）核心。</summary>
        public static bool ProbeReferences()
        {
            var av = new AttributeValue();      // Ale.Toolkit.Runtime
            var e  = new ConditionExpression(); // Ale.Condition.Core
            return av != null && e.IsEmpty;
        }
    }
}

using Ale.Toolkit.Runtime;
using UnityEngine;

namespace Ale.Chronicle
{
    /// <summary>
    /// 把编年史的枚举类型集合接到 toolkit 的 <see cref="EnumTypeResolver"/> 上（<b>运行时</b>路径）。
    /// <see cref="AttributeValue"/> 把枚举整数值转显示名时按类型名找 <see cref="EnumType"/>，
    /// 集合由 <see cref="ChronicleDataManager"/> 持有。委托惰性求值（调用时才访问 Instance），不提前建单例。
    /// <para>编辑器（非播放）路径由 <c>ChronicleEnumResolverEditorInstaller</c> 以 <c>[InitializeOnLoad]</c> 安装同一委托。</para>
    /// </summary>
    internal static class ChronicleEnumResolverInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            EnumTypeResolver.Resolve = n => ChronicleDataManager.Instance != null
                ? ChronicleDataManager.Instance.GetEnumType(n)
                : null;
        }
    }
}

using Ale.Chronicle;
using Ale.Toolkit.Runtime;
using UnityEditor;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 编辑器（非播放）路径下把编年史枚举类型集合接到 toolkit 的 <see cref="EnumTypeResolver"/> 上。
    /// <c>[RuntimeInitializeOnLoadMethod]</c> 只在进入播放时触发；编辑器内绘制枚举属性字段也需解析，
    /// 故用 <c>[InitializeOnLoad]</c> 在编辑器加载时安装同一委托（惰性求值，不提前建单例）。
    /// </summary>
    [InitializeOnLoad]
    internal static class ChronicleEnumResolverEditorInstaller
    {
        static ChronicleEnumResolverEditorInstaller()
        {
            EnumTypeResolver.Resolve = n => ChronicleDataManager.Instance != null
                ? ChronicleDataManager.Instance.GetEnumType(n)
                : null;
        }
    }
}

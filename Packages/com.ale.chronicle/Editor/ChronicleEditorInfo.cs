using System;
using Ale.Toolkit.Editor;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 编年史编辑器程序集的脚手架占位。后续步骤在此填充编辑器窗口 / 页签 / 检视器 / 绘制器。
    /// </summary>
    internal static class ChronicleEditorInfo
    {
        // 脚手架自检：证明 Editor 程序集可引用 toolkit editor / condition editor / chronicle runtime。
        internal static Type[] Probe() => new[]
        {
            typeof(IEditorContext),                                  // Ale.Toolkit.Editor
            typeof(Ale.Condition.Editor.ConditionEvaluatorCatalog), // Ale.Condition.Editor
            typeof(Ale.Chronicle.ChronicleInfo),                    // Ale.Chronicle.Runtime
        };
    }
}

using System.Collections.Generic;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Editor;

namespace Ale.Chronicle.Editor
{
    /// <summary>「枚举」页签：左列枚举类型列表，右列枚举项与其 schema 编辑。机制全部下沉至 toolkit 的枚举面板。</summary>
    public sealed class EnumSystemTab : ChronicleTwoColumnTab
    {
        private readonly EnumTypePanel _panel = new EnumTypePanel();
        protected override IEditorMasterListPanel<ChronicleDatabase> Panel => _panel;
    }

    /// <summary>
    /// 枚举类型面板（编年史闭合）：编辑机制全部来自 <see cref="EditorEnumTypePanel{ChronicleDatabase}"/>，
    /// 本类仅绑定 <see cref="ChronicleDatabase.EnumTypesList"/>。schema 内枚举引用经 <see cref="ChronicleDatabase"/>
    /// 实现的 <see cref="IEnumTypeSource"/> 解析。
    /// </summary>
    public sealed class EnumTypePanel : EditorEnumTypePanel<ChronicleDatabase>
    {
        protected override List<EnumType> GetList(ChronicleDatabase db) => db.EnumTypesList;
    }
}

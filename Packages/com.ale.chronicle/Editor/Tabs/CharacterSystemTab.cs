using System.Collections.Generic;
using Ale.Chronicle;
using Ale.Toolkit.Editor;

namespace Ale.Chronicle.Editor
{
    /// <summary>「角色」页签（Phase 1 核心交付）：左列角色列表，右列组合装配检视器 + 属性汇流实时预览。</summary>
    public sealed class CharacterSystemTab : ChronicleTwoColumnTab
    {
        private readonly CharacterListPanel _panel = new CharacterListPanel();
        protected override IEditorMasterListPanel<ChronicleDatabase> Panel => _panel;
    }

    /// <summary>角色主列表面板：绑定 <see cref="ChronicleDatabase.Characters"/>，右列交给 <see cref="CharacterInspectorPanel"/>。</summary>
    public sealed class CharacterListPanel : EditorMasterListPanel<CharacterDefinition>
    {
        protected override List<CharacterDefinition> GetList(ChronicleDatabase db) => db.Characters;
        protected override string Noun => "角色";

        protected override string RowLabel(CharacterDefinition item)
        {
            string idPart = string.IsNullOrEmpty(item.id) ? "(未命名)" : item.id;
            string name   = item.GetAttributeValue<string>(WellKnownAttr.Name);
            return string.IsNullOrEmpty(name) ? idPart : $"{idPart} · {name}";
        }

        protected override CharacterDefinition CreateNew(ChronicleDatabase db, List<CharacterDefinition> list)
        {
            int n = list.Count + 1;
            string id;
            do { id = "char_" + n; n++; } while (Contains(list, id));
            var c = new CharacterDefinition(id);
            if (db.CharacterTemplates.Count > 0)
                c.templateRef = db.CharacterTemplates[0].name;   // 默认取首个模板，用户可改
            return c;
        }

        private static bool Contains(List<CharacterDefinition> list, string id)
        {
            foreach (var c in list) if (c != null && c.id == id) return true;
            return false;
        }

        public override void DrawInspector(IChronicleEditorContext ctx, CharacterDefinition item)
            => CharacterInspectorPanel.Draw(ctx, item);
    }
}

using System.Collections.Generic;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 「左栏子页签 + 右栏 Inspector」两列页签基类（无中列实体列表）：左列顶部一行子页签工具栏，
    /// 切换其下的主列表面板（<see cref="IEditorMasterListPanel{ChronicleDatabase}"/>），右列为选中项 Inspector。
    /// 用于「通用」这类「多个扁平配置目录」的集中管理。
    /// </summary>
    public abstract class ChronicleSubTabPanelTab : IEditorSystemTab<ChronicleDatabase>
    {
        private const float Padding = 4f, LeftWidth = 320f, TabRowH = 22f;

        private int     _subTab;
        private int[]   _selected;
        private Vector2 _leftScroll, _rightScroll;

        protected abstract string[] SubTabLabels { get; }
        protected abstract IEditorMasterListPanel<ChronicleDatabase>[] Panels { get; }

        private int[] Selected
        {
            get
            {
                var panels = Panels;
                if (_selected == null || _selected.Length != panels.Length)
                {
                    _selected = new int[panels.Length];
                    for (int i = 0; i < _selected.Length; i++) _selected[i] = -1;
                }
                return _selected;
            }
        }

        public void OnGUI(Rect rect, IEditorDbContext<ChronicleDatabase> ctx)
        {
            var panels = Panels;
            var labels = SubTabLabels;

            var leftRect  = new Rect(rect.x + Padding, rect.y + Padding, LeftWidth, rect.height - Padding * 2);
            var rightRect = new Rect(leftRect.xMax + Padding, rect.y + Padding,
                rect.width - LeftWidth - Padding * 3, rect.height - Padding * 2);

            // ── 左列：子页签工具栏 + 主列表 ────────────────────────────────────────
            GUILayout.BeginArea(leftRect, EditorStyles.helpBox);
            if (labels != null && labels.Length > 1)
                _subTab = GUILayout.Toolbar(_subTab, labels, GUILayout.Height(TabRowH));
            _subTab = Mathf.Clamp(_subTab, 0, panels.Length - 1);

            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
            var sel = Selected;
            sel[_subTab] = panels[_subTab].DrawMasterList(ctx, sel[_subTab]);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();

            // ── 右列：Inspector ──────────────────────────────────────────────────
            GUILayout.BeginArea(rightRect, EditorStyles.helpBox);
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll, false, false,
                GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
            panels[_subTab].DrawInspectorAt(ctx, Selected[_subTab]);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        public void OnDatabaseChanged(IEditorDbContext<ChronicleDatabase> ctx)
        {
            _selected = null;
            foreach (var p in Panels) p.Invalidate();
        }

        public void OnUndoRedo()
        {
            foreach (var p in Panels) p.Invalidate();
        }
    }

    /// <summary>「通用」页签：左栏子页签 枚举 / 功能标签 / 分组标签 / 数字格式，各自增删改，右列 Inspector。</summary>
    public sealed class GeneralSystemTab : ChronicleSubTabPanelTab
    {
        private readonly EnumTypePanel              _enum   = new EnumTypePanel();
        private readonly ChronicleTagPanel          _tag    = new ChronicleTagPanel();
        private readonly ChronicleGroupTagPanel     _group  = new ChronicleGroupTagPanel();
        private readonly ChronicleNumberFormatPanel _numfmt = new ChronicleNumberFormatPanel();
        private IEditorMasterListPanel<ChronicleDatabase>[] _panels;

        protected override string[] SubTabLabels => new[] { "枚举", "功能标签", "分组标签", "数字格式" };

        protected override IEditorMasterListPanel<ChronicleDatabase>[] Panels
            => _panels ??= new IEditorMasterListPanel<ChronicleDatabase>[] { _enum, _tag, _group, _numfmt };
    }

    /// <summary>枚举类型面板（编年史闭合）：机制全部来自 <see cref="EditorEnumTypePanel{ChronicleDatabase}"/>，仅绑定 <see cref="ChronicleDatabase.EnumTypesList"/>。</summary>
    public sealed class EnumTypePanel : EditorEnumTypePanel<ChronicleDatabase>
    {
        protected override List<EnumType> GetList(ChronicleDatabase db) => db.EnumTypesList;
    }

    /// <summary>功能标签面板（编年史闭合）：机制来自 toolkit <see cref="EditorTagPanel{ChronicleDatabase}"/>，仅绑定 <see cref="ChronicleDatabase.Tags"/>。</summary>
    public sealed class ChronicleTagPanel : EditorTagPanel<ChronicleDatabase>
    {
        protected override List<Tag> GetList(ChronicleDatabase db) => db.Tags;
    }

    /// <summary>数字格式面板（编年史闭合）：机制来自 toolkit <see cref="EditorNumberFormatConfigPanel{ChronicleDatabase}"/>，仅绑定 <see cref="ChronicleDatabase.NumberFormatConfigs"/>。</summary>
    public sealed class ChronicleNumberFormatPanel : EditorNumberFormatConfigPanel<ChronicleDatabase>
    {
        protected override List<NumberFormatConfig> GetList(ChronicleDatabase db) => db.NumberFormatConfigs;
    }

    /// <summary>分组标签面板：绑定 <see cref="ChronicleDatabase.GroupTags"/>，检视 id / 色点 / 名称 / 描述（仿 inventory 分组标签面板）。</summary>
    public sealed class ChronicleGroupTagPanel : EditorMasterListPanel<ChronicleGroupTag>
    {
        protected override List<ChronicleGroupTag> GetList(ChronicleDatabase db) => db.GroupTags;
        protected override string Noun => "分组标签";
        protected override bool   HasColorDot => true;
        protected override Color  RowColor(ChronicleGroupTag item) => item.color;

        protected override string RowLabel(ChronicleGroupTag item)
            => string.IsNullOrEmpty(item.id) ? "(空 ID)" : item.id;

        protected override ChronicleGroupTag CreateNew(ChronicleDatabase db, List<ChronicleGroupTag> list)
        {
            int n = list.Count + 1;
            string id;
            do { id = "group_" + n; n++; } while (Contains(list, id));
            var t = new ChronicleGroupTag(id);
            t.displayName.SetTextValue(0, "新分组");
            return t;
        }

        private static bool Contains(List<ChronicleGroupTag> list, string id)
        {
            foreach (var t in list) if (t != null && t.id == id) return true;
            return false;
        }

        public override void DrawInspector(IChronicleEditorContext ctx, ChronicleGroupTag tag)
        {
            if (tag == null)
            {
                EditorGUILayout.LabelField("请选择或新建一个分组标签。", ToolkitEditorStyles.Placeholder);
                return;
            }

            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);
            EditorGUI.BeginChangeCheck();
            string id    = EditorGUILayout.TextField("ID", tag.id);
            Color  color = EditorGUILayout.ColorField("标识颜色", tag.color);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改分组标签");
                tag.id    = id;
                tag.color = color;
                ctx.MarkDirty();
            }

            tag.NormalizeTextFields();
            AttributeFieldDrawer.Draw(ctx, "名称", tag.displayName, null);
            AttributeFieldDrawer.Draw(ctx, "描述", tag.description, null);
        }
    }
}

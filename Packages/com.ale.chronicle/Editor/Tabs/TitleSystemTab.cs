using System.Collections.Generic;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 「头衔」页签（三列）：左=头衔模板 / 阶级序列两个子页签（模板列表 + 选中序列后右列渲染有序阶梯编辑器）、
    /// 中=头衔列表（按模板过滤 / 搜索 / 从模板添加）、右=头衔 Inspector（选中头衔时）或模板 / 阶级序列 Inspector（选中左列条目时）。
    /// 中列过滤维为头衔模板（<c>TTemplate=TitleTemplate</c>）；分组标签降为检视器可编辑字段。
    /// </summary>
    public sealed class TitleSystemTab : EditorThreeColumnTab<TitleDefinition>
    {
        private readonly TitleTemplatePanel  _templatePanel = new TitleTemplatePanel();
        private readonly RankLadderListPanel _ladderPanel   = new RankLadderListPanel();
        private readonly TitleListPanel      _listPanel     = new TitleListPanel();
        private IEditorMasterListPanel<ChronicleDatabase>[] _leftPanels;

        protected override string[] LeftSubTabs => new[] { "头衔模板", "阶级序列" };

        protected override IEditorMasterListPanel<ChronicleDatabase>[] LeftPanels
            => _leftPanels ??= new IEditorMasterListPanel<ChronicleDatabase>[] { _templatePanel, _ladderPanel };

        protected override string EntityNoun => "头衔";

        protected override List<TitleDefinition> EntityList(ChronicleDatabase db) => db.Titles;

        protected override TitleDefinition DrawEntityList(IChronicleEditorContext ctx, TitleDefinition displaySelected)
            => _listPanel.DrawList(ctx, displaySelected);

        protected override TitleDefinition ConsumePendingSelect() => _listPanel.ConsumePendingSelect();

        protected override void DrawEntityInspector(IChronicleEditorContext ctx, TitleDefinition entity)
            => TitleInspectorPanel.Draw(ctx, entity);
    }

    /// <summary>头衔列表面板（中列）：按分组标签过滤 + 搜索 + 添加，每行 id / 名称 / 类型 / 位阶。</summary>
    public sealed class TitleListPanel : EditorEntityListPanel<TitleDefinition, TitleTemplate>
    {
        public TitleListPanel() : base("ChronicleTitleListDrag") { }

        protected override EChronicleEntityKind Kind => EChronicleEntityKind.Title;
        protected override string Noun => "头衔";

        protected override List<TitleDefinition> Entities(ChronicleDatabase db)  => db.Titles;
        protected override List<TitleTemplate>   Templates(ChronicleDatabase db) => db.TitleTemplates;   // 模板作过滤维
        protected override string TemplateName(TitleTemplate t) => t.name;
        protected override string TemplateRefOf(TitleDefinition e) => e.templateRef;
        protected override string IdOf(TitleDefinition e) => e.id;

        protected override Color RowDotColor(ChronicleDatabase db, TitleDefinition e)
        {
            var t = db.GetTitleTemplate(e.templateRef);
            return t != null ? t.color : Color.gray;
        }

        protected override string SearchName(ChronicleDatabase db, TitleDefinition e)
            => e.displayName != null ? e.displayName.GetTextValue() : null;

        protected override TitleDefinition AddFromTemplate(IChronicleEditorContext ctx, string templateName)
        {
            var db = ctx.Database;
            ctx.RecordUndo("从模板添加头衔");
            var title = new TitleDefinition(GenerateId(db, "title_", id => db.GetTitle(id) != null), templateName);

            // 从模板复制默认预设（种类 / 可剥夺）；自定义属性值由 RebuildAttributes 依模板 schema 初始化。
            var t = db.GetTitleTemplate(templateName);
            if (t != null) { title.kind = t.kind; title.isRevocable = t.isRevocable; }
            title.RebuildAttributes(db);
            db.Titles.Add(title);
            ctx.MarkDirty();
            return title;
        }

        protected override TitleDefinition QuickAdd(IChronicleEditorContext ctx)
        {
            var db = ctx.Database;
            if (db.Titles.Count == 0) return AddFromTemplate(ctx, null);

            ctx.RecordUndo("快速添加头衔");
            var clone = db.Titles[db.Titles.Count - 1].Clone();
            clone.id = GenerateId(db, "title_", id => db.GetTitle(id) != null);
            db.Titles.Add(clone);
            ctx.MarkDirty();
            return clone;
        }

        protected override void DrawRowColumns(ChronicleDatabase db, TitleDefinition e,
            Rect keyRow, float contentX, float contentRight, float valY, float valH)
        {
            float w     = Mathf.Max(0f, contentRight - contentX);
            float idW   = Mathf.Min(90f, w * 0.26f);
            float kindW = Mathf.Min(56f, w * 0.16f);
            float tierW = Mathf.Min(48f, w * 0.14f);
            float nameX = contentX + idW + Pad;
            float tierX = contentRight - tierW;
            float kindX = tierX - Pad - kindW;
            float nameW = Mathf.Max(0f, kindX - Pad - nameX);

            GUI.Label(new Rect(contentX, keyRow.y, idW,   keyRow.height), "ID",   KeyStyle);
            GUI.Label(new Rect(nameX,    keyRow.y, nameW, keyRow.height), "名称", KeyStyle);
            GUI.Label(new Rect(kindX,    keyRow.y, kindW, keyRow.height), "类型", KeyStyle);
            GUI.Label(new Rect(tierX,    keyRow.y, tierW, keyRow.height), "位阶", KeyStyle);

            GUI.Label(new Rect(contentX, valY, idW, valH), string.IsNullOrEmpty(e.id) ? "(空 ID)" : e.id, IdStyle);
            string name = e.displayName != null ? e.displayName.GetTextValue() : null;
            GUI.Label(new Rect(nameX, valY, nameW, valH), string.IsNullOrEmpty(name) ? "—" : name, SubStyle);
            GUI.Label(new Rect(kindX, valY, kindW, valH), e.kind == ETitleKind.RankTitle ? "阶级" : "称号", SubStyle);
            GUI.Label(new Rect(tierX, valY, tierW, valH), e.kind == ETitleKind.RankTitle ? e.rankTier.ToString() : "—", SubStyle);
        }
    }

    /// <summary>头衔模板主列表面板（头衔页左列首个子页签）：绑定 <see cref="ChronicleDatabase.TitleTemplates"/>；专属字段=默认种类 / 可剥夺。</summary>
    public sealed class TitleTemplatePanel : ChronicleTemplateListPanel<TitleTemplate>
    {
        protected override List<TitleTemplate> GetList(ChronicleDatabase db) => db.TitleTemplates;
        protected override string Noun => "头衔模板";
        protected override string NewNamePrefix => "title_template_";
        protected override TitleTemplate NewTemplate(string name) => new TitleTemplate(name);
        protected override string SchemaLabel => "自定义属性字段 schema";

        protected override void DrawExtras(IChronicleEditorContext ctx, TitleTemplate tmpl)
        {
            EditorGUI.BeginChangeCheck();
            var  kind        = (ETitleKind)EditorGUILayout.EnumPopup("默认种类", tmpl.kind);
            bool isRevocable = EditorGUILayout.Toggle("默认可被剥夺", tmpl.isRevocable);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改头衔模板");
                tmpl.kind        = kind;
                tmpl.isRevocable = isRevocable;
                ctx.MarkDirty();
            }
        }
    }

    /// <summary>阶级序列主列表面板（头衔页左列）：绑定 <see cref="ChronicleDatabase.RankLadders"/>；选中后右列渲染有序阶梯编辑器。</summary>
    public sealed class RankLadderListPanel : EditorMasterListPanel<RankLadder>
    {
        protected override List<RankLadder> GetList(ChronicleDatabase db) => db.RankLadders;
        protected override string Noun => "阶级序列";

        protected override string RowLabel(RankLadder item)
        {
            string name = item.displayName != null ? item.displayName.GetTextValue() : null;
            return !string.IsNullOrEmpty(name) ? name : (string.IsNullOrEmpty(item.id) ? "(未命名)" : item.id);
        }

        protected override RankLadder CreateNew(ChronicleDatabase db, List<RankLadder> list)
        {
            int n = list.Count + 1;
            string id;
            do { id = "rank_ladder_" + n; n++; } while (Exists(list, id));
            return new RankLadder(id);
        }

        private static bool Exists(List<RankLadder> list, string id)
        {
            foreach (var l in list) if (l != null && l.id == id) return true;
            return false;
        }

        public override void DrawInspector(IChronicleEditorContext ctx, RankLadder ladder)
        {
            if (ladder == null)
            {
                EditorGUILayout.LabelField("请选择或新建一条阶级序列。", ToolkitEditorStyles.Placeholder);
                return;
            }

            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);
            ChronicleEntityHeader.DrawIdField(ctx, "阶级序列", ladder.id,
                ctx.DuplicateIdsOf(EChronicleEntityKind.RankLadder), v => ladder.id = v);
            AttributeFieldDrawer.Draw(ctx, "显示名", ladder.displayName, null);

            EditorGUILayout.Space(6);
            RankLadderDrawer.Draw(ctx, ladder);
        }
    }

    /// <summary>头衔实例检视器（头衔页右列）：ID / 显示信息 / 类型 / 分组 / 爵位或称号专属字段 / 修饰器 / 好感修饰器 / 获得条件。</summary>
    public static class TitleInspectorPanel
    {
        public static void Draw(IChronicleEditorContext ctx, TitleDefinition title)
        {
            if (title == null)
            {
                EditorGUILayout.LabelField("请选择或新建一个头衔。", ToolkitEditorStyles.Placeholder);
                return;
            }

            var db = ctx.Database;

            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);
            ChronicleEntityHeader.DrawIdField(ctx, "头衔", title.id,
                ctx.DuplicateIdsOf(EChronicleEntityKind.Title), v => title.id = v);
            ChronicleEntityHeader.DrawTemplateRefReadonly(title.templateRef);
            AttributeFieldDrawer.Draw(ctx, "显示名", title.displayName, null);
            AttributeFieldDrawer.Draw(ctx, "说明",   title.description, null);
            AttributeFieldDrawer.Draw(ctx, "图标",   title.icon, null);

            EditorGUI.BeginChangeCheck();
            var kind = (ETitleKind)EditorGUILayout.EnumPopup("类型", title.kind);
            if (EditorGUI.EndChangeCheck()) { ctx.RecordUndo("修改头衔类型"); title.kind = kind; ctx.MarkDirty(); }

            ChronicleEditorFields.GroupPopup(ctx, title.groupTagRef, "修改头衔分组", v => title.groupTagRef = v);

            if (title.kind == ETitleKind.RankTitle)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("阶级头衔专属", ToolkitEditorStyles.Header);
                EditorGUI.BeginChangeCheck();
                int  rankTier   = EditorGUILayout.IntField("位阶(越大越高)", title.rankTier);
                bool heritable  = EditorGUILayout.Toggle("可继承", title.heritable);
                string succ     = EditorGUILayout.TextField("继承法引用", title.successionPolicyRef);
                if (EditorGUI.EndChangeCheck())
                {
                    ctx.RecordUndo("修改阶级头衔字段");
                    title.rankTier = rankTier; title.heritable = heritable; title.successionPolicyRef = succ;
                    ctx.MarkDirty();
                }
            }

            EditorGUILayout.Space(4);
            EditorGUI.BeginChangeCheck();
            bool isUnique    = EditorGUILayout.Toggle("全世界唯一", title.isUnique);
            bool isRevocable = EditorGUILayout.Toggle("可被剥夺",   title.isRevocable);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改头衔标志");
                title.isUnique = isUnique; title.isRevocable = isRevocable;
                ctx.MarkDirty();
            }

            EditorGUILayout.Space(6);
            ModifierListDrawer.Draw(ctx, "修饰器（汇入核心属性 · title:{id}）", title.modifiers, db);

            EditorGUILayout.Space(6);
            ModifierListDrawer.Draw(ctx, "好感修饰器（社交轴 · 暂不汇入核心属性）", title.opinionModifiers, db);

            EditorGUILayout.Space(6);
            DrawAcquisitionConditions(ctx, title);

            EditorGUILayout.Space(6);
            var tmpl = db.GetTitleTemplate(title.templateRef);
            ChronicleEntityHeader.DrawCustomAttributes(ctx, title.values, tmpl?.attributes,
                "（该头衔暂无自定义属性字段；可在左侧「头衔模板」中添加）");
        }

        private static void DrawAcquisitionConditions(IChronicleEditorContext ctx, TitleDefinition title)
        {
            EditorGUILayout.LabelField("获得条件", ToolkitEditorStyles.Header);
            ChronicleEditorFields.InlineCondition(ctx, "titles",
                () => ctx.Database.Titles.IndexOf(title), "acquisitionConditions");
        }
    }

    /// <summary>阶级序列有序阶梯编辑器：orderedTitleRefs 低→高，可上下移 / 移除 / 添加（仅列阶级头衔类）。</summary>
    public static class RankLadderDrawer
    {
        public static void Draw(IChronicleEditorContext ctx, RankLadder ladder)
        {
            var db = ctx.Database;
            EditorGUILayout.LabelField("阶梯（低 → 高，同时只持其一）", ToolkitEditorStyles.Header);

            var refs = ladder.orderedTitleRefs;
            if (refs.Count == 0)
                EditorGUILayout.LabelField("（空序列；用下方按钮添加阶级头衔）", ToolkitEditorStyles.Placeholder);

            // 延迟应用：绘制期只记录动作，行 / 组闭合后再改列表（避免在 DisabledScope 内提前 EndHorizontal 破坏 IMGUI 组嵌套）。
            int moveFrom = -1, moveTo = -1, removeAt = -1;
            for (int i = 0; i < refs.Count; i++)
            {
                string tid   = refs[i];
                var    title = db.GetTitle(tid);
                bool   ok    = title != null && title.kind == ETitleKind.RankTitle;
                string label = title != null ? $"{i + 1}. {title.PlainName()}  (tier {title.rankTier})"
                                             : $"{i + 1}. {tid}（缺失）";

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(label, ok ? EditorStyles.label : ToolkitEditorStyles.StatusError);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUILayout.Button("▲", GUILayout.Width(24))) { moveFrom = i; moveTo = i - 1; }
                using (new EditorGUI.DisabledScope(i == refs.Count - 1))
                    if (GUILayout.Button("▼", GUILayout.Width(24))) { moveFrom = i; moveTo = i + 1; }
                if (GUILayout.Button("✕", GUILayout.Width(24))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }

            if (removeAt >= 0)
            {
                ctx.RecordUndo("移除阶梯头衔"); refs.RemoveAt(removeAt); ctx.MarkDirty();
            }
            else if (moveFrom >= 0 && moveTo >= 0 && moveTo < refs.Count)
            {
                ctx.RecordUndo("调整阶梯顺序");
                (refs[moveFrom], refs[moveTo]) = (refs[moveTo], refs[moveFrom]);
                ctx.MarkDirty();
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ 添加阶级头衔", GUILayout.Width(130)))
                ShowAddMenu(ctx, ladder);
        }

        private static void ShowAddMenu(IChronicleEditorContext ctx, RankLadder ladder)
        {
            var db = ctx.Database;
            var menu = new GenericMenu();
            bool any = false;
            foreach (var t in db.Titles)
            {
                if (t == null || string.IsNullOrEmpty(t.id)) continue;
                if (t.kind != ETitleKind.RankTitle) continue;          // 仅阶级头衔
                if (ladder.orderedTitleRefs.Contains(t.id)) continue;  // 已在阶梯
                any = true;
                string captured = t.id;
                string label = $"{t.PlainName()} ({t.id}) · tier {t.rankTier}";
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    ctx.RecordUndo("添加阶梯头衔");
                    ladder.orderedTitleRefs.Add(captured);
                    ctx.MarkDirty();
                    ctx.Repaint();
                });
            }
            if (!any) menu.AddDisabledItem(new GUIContent("（无可添加的阶级头衔）"));
            menu.ShowAsContext();
        }
    }
}

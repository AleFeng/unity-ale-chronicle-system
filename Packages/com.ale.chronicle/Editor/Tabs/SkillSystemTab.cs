using System.Collections.Generic;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 「技能」页签（三列）：左=技能模板列表、中=技能列表（模板过滤 / 搜索 / 从模板添加 / 快速添加）、
    /// 右=技能 Inspector（选中技能时）或技能模板 Inspector（选中左列模板时）。
    /// 分组标签复用统一 <see cref="ChronicleDatabase.GroupTags"/> 池（在「通用 → 分组标签」管理），故左列不设分组标签面板。
    /// </summary>
    public sealed class SkillSystemTab : EditorThreeColumnTab<Skill>
    {
        private readonly SkillTemplatePanel _templatePanel = new SkillTemplatePanel();
        private readonly SkillTreeListPanel _treePanel     = new SkillTreeListPanel();
        private readonly SkillListPanel     _listPanel     = new SkillListPanel();
        private IEditorMasterListPanel<ChronicleDatabase>[] _leftPanels;

        protected override string[] LeftSubTabs => new[] { "技能模板", "技能树" };

        protected override IEditorMasterListPanel<ChronicleDatabase>[] LeftPanels
            => _leftPanels ??= new IEditorMasterListPanel<ChronicleDatabase>[] { _templatePanel, _treePanel };

        protected override string EntityNoun => "技能";

        protected override List<Skill> EntityList(ChronicleDatabase db) => db.Skills;

        protected override Skill DrawEntityList(IChronicleEditorContext ctx, Skill displaySelected)
            => _listPanel.DrawList(ctx, displaySelected);

        protected override Skill ConsumePendingSelect() => _listPanel.ConsumePendingSelect();

        protected override void DrawEntityInspector(IChronicleEditorContext ctx, Skill entity)
            => SkillInspectorPanel.Draw(ctx, entity);
    }

    /// <summary>技能列表面板（中列）：模板过滤 + 搜索 + 从模板添加 / 快速添加，每行 id / 名称 / 描述 / 主分组。</summary>
    public sealed class SkillListPanel : EditorEntityListPanel<Skill, SkillTemplate>
    {
        public SkillListPanel() : base("ChronicleSkillListDrag") { }

        protected override EChronicleEntityKind Kind => EChronicleEntityKind.Skill;
        protected override string Noun => "技能";

        protected override List<Skill>         Entities(ChronicleDatabase db)  => db.Skills;
        protected override List<SkillTemplate> Templates(ChronicleDatabase db) => db.SkillTemplates;
        protected override string TemplateName(SkillTemplate t) => t.name;
        protected override string TemplateRefOf(Skill e) => e.templateRef;
        protected override string IdOf(Skill e) => e.id;

        protected override Color RowDotColor(ChronicleDatabase db, Skill e)
        {
            var t = db.GetSkillTemplate(e.templateRef);
            return t != null ? t.color : Color.gray;
        }

        protected override string SearchName(ChronicleDatabase db, Skill e)
            => e.displayText != null ? e.displayText.GetTextValue() : null;

        protected override Skill AddFromTemplate(IChronicleEditorContext ctx, string templateName)
        {
            var db = ctx.Database;
            ctx.RecordUndo("从模板添加技能");
            var skill = new Skill(GenerateId(db, "skill_", id => db.GetSkill(id) != null), templateName);

            // 从模板复制「技能默认信息」（名称 / 描述 / 图标 / 分组标签）；自定义属性值由 RebuildAttributes 依模板 schema 初始化。
            var t = db.GetSkillTemplate(templateName);
            if (t != null)
            {
                skill.displayText        = t.displayText     != null ? t.displayText.Clone()     : new AttributeValue(EFieldType.Text);
                skill.descriptionText    = t.descriptionText != null ? t.descriptionText.Clone() : new AttributeValue(EFieldType.Text);
                skill.iconValue          = t.iconValue != null ? t.iconValue.Clone() : new AttributeValue(EFieldType.Sprite);
                skill.primaryGroupTag    = t.primaryGroupTag;
                skill.secondaryGroupTags = new List<string>(t.secondaryGroupTags);
            }
            skill.RebuildAttributes(db);
            db.Skills.Add(skill);
            ctx.MarkDirty();
            return skill;
        }

        protected override Skill QuickAdd(IChronicleEditorContext ctx)
        {
            var db = ctx.Database;
            if (db.Skills.Count == 0)
                return AddFromTemplate(ctx, db.SkillTemplates.Count > 0 ? db.SkillTemplates[0].name : null);

            ctx.RecordUndo("快速添加技能");
            var clone = db.Skills[db.Skills.Count - 1].Clone();
            clone.id = GenerateId(db, "skill_", id => db.GetSkill(id) != null);
            db.Skills.Add(clone);
            ctx.MarkDirty();
            return clone;
        }

        protected override void DrawRowColumns(ChronicleDatabase db, Skill e,
            Rect keyRow, float contentX, float contentRight, float valY, float valH)
        {
            float w     = Mathf.Max(0f, contentRight - contentX);
            float idW   = Mathf.Min(90f, w * 0.26f);
            float grpW  = Mathf.Min(84f, w * 0.22f);
            float nameX = contentX + idW + Pad;
            float grpX  = contentRight - grpW;
            float nameW = Mathf.Max(0f, (grpX - Pad - nameX) * 0.42f);
            float descX = nameX + nameW + Pad;
            float descW = Mathf.Max(0f, grpX - Pad - descX);

            GUI.Label(new Rect(contentX, keyRow.y, idW,   keyRow.height), "ID",   KeyStyle);
            GUI.Label(new Rect(nameX,    keyRow.y, nameW, keyRow.height), "名称", KeyStyle);
            GUI.Label(new Rect(descX,    keyRow.y, descW, keyRow.height), "描述", KeyStyle);
            GUI.Label(new Rect(grpX,     keyRow.y, grpW,  keyRow.height), "主分组", KeyStyle);

            GUI.Label(new Rect(contentX, valY, idW, valH), string.IsNullOrEmpty(e.id) ? "(空 ID)" : e.id, IdStyle);

            string name = e.displayText != null ? e.displayText.GetTextValue() : null;
            GUI.Label(new Rect(nameX, valY, nameW, valH), string.IsNullOrEmpty(name) ? "—" : name, SubStyle);

            string desc = e.descriptionText != null ? e.descriptionText.GetTextValue() : null;
            GUI.Label(new Rect(descX, valY, descW, valH), string.IsNullOrEmpty(desc) ? "—" : desc, SubStyle);

            var    grp     = db.GetGroupTag(e.primaryGroupTag);
            string grpName = grp != null ? grp.PlainName() : "—";
            GUI.Label(new Rect(grpX, valY, grpW, valH), grpName, SubStyle);
        }
    }

    /// <summary>技能模板主列表面板（技能页左列）：绑定 <see cref="ChronicleDatabase.SkillTemplates"/>；专属字段=技能默认信息 + 分组标签。</summary>
    public sealed class SkillTemplatePanel : ChronicleTemplateListPanel<SkillTemplate>
    {
        protected override List<SkillTemplate> GetList(ChronicleDatabase db) => db.SkillTemplates;
        protected override string Noun => "技能模板";
        protected override string NewNamePrefix => "skill_template_";
        protected override SkillTemplate NewTemplate(string name) => new SkillTemplate(name);
        protected override string SchemaLabel => "自定义属性字段 schema";

        protected override void DrawExtras(IChronicleEditorContext ctx, SkillTemplate tmpl)
        {
            EditorGUILayout.LabelField("技能默认信息（从模板创建时复制）", ToolkitEditorStyles.Header);
            SkillConfigDrawer.DrawDisplayFields(ctx, tmpl);
            EditorGUILayout.Space(6);
            SkillConfigDrawer.DrawGroupTags(ctx, tmpl);
        }
    }

    /// <summary>技能实例检视器（技能页右列）：ID / 显示信息 / 来源模板 / 分组标签 / 自定义属性字段。</summary>
    public static class SkillInspectorPanel
    {
        public static void Draw(IChronicleEditorContext ctx, Skill skill)
        {
            if (skill == null)
            {
                EditorGUILayout.LabelField("请选择或新建一个技能。", ToolkitEditorStyles.Placeholder);
                return;
            }

            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);
            ChronicleEntityHeader.DrawIdField(ctx, "技能", skill.id,
                ctx.DuplicateIdsOf(EChronicleEntityKind.Skill), v => skill.id = v);

            // 名称 / 描述 / 图标（与技能模板共用绘制）
            SkillConfigDrawer.DrawDisplayFields(ctx, skill);
            ChronicleEntityHeader.DrawTemplateRefReadonly(skill.templateRef);

            EditorGUILayout.Space(6);
            SkillConfigDrawer.DrawGroupTags(ctx, skill);

            EditorGUILayout.Space(6);
            var tmpl = ctx.Database.GetSkillTemplate(skill.templateRef);
            ChronicleEntityHeader.DrawCustomAttributes(ctx, skill.values, tmpl?.attributes,
                "（该技能暂无自定义属性字段；可在左侧「技能模板」中添加）");
        }
    }

    /// <summary>技能树主列表面板（技能页左列第二子页签）：绑定 <see cref="ChronicleDatabase.SkillTrees"/>；
    /// 选中后右列渲染 基础信息 + 类型 + 技能点获取；三类型结构编辑（列表 / 层级 / 树状）在 <c>SkillTreeDrawer</c>（后续步骤）。</summary>
    public sealed class SkillTreeListPanel : EditorMasterListPanel<SkillTree>
    {
        protected override List<SkillTree> GetList(ChronicleDatabase db) => db.SkillTrees;
        protected override string Noun => "技能树";

        protected override string RowLabel(SkillTree item)
        {
            string name = item.displayName != null ? item.displayName.GetTextValue() : null;
            return !string.IsNullOrEmpty(name) ? name : (string.IsNullOrEmpty(item.id) ? "(未命名)" : item.id);
        }

        protected override SkillTree CreateNew(ChronicleDatabase db, List<SkillTree> list)
        {
            int n = list.Count + 1;
            string id;
            do { id = "skill_tree_" + n; n++; } while (Exists(list, id));
            return new SkillTree(id);
        }

        private static bool Exists(List<SkillTree> list, string id)
        {
            foreach (var t in list) if (t != null && t.id == id) return true;
            return false;
        }

        public override void DrawInspector(IChronicleEditorContext ctx, SkillTree tree)
        {
            if (tree == null)
            {
                EditorGUILayout.LabelField("请选择或新建一棵技能树。", ToolkitEditorStyles.Placeholder);
                return;
            }
            tree.Normalize();

            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);
            ChronicleEntityHeader.DrawIdField(ctx, "技能树", tree.id,
                ctx.DuplicateIdsOf(EChronicleEntityKind.SkillTree), v => tree.id = v);
            AttributeFieldDrawer.Draw(ctx, "显示名", tree.displayName, null);

            EditorGUI.BeginChangeCheck();
            var kind = (ESkillTreeKind)EditorGUILayout.EnumPopup("类型", tree.kind);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改技能树类型");
                tree.kind = kind;
                ctx.MarkDirty();
            }

            EditorGUILayout.Space(6);
            SkillTreeDrawer.Draw(ctx, tree);

            EditorGUILayout.Space(6);
            DrawPointGrants(ctx, tree);
        }

        // 技能点获取条目列表：点数 + 获取方式 + 内联获取条件；延迟应用增删。
        private static void DrawPointGrants(IChronicleEditorContext ctx, SkillTree tree)
        {
            var list = tree.pointGrants;
            EditorGUILayout.LabelField("技能点获取", ToolkitEditorStyles.Header);
            if (list == null) return;

            int treeIdx  = ctx.Database.SkillTrees.IndexOf(tree);
            int removeAt = -1;
            for (int j = 0; j < list.Count; j++)
            {
                var g = list[j];
                if (g == null) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"获取 {j + 1}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) removeAt = j;
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                int pts  = EditorGUILayout.IntField("点数", g.points);
                var mode = (ESkillPointGrantMode)EditorGUILayout.EnumPopup("获取方式", g.mode);
                if (EditorGUI.EndChangeCheck())
                {
                    ctx.RecordUndo("修改技能点获取");
                    g.points = pts;
                    g.mode   = mode;
                    ctx.MarkDirty();
                }

                if (treeIdx >= 0)
                {
                    int jj = j;
                    ChronicleEditorFields.InlineConditionAt(ctx, "获取条件", so =>
                    {
                        var arr = so.FindProperty("skillTrees");
                        if (arr == null || treeIdx >= arr.arraySize) return null;
                        var gArr = arr.GetArrayElementAtIndex(treeIdx).FindPropertyRelative("pointGrants");
                        if (gArr == null || jj >= gArr.arraySize) return null;
                        return gArr.GetArrayElementAtIndex(jj).FindPropertyRelative("condition");
                    });
                }

                EditorGUILayout.EndVertical();
            }

            if (removeAt >= 0)
            {
                ctx.RecordUndo("删除技能点获取");
                list.RemoveAt(removeAt);
                ctx.MarkDirty();
            }

            if (GUILayout.Button("+ 添加获取条目"))
            {
                ctx.RecordUndo("添加技能点获取");
                list.Add(new SkillPointGrant());
                ctx.MarkDirty();
            }
        }
    }

    /// <summary>技能树结构编辑器（按 <see cref="ESkillTreeKind"/> 分支）：列表 / 层级（复用阶级序列延迟应用）/
    /// 树状（复用转职树缩进 + 前置防环）。技能与层级均可内联配解锁条件。</summary>
    public static class SkillTreeDrawer
    {
        private static readonly HashSet<string> Collapsed = new HashSet<string>();

        public static void Draw(IChronicleEditorContext ctx, SkillTree tree)
        {
            switch (tree.kind)
            {
                case ESkillTreeKind.List:   DrawList(ctx, tree);   break;
                case ESkillTreeKind.Tiered: DrawTiered(ctx, tree); break;
                case ESkillTreeKind.Tree:   DrawTree(ctx, tree);   break;
            }
        }

        // ── 列表 ────────────────────────────────────────────────────────────────────
        private static void DrawList(IChronicleEditorContext ctx, SkillTree tree)
        {
            var db = ctx.Database;
            int treeIdx = db.SkillTrees.IndexOf(tree);
            EditorGUILayout.LabelField("技能列表（无层级 / 先后；每技能可配解锁条件）", ToolkitEditorStyles.Header);

            var skills = tree.skills;
            if (skills.Count == 0)
                EditorGUILayout.LabelField("（空；用下方按钮添加技能）", ToolkitEditorStyles.Placeholder);

            int removeAt = -1;
            for (int i = 0; i < skills.Count; i++)
            {
                var e = skills[i];
                if (e == null) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                var sk = db.GetSkill(e.skillRef);
                EditorGUILayout.LabelField(SkillLabel(sk, e.skillRef), sk != null ? EditorStyles.label : ToolkitEditorStyles.StatusError);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("✕", GUILayout.Width(24))) removeAt = i;
                EditorGUILayout.EndHorizontal();
                SkillUnlockCondition(ctx, treeIdx, i);
                EditorGUILayout.EndVertical();
            }

            if (removeAt >= 0) { ctx.RecordUndo("移除技能"); skills.RemoveAt(removeAt); ctx.MarkDirty(); }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ 添加技能", GUILayout.Width(110)))
                ShowAddSkillMenu(ctx, tree, null);
        }

        // ── 层级 ────────────────────────────────────────────────────────────────────
        private static void DrawTiered(IChronicleEditorContext ctx, SkillTree tree)
        {
            var db = ctx.Database;
            int treeIdx = db.SkillTrees.IndexOf(tree);
            EditorGUILayout.LabelField("层级（低 → 高；层级与技能均可配解锁条件）", ToolkitEditorStyles.Header);

            var tiers = tree.tiers;
            if (tiers.Count == 0)
                EditorGUILayout.LabelField("（无层级；用下方按钮添加层级）", ToolkitEditorStyles.Placeholder);

            int moveFrom = -1, moveTo = -1, removeTierAt = -1;
            for (int ti = 0; ti < tiers.Count; ti++)
            {
                var tier = tiers[ti];
                if (tier == null) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"层 {ti + 1}", EditorStyles.miniBoldLabel, GUILayout.Width(48));
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(ti == 0))
                    if (GUILayout.Button("▲", GUILayout.Width(24))) { moveFrom = ti; moveTo = ti - 1; }
                using (new EditorGUI.DisabledScope(ti == tiers.Count - 1))
                    if (GUILayout.Button("▼", GUILayout.Width(24))) { moveFrom = ti; moveTo = ti + 1; }
                if (GUILayout.Button("✕", GUILayout.Width(24))) removeTierAt = ti;
                EditorGUILayout.EndHorizontal();

                AttributeFieldDrawer.Draw(ctx, "层名", tier.displayName, null);
                TierUnlockCondition(ctx, treeIdx, ti);

                EditorGUILayout.LabelField("本层技能", EditorStyles.miniBoldLabel);
                int removeSkillAt = -1;
                for (int i = 0; i < tree.skills.Count; i++)
                {
                    var e = tree.skills[i];
                    if (e == null || e.tierKey != tier.key) continue;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    var sk = db.GetSkill(e.skillRef);
                    EditorGUILayout.LabelField(SkillLabel(sk, e.skillRef), sk != null ? EditorStyles.label : ToolkitEditorStyles.StatusError);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("✕", GUILayout.Width(24))) removeSkillAt = i;
                    EditorGUILayout.EndHorizontal();
                    SkillUnlockCondition(ctx, treeIdx, i);
                    EditorGUILayout.EndVertical();
                }
                if (removeSkillAt >= 0) { ctx.RecordUndo("移除层内技能"); tree.skills.RemoveAt(removeSkillAt); ctx.MarkDirty(); }

                string tierKeyCaptured = tier.key;
                if (GUILayout.Button("+ 该层技能", GUILayout.Width(110)))
                    ShowAddSkillMenu(ctx, tree, tierKeyCaptured);

                EditorGUILayout.EndVertical();
            }

            if (removeTierAt >= 0)
            {
                ctx.RecordUndo("移除层级");
                string key = tiers[removeTierAt].key;
                tiers.RemoveAt(removeTierAt);
                foreach (var e in tree.skills) if (e != null && e.tierKey == key) e.tierKey = null;  // 孤儿技能脱离层级
                ctx.MarkDirty();
            }
            else if (moveFrom >= 0 && moveTo >= 0 && moveTo < tiers.Count)
            {
                ctx.RecordUndo("调整层级顺序");
                (tiers[moveFrom], tiers[moveTo]) = (tiers[moveTo], tiers[moveFrom]);
                ctx.MarkDirty();
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ 添加层级", GUILayout.Width(110)))
            {
                ctx.RecordUndo("添加层级");
                tree.tiers.Add(new SkillTreeTier { key = NewTierKey(tree) });
                ctx.MarkDirty();
            }
        }

        // ── 树状（前置技能 → 后继）────────────────────────────────────────────────────
        private static void DrawTree(IChronicleEditorContext ctx, SkillTree tree)
        {
            var db = ctx.Database;
            int treeIdx = db.SkillTrees.IndexOf(tree);
            EditorGUILayout.LabelField("技能树结构（前置 → 后继；加前置带防环）", ToolkitEditorStyles.Header);

            // 反向 children 映射：某技能 → 以它为前置的技能集合。
            var childrenOf = new Dictionary<string, List<string>>();
            foreach (var e in tree.skills)
            {
                if (e == null || e.prerequisiteSkillRefs == null) continue;
                foreach (var pre in e.prerequisiteSkillRefs)
                {
                    if (string.IsNullOrEmpty(pre)) continue;
                    if (!childrenOf.TryGetValue(pre, out var kids)) { kids = new List<string>(); childrenOf[pre] = kids; }
                    kids.Add(e.skillRef);
                }
            }

            var roots = new List<string>(tree.Roots());
            if (roots.Count == 0)
                EditorGUILayout.LabelField("（空树；用下方按钮添加根技能）", ToolkitEditorStyles.Placeholder);

            var visiting = new HashSet<string>();
            foreach (var root in roots)
                DrawTreeNode(ctx, db, tree, treeIdx, childrenOf, root, 0, visiting);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ 添加根技能", GUILayout.Width(110)))
                ShowAddSkillMenu(ctx, tree, null);
        }

        private static void DrawTreeNode(IChronicleEditorContext ctx, ChronicleDatabase db, SkillTree tree, int treeIdx,
            Dictionary<string, List<string>> childrenOf, string skillRef, int depth, HashSet<string> visiting)
        {
            if (!visiting.Add(skillRef))
            {
                EditorGUILayout.LabelField($"↻ {skillRef}（环，已跳过）", ToolkitEditorStyles.StatusError);
                return;
            }

            var sk = db.GetSkill(skillRef);
            string name = SkillLabel(sk, skillRef);
            childrenOf.TryGetValue(skillRef, out var kids);
            bool hasChildren = kids != null && kids.Count > 0;
            string key = tree.id + ":" + skillRef;
            int si = FindEntryIndex(tree, skillRef);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(depth * 14);
            if (hasChildren)
            {
                bool expanded = !Collapsed.Contains(key);
                bool ne = EditorGUILayout.Foldout(expanded, name, true);
                if (ne != expanded) { if (ne) Collapsed.Remove(key); else Collapsed.Add(key); }
            }
            else EditorGUILayout.LabelField("• " + name);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+后继", GUILayout.Width(48))) ShowAddChildMenu(ctx, tree, skillRef);
            if (GUILayout.Button("✕", GUILayout.Width(24)))
            {
                DetachSkill(ctx, tree, skillRef);
                EditorGUILayout.EndHorizontal();
                visiting.Remove(skillRef);
                return;
            }
            EditorGUILayout.EndHorizontal();

            if (si >= 0) SkillUnlockCondition(ctx, treeIdx, si);

            if (hasChildren && !Collapsed.Contains(key))
                foreach (var child in new List<string>(kids))
                    DrawTreeNode(ctx, db, tree, treeIdx, childrenOf, child, depth + 1, visiting);

            visiting.Remove(skillRef);
        }

        // ── 共用 ────────────────────────────────────────────────────────────────────
        private static string SkillLabel(Skill sk, string skillRef)
        {
            if (sk == null) return (string.IsNullOrEmpty(skillRef) ? "(未指定)" : skillRef) + "（缺失）";
            string nm = sk.displayText != null ? sk.displayText.GetTextValue() : null;
            return (string.IsNullOrEmpty(nm) ? sk.id : nm) + " (" + sk.id + ")";
        }

        private static int FindEntryIndex(SkillTree tree, string skillRef)
        {
            for (int i = 0; i < tree.skills.Count; i++)
                if (tree.skills[i] != null && tree.skills[i].skillRef == skillRef) return i;
            return -1;
        }

        private static string NewTierKey(SkillTree tree)
        {
            string key;
            do { key = "tier_" + System.Guid.NewGuid().ToString("N").Substring(0, 8); }
            while (TierKeyExists(tree, key));
            return key;
        }

        private static bool TierKeyExists(SkillTree tree, string key)
        {
            foreach (var t in tree.tiers) if (t != null && t.key == key) return true;
            return false;
        }

        private static void SkillUnlockCondition(IChronicleEditorContext ctx, int treeIdx, int skillIdx)
        {
            if (treeIdx < 0) return;
            int ti = treeIdx, si = skillIdx;
            ChronicleEditorFields.InlineConditionAt(ctx, "解锁条件", so =>
            {
                var arr = so.FindProperty("skillTrees");
                if (arr == null || ti >= arr.arraySize) return null;
                var sArr = arr.GetArrayElementAtIndex(ti).FindPropertyRelative("skills");
                if (sArr == null || si >= sArr.arraySize) return null;
                return sArr.GetArrayElementAtIndex(si).FindPropertyRelative("unlockCondition");
            });
        }

        private static void TierUnlockCondition(IChronicleEditorContext ctx, int treeIdx, int tierIdx)
        {
            if (treeIdx < 0) return;
            int ti = treeIdx, ki = tierIdx;
            ChronicleEditorFields.InlineConditionAt(ctx, "层级解锁条件", so =>
            {
                var arr = so.FindProperty("skillTrees");
                if (arr == null || ti >= arr.arraySize) return null;
                var tArr = arr.GetArrayElementAtIndex(ti).FindPropertyRelative("tiers");
                if (tArr == null || ki >= tArr.arraySize) return null;
                return tArr.GetArrayElementAtIndex(ki).FindPropertyRelative("unlockCondition");
            });
        }

        private static void ShowAddSkillMenu(IChronicleEditorContext ctx, SkillTree tree, string tierKey)
        {
            var db = ctx.Database;
            var menu = new GenericMenu();
            bool any = false;
            foreach (var s in db.Skills)
            {
                if (s == null || string.IsNullOrEmpty(s.id) || tree.FindEntry(s.id) != null) continue;
                any = true;
                string captured = s.id;
                menu.AddItem(new GUIContent(SkillLabel(s, s.id)), false, () =>
                {
                    ctx.RecordUndo("添加技能到技能树");
                    tree.skills.Add(new SkillTreeEntry { skillRef = captured, tierKey = tierKey });
                    ctx.MarkDirty();
                    ctx.Repaint();
                });
            }
            if (!any) menu.AddDisabledItem(new GUIContent("（无可添加的技能）"));
            menu.ShowAsContext();
        }

        private static void ShowAddChildMenu(IChronicleEditorContext ctx, SkillTree tree, string parentRef)
        {
            var db = ctx.Database;
            var menu = new GenericMenu();
            bool any = false;
            foreach (var s in db.Skills)
            {
                if (s == null || string.IsNullOrEmpty(s.id)) continue;
                string sid = s.id;
                if (sid == parentRef) continue;
                var existing = tree.FindEntry(sid);
                if (existing != null && existing.prerequisiteSkillRefs.Contains(parentRef)) continue;  // 已是其前置
                if (WouldCycle(tree, parentRef, sid)) continue;                                          // 会成环
                any = true;
                string captured = sid;
                menu.AddItem(new GUIContent(SkillLabel(s, sid)), false, () =>
                {
                    ctx.RecordUndo("添加后继技能");
                    var e = tree.FindEntry(captured);
                    if (e == null) { e = new SkillTreeEntry { skillRef = captured }; tree.skills.Add(e); }
                    if (!e.prerequisiteSkillRefs.Contains(parentRef)) e.prerequisiteSkillRefs.Add(parentRef);
                    ctx.MarkDirty();
                    ctx.Repaint();
                });
            }
            if (!any) menu.AddDisabledItem(new GUIContent("（无可添加的技能）"));
            menu.ShowAsContext();
        }

        private static void DetachSkill(IChronicleEditorContext ctx, SkillTree tree, string skillRef)
        {
            ctx.RecordUndo("从技能树移除技能");
            tree.skills.RemoveAll(e => e != null && e.skillRef == skillRef);
            foreach (var e in tree.skills)
                e?.prerequisiteSkillRefs.RemoveAll(p => p == skillRef);
            ctx.MarkDirty();
        }

        // 加边 parent→child（child 以 parent 为前置）是否成环：parent 是否已（间接）以 child 为前置。
        private static bool WouldCycle(SkillTree tree, string parentRef, string childRef)
        {
            if (parentRef == childRef) return true;
            var stack = new Stack<string>();
            var seen  = new HashSet<string>();
            stack.Push(parentRef);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur == childRef) return true;
                if (!seen.Add(cur)) continue;
                var n = tree.FindEntry(cur);
                if (n != null && n.prerequisiteSkillRefs != null)
                    foreach (var pre in n.prerequisiteSkillRefs) stack.Push(pre);
            }
            return false;
        }
    }
}

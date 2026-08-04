using System.Collections.Generic;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 「职业」页签（三列）：左=转职树列表（选中后右列渲染缩进树结构编辑器）、中=职业列表（按分组过滤 / 搜索 / 添加）、
    /// 右=职业 Inspector（选中职业时）或转职树 Inspector（选中左列转职树时）。
    /// 职业无独立模板：中列以统一 <see cref="ChronicleDatabase.GroupTags"/> 池作过滤维（<c>TTemplate=ChronicleGroupTag</c>）。
    /// </summary>
    public sealed class ProfessionSystemTab : EditorThreeColumnTab<ProfessionDefinition>
    {
        private readonly ProfessionTreeListPanel _treePanel = new ProfessionTreeListPanel();
        private readonly ProfessionListPanel     _listPanel = new ProfessionListPanel();
        private IEditorMasterListPanel<ChronicleDatabase>[] _leftPanels;

        protected override string[] LeftSubTabs => new[] { "转职树" };

        protected override IEditorMasterListPanel<ChronicleDatabase>[] LeftPanels
            => _leftPanels ??= new IEditorMasterListPanel<ChronicleDatabase>[] { _treePanel };

        protected override string EntityNoun => "职业";

        protected override List<ProfessionDefinition> EntityList(ChronicleDatabase db) => db.Professions;

        protected override ProfessionDefinition DrawEntityList(IChronicleEditorContext ctx, ProfessionDefinition displaySelected)
            => _listPanel.DrawList(ctx, displaySelected);

        protected override ProfessionDefinition ConsumePendingSelect() => _listPanel.ConsumePendingSelect();

        protected override void DrawEntityInspector(IChronicleEditorContext ctx, ProfessionDefinition entity)
            => ProfessionInspectorPanel.Draw(ctx, entity);
    }

    /// <summary>职业列表面板（中列）：按分组标签过滤 + 搜索 + 添加，每行 id / 名称 / 上限 / 成长数。</summary>
    public sealed class ProfessionListPanel : EditorEntityListPanel<ProfessionDefinition, ChronicleGroupTag>
    {
        public ProfessionListPanel() : base("ChronicleProfessionListDrag") { }

        protected override EChronicleEntityKind Kind => EChronicleEntityKind.Profession;
        protected override string Noun => "职业";

        protected override List<ProfessionDefinition> Entities(ChronicleDatabase db)  => db.Professions;
        protected override List<ChronicleGroupTag>    Templates(ChronicleDatabase db) => db.GroupTags;   // 分组作过滤维
        protected override string TemplateName(ChronicleGroupTag t) => t.id;                             // 过滤键 = 分组 id（与 groupTagRef 一致）
        protected override string TemplateRefOf(ProfessionDefinition e) => e.groupTagRef;
        protected override string IdOf(ProfessionDefinition e) => e.id;

        protected override Color RowDotColor(ChronicleDatabase db, ProfessionDefinition e)
        {
            var g = db.GetGroupTag(e.groupTagRef);
            return g != null ? g.color : Color.gray;
        }

        protected override string SearchName(ChronicleDatabase db, ProfessionDefinition e)
            => e.displayName != null ? e.displayName.GetTextValue() : null;

        protected override ProfessionDefinition AddFromTemplate(IChronicleEditorContext ctx, string templateName)
        {
            var db = ctx.Database;
            ctx.RecordUndo("添加职业");
            var prof = new ProfessionDefinition(GenerateId(db, "prof_", id => db.GetProfession(id) != null))
            {
                groupTagRef = templateName,   // templateName = 选中的分组 id（「全部」时为 null）
            };
            db.Professions.Add(prof);
            ctx.MarkDirty();
            return prof;
        }

        protected override ProfessionDefinition QuickAdd(IChronicleEditorContext ctx)
        {
            var db = ctx.Database;
            if (db.Professions.Count == 0) return AddFromTemplate(ctx, null);

            ctx.RecordUndo("快速添加职业");
            var clone = db.Professions[db.Professions.Count - 1].Clone();
            clone.id = GenerateId(db, "prof_", id => db.GetProfession(id) != null);
            db.Professions.Add(clone);
            ctx.MarkDirty();
            return clone;
        }

        protected override void DrawRowColumns(ChronicleDatabase db, ProfessionDefinition e,
            Rect keyRow, float contentX, float contentRight, float valY, float valH)
        {
            float w     = Mathf.Max(0f, contentRight - contentX);
            float idW   = Mathf.Min(90f, w * 0.26f);
            float lvW   = Mathf.Min(60f, w * 0.16f);
            float grW   = Mathf.Min(60f, w * 0.16f);
            float nameX = contentX + idW + Pad;
            float grX   = contentRight - grW;
            float lvX   = grX - Pad - lvW;
            float nameW = Mathf.Max(0f, lvX - Pad - nameX);

            GUI.Label(new Rect(contentX, keyRow.y, idW,   keyRow.height), "ID",   KeyStyle);
            GUI.Label(new Rect(nameX,    keyRow.y, nameW, keyRow.height), "名称", KeyStyle);
            GUI.Label(new Rect(lvX,      keyRow.y, lvW,   keyRow.height), "上限", KeyStyle);
            GUI.Label(new Rect(grX,      keyRow.y, grW,   keyRow.height), "成长", KeyStyle);

            GUI.Label(new Rect(contentX, valY, idW, valH), string.IsNullOrEmpty(e.id) ? "(空 ID)" : e.id, IdStyle);
            string name = e.displayName != null ? e.displayName.GetTextValue() : null;
            GUI.Label(new Rect(nameX, valY, nameW, valH), string.IsNullOrEmpty(name) ? "—" : name, SubStyle);
            GUI.Label(new Rect(lvX, valY, lvW, valH), e.maxLevel.ToString(), SubStyle);
            GUI.Label(new Rect(grX, valY, grW, valH), (e.growth?.Count ?? 0).ToString(), SubStyle);
        }
    }

    /// <summary>转职树主列表面板（职业页左列）：绑定 <see cref="ChronicleDatabase.ProfessionTrees"/>；选中后右列渲染缩进树编辑器。</summary>
    public sealed class ProfessionTreeListPanel : EditorMasterListPanel<ProfessionTree>
    {
        protected override List<ProfessionTree> GetList(ChronicleDatabase db) => db.ProfessionTrees;
        protected override string Noun => "转职树";

        protected override string RowLabel(ProfessionTree item)
        {
            string name = item.displayName != null ? item.displayName.GetTextValue() : null;
            return !string.IsNullOrEmpty(name) ? name : (string.IsNullOrEmpty(item.id) ? "(未命名)" : item.id);
        }

        protected override ProfessionTree CreateNew(ChronicleDatabase db, List<ProfessionTree> list)
        {
            int n = list.Count + 1;
            string id;
            do { id = "prof_tree_" + n; n++; } while (Exists(list, id));
            return new ProfessionTree(id);
        }

        private static bool Exists(List<ProfessionTree> list, string id)
        {
            foreach (var t in list) if (t != null && t.id == id) return true;
            return false;
        }

        public override void DrawInspector(IChronicleEditorContext ctx, ProfessionTree tree)
        {
            if (tree == null)
            {
                EditorGUILayout.LabelField("请选择或新建一棵转职树。", ToolkitEditorStyles.Placeholder);
                return;
            }

            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);
            ChronicleEntityHeader.DrawIdField(ctx, "转职树", tree.id,
                ctx.DuplicateIdsOf(EChronicleEntityKind.ProfessionTree), v => tree.id = v);
            AttributeFieldDrawer.Draw(ctx, "显示名", tree.displayName, null);

            EditorGUILayout.Space(6);
            ProfessionTreeDrawer.Draw(ctx, tree);
        }
    }

    /// <summary>职业实例检视器（职业页右列）：ID / 显示信息 / 分组 / 等级上限 / 经验曲线 / 每级成长 / 等级解锁 / 从业条件。</summary>
    public static class ProfessionInspectorPanel
    {
        public static void Draw(IChronicleEditorContext ctx, ProfessionDefinition prof)
        {
            if (prof == null)
            {
                EditorGUILayout.LabelField("请选择或新建一个职业。", ToolkitEditorStyles.Placeholder);
                return;
            }

            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);
            ChronicleEntityHeader.DrawIdField(ctx, "职业", prof.id,
                ctx.DuplicateIdsOf(EChronicleEntityKind.Profession), v => prof.id = v);
            AttributeFieldDrawer.Draw(ctx, "显示名", prof.displayName, null);
            AttributeFieldDrawer.Draw(ctx, "说明",   prof.description, null);
            AttributeFieldDrawer.Draw(ctx, "图标",   prof.icon, null);
            DrawGroupPopup(ctx, prof);

            EditorGUI.BeginChangeCheck();
            int maxLevel = EditorGUILayout.IntField("等级上限", prof.maxLevel);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改等级上限");
                prof.maxLevel = Mathf.Max(1, maxLevel);
                ctx.MarkDirty();
            }

            EditorGUILayout.Space(6);
            DrawExpCurve(ctx, prof);

            EditorGUILayout.Space(6);
            DrawGrowthList(ctx, prof);

            EditorGUILayout.Space(6);
            DrawUnlockList(ctx, prof);

            EditorGUILayout.Space(6);
            DrawRequirements(ctx, prof);
        }

        // ── 分组 ────────────────────────────────────────────────────────────────

        private static void DrawGroupPopup(IChronicleEditorContext ctx, ProfessionDefinition prof)
        {
            var db = ctx.Database;
            if (db.GroupTags.Count == 0)
            {
                EditorGUILayout.LabelField("分组", "（无分组标签；在「通用 → 分组标签」添加）");
                return;
            }
            var ids    = new List<string> { "" };
            var labels = new List<string> { "（无）" };
            foreach (var g in db.GroupTags)
                if (g != null && !string.IsNullOrEmpty(g.id)) { ids.Add(g.id); labels.Add(g.PlainName()); }

            int idx = Mathf.Max(0, ids.IndexOf(prof.groupTagRef ?? ""));
            EditorGUI.BeginChangeCheck();
            int newIdx = EditorGUILayout.Popup("分组", idx, labels.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改职业分组");
                prof.groupTagRef = newIdx <= 0 ? null : ids[newIdx];
                ctx.MarkDirty();
            }
        }

        // ── 经验曲线 ──────────────────────────────────────────────────────────────

        private static void DrawExpCurve(IChronicleEditorContext ctx, ProfessionDefinition prof)
        {
            var c = prof.expCurve;
            EditorGUILayout.LabelField("经验曲线", ToolkitEditorStyles.Header);

            EditorGUI.BeginChangeCheck();
            var mode = (EExpCurveMode)EditorGUILayout.EnumPopup("模式", c.mode);
            if (EditorGUI.EndChangeCheck()) { ctx.RecordUndo("修改经验曲线模式"); c.mode = mode; ctx.MarkDirty(); }

            switch (c.mode)
            {
                case EExpCurveMode.Formula:
                    EditorGUI.BeginChangeCheck();
                    float baseExp  = EditorGUILayout.FloatField("基础经验", c.baseExp);
                    float exponent = EditorGUILayout.FloatField("指数",     c.exponent);
                    float linear   = EditorGUILayout.FloatField("线性系数", c.linear);
                    if (EditorGUI.EndChangeCheck())
                    {
                        ctx.RecordUndo("修改经验曲线");
                        c.baseExp = baseExp; c.exponent = exponent; c.linear = linear;
                        ctx.MarkDirty();
                    }
                    break;

                case EExpCurveMode.Table:
                    for (int i = 0; i < c.perLevelExp.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUI.BeginChangeCheck();
                        int v = EditorGUILayout.IntField($"Lv{i + 1}→{i + 2} 所需", c.perLevelExp[i]);
                        if (EditorGUI.EndChangeCheck()) { ctx.RecordUndo("修改每级经验"); c.perLevelExp[i] = v; ctx.MarkDirty(); }
                        if (GUILayout.Button("✕", GUILayout.Width(22)))
                        {
                            ctx.RecordUndo("删除每级经验"); c.perLevelExp.RemoveAt(i); ctx.MarkDirty();
                            EditorGUILayout.EndHorizontal();
                            break;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    if (GUILayout.Button("+ 添加一级", GUILayout.Width(90)))
                    {
                        ctx.RecordUndo("添加每级经验"); c.perLevelExp.Add(100); ctx.MarkDirty();
                    }
                    break;

                case EExpCurveMode.Curve:
                    AttributeFieldDrawer.Draw(ctx, "曲线 (level→expToNext)", c.curveValue, null);
                    EditorGUI.BeginChangeCheck();
                    float scale = EditorGUILayout.FloatField("曲线缩放", c.curveScale);
                    if (EditorGUI.EndChangeCheck()) { ctx.RecordUndo("修改曲线缩放"); c.curveScale = scale; ctx.MarkDirty(); }
                    break;
            }

            DrawSparkline(prof);
        }

        // 预览折线：ExpToNext(1..maxLevel-1)。
        private static void DrawSparkline(ProfessionDefinition prof)
        {
            var c = prof.expCurve;
            int maxLv = Mathf.Clamp(prof.maxLevel, 2, 60);
            int n = maxLv - 1;
            if (n < 1) return;

            var rect = GUILayoutUtility.GetRect(0, 52, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 0.25f));
            EditorGUI.LabelField(new Rect(rect.x + 4, rect.y + 2, rect.width - 8, 16),
                "每级所需经验预览", EditorStyles.miniLabel);
            if (Event.current.type != EventType.Repaint) return;

            var vals = new float[n];
            float maxVal = 1f;
            for (int i = 0; i < n; i++) { vals[i] = c.ExpToNext(i + 1); if (vals[i] > maxVal) maxVal = vals[i]; }

            const float pad = 6f;
            float top = rect.y + 18f;
            float h   = rect.yMax - pad - top;
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float x = rect.x + pad + (rect.width - 2 * pad) * (n == 1 ? 0.5f : (float)i / (n - 1));
                float y = rect.yMax - pad - h * (vals[i] / maxVal);
                pts[i] = new Vector3(x, y, 0f);
            }
            Handles.color = new Color(0.4f, 0.7f, 1f);
            if (n >= 2) Handles.DrawAAPolyLine(2f, pts);
            else EditorGUI.DrawRect(new Rect(pts[0].x - 1, pts[0].y - 1, 3, 3), Color.cyan);
            Handles.color = Color.white;
        }

        // ── 每级成长 ──────────────────────────────────────────────────────────────

        private static void DrawGrowthList(IChronicleEditorContext ctx, ProfessionDefinition prof)
        {
            EditorGUILayout.LabelField("每级成长（汇入核心属性 · prof:{id}:growth）", ToolkitEditorStyles.Header);
            var db = ctx.Database;
            for (int i = 0; i < prof.growth.Count; i++)
            {
                var g = prof.growth[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                string attrId = CoreAttrPopup("目标属性", g.coreAttrId, db);
                if (EditorGUI.EndChangeCheck()) { ctx.RecordUndo("修改成长目标"); g.coreAttrId = attrId; ctx.MarkDirty(); }
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    ctx.RecordUndo("删除成长条目"); prof.growth.RemoveAt(i); ctx.MarkDirty();
                    EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                bool useCurve = EditorGUILayout.Toggle("用曲线", g.useCurve);
                if (EditorGUI.EndChangeCheck()) { ctx.RecordUndo("修改成长方式"); g.useCurve = useCurve; ctx.MarkDirty(); }

                if (g.useCurve)
                {
                    AttributeFieldDrawer.Draw(ctx, "成长曲线 (level→累计)", g.levelCurve, null);
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    float perLevel = EditorGUILayout.FloatField("每级增量", g.perLevel);
                    if (EditorGUI.EndChangeCheck()) { ctx.RecordUndo("修改每级增量"); g.perLevel = perLevel; ctx.MarkDirty(); }
                }
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("+ 添加成长", GUILayout.Width(90)))
            {
                ctx.RecordUndo("添加成长条目"); prof.growth.Add(new LevelGrowthEntry()); ctx.MarkDirty();
            }
        }

        // ── 等级解锁 ──────────────────────────────────────────────────────────────

        private static void DrawUnlockList(IChronicleEditorContext ctx, ProfessionDefinition prof)
        {
            EditorGUILayout.LabelField("等级解锁", ToolkitEditorStyles.Header);
            for (int i = 0; i < prof.unlocks.Count; i++)
            {
                var u = prof.unlocks[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                int lvl = EditorGUILayout.IntField("等级", u.level);
                if (EditorGUI.EndChangeCheck()) { ctx.RecordUndo("修改解锁等级"); u.level = lvl; ctx.MarkDirty(); }
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    ctx.RecordUndo("删除解锁条目"); prof.unlocks.RemoveAt(i); ctx.MarkDirty();
                    EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                DrawStringList(ctx, "授予特质 id", u.grantTraitRefs, "修改解锁特质");
                DrawStringList(ctx, "授予头衔 id", u.grantTitleRefs, "修改解锁头衔");
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("+ 添加解锁", GUILayout.Width(90)))
            {
                ctx.RecordUndo("添加解锁条目"); prof.unlocks.Add(new LevelUnlock()); ctx.MarkDirty();
            }
        }

        // ── 从业 / 转职条件（内联 ConditionExpression）──────────────────────────────

        private static void DrawRequirements(IChronicleEditorContext ctx, ProfessionDefinition prof)
        {
            EditorGUILayout.LabelField("从业 / 转职条件", ToolkitEditorStyles.Header);
            var so = ctx.Serialized;
            if (so == null || ctx.Database == null)
            {
                EditorGUILayout.HelpBox("条件编辑暂不可用（无序列化对象）。", MessageType.None);
                return;
            }
            so.Update();
            var arr = so.FindProperty("professions");
            int idx = ctx.Database.Professions.IndexOf(prof);
            if (arr == null || idx < 0 || idx >= arr.arraySize)
            {
                EditorGUILayout.HelpBox("条件编辑暂不可用。", MessageType.None);
                return;
            }
            var reqProp = arr.GetArrayElementAtIndex(idx).FindPropertyRelative("requirements");
            if (reqProp == null) return;
            EditorGUILayout.PropertyField(reqProp, new GUIContent("条件"), true);
            so.ApplyModifiedProperties();
        }

        // ── 小工具 ────────────────────────────────────────────────────────────────

        private static string CoreAttrPopup(string label, string current, ChronicleDatabase db)
        {
            var ids = new List<string>();
            if (db != null)
                foreach (var a in db.CoreAttributes)
                    if (a != null && !string.IsNullOrEmpty(a.id) && !ids.Contains(a.id)) ids.Add(a.id);

            if (ids.Count == 0) return EditorGUILayout.TextField(label, current);
            if (!string.IsNullOrEmpty(current) && !ids.Contains(current)) ids.Insert(0, current);
            int idx    = Mathf.Max(0, ids.IndexOf(current));
            int newIdx = EditorGUILayout.Popup(label, idx, ids.ToArray());
            return ids[newIdx];
        }

        private static void DrawStringList(IChronicleEditorContext ctx, string label, List<string> list, string undo)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            for (int i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                string v = EditorGUILayout.TextField(list[i]);
                if (EditorGUI.EndChangeCheck()) { ctx.RecordUndo(undo); list[i] = v; ctx.MarkDirty(); }
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    ctx.RecordUndo(undo); list.RemoveAt(i); ctx.MarkDirty();
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ 添加", GUILayout.Width(60)))
            {
                ctx.RecordUndo(undo); list.Add(""); ctx.MarkDirty();
            }
        }
    }

    /// <summary>转职树缩进结构编辑器：父→子 = 「可转职为」。折叠 / 加子 / 移除节点，加子带防环。</summary>
    public static class ProfessionTreeDrawer
    {
        private static readonly HashSet<string> Collapsed = new HashSet<string>();

        public static void Draw(IChronicleEditorContext ctx, ProfessionTree tree)
        {
            var db = ctx.Database;
            EditorGUILayout.LabelField("转职树结构（父 → 子 = 可转职为）", ToolkitEditorStyles.Header);

            var roots = new List<string>(tree.Roots());
            if (roots.Count == 0)
                EditorGUILayout.LabelField("（空树；用下方按钮添加根职业）", ToolkitEditorStyles.Placeholder);

            var visiting = new HashSet<string>();
            foreach (var root in roots)
                DrawNode(ctx, db, tree, root, 0, visiting);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ 添加根职业", GUILayout.Width(110)))
                ShowAddMenu(ctx, tree, null);
        }

        private static void DrawNode(IChronicleEditorContext ctx, ChronicleDatabase db, ProfessionTree tree,
            string professionRef, int depth, HashSet<string> visiting)
        {
            if (!visiting.Add(professionRef))
            {
                EditorGUILayout.LabelField($"{Indent(depth)}↻ {professionRef}（环，已跳过）", ToolkitEditorStyles.StatusError);
                return;
            }

            var node = tree.FindNode(professionRef);
            var prof = db.GetProfession(professionRef);
            string name = prof != null ? prof.PlainName() : professionRef + "（缺失）";
            bool hasChildren = node != null && node.childProfessionRefs.Count > 0;
            string key = tree.id + ":" + professionRef;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(depth * 14);
            if (hasChildren)
            {
                bool expanded = !Collapsed.Contains(key);
                bool ne = EditorGUILayout.Foldout(expanded, name, true);
                if (ne != expanded) { if (ne) Collapsed.Remove(key); else Collapsed.Add(key); }
            }
            else
            {
                EditorGUILayout.LabelField("• " + name);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+子", GUILayout.Width(34))) ShowAddMenu(ctx, tree, professionRef);
            if (GUILayout.Button("✕", GUILayout.Width(24)))
            {
                Detach(ctx, tree, professionRef);
                EditorGUILayout.EndHorizontal();
                visiting.Remove(professionRef);
                return;
            }
            EditorGUILayout.EndHorizontal();

            if (hasChildren && !Collapsed.Contains(key))
                foreach (var child in new List<string>(node.childProfessionRefs))
                    DrawNode(ctx, db, tree, child, depth + 1, visiting);

            visiting.Remove(professionRef);
        }

        private static string Indent(int depth) => depth <= 0 ? "" : new string(' ', depth * 2);

        private static void ShowAddMenu(IChronicleEditorContext ctx, ProfessionTree tree, string parentRef)
        {
            var db = ctx.Database;
            var menu = new GenericMenu();
            bool any = false;
            foreach (var p in db.Professions)
            {
                if (p == null || string.IsNullOrEmpty(p.id)) continue;
                string pid = p.id;
                if (parentRef == null)
                {
                    if (tree.FindNode(pid) != null) continue;   // 已是节点
                }
                else
                {
                    if (pid == parentRef) continue;
                    var pn = tree.FindNode(parentRef);
                    if (pn != null && pn.childProfessionRefs.Contains(pid)) continue;  // 已是子
                    if (WouldCycle(tree, parentRef, pid)) continue;                    // 会成环
                }
                any = true;
                string label = p.PlainName() + " (" + pid + ")";
                string captured = pid;
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    ctx.RecordUndo(parentRef == null ? "添加根职业" : "添加子职业");
                    EnsureNode(tree, captured);
                    if (parentRef != null)
                    {
                        EnsureNode(tree, parentRef);
                        var pn = tree.FindNode(parentRef);
                        if (!pn.childProfessionRefs.Contains(captured)) pn.childProfessionRefs.Add(captured);
                    }
                    ctx.MarkDirty();
                    ctx.Repaint();
                });
            }
            if (!any) menu.AddDisabledItem(new GUIContent("（无可添加的职业）"));
            menu.ShowAsContext();
        }

        private static void Detach(IChronicleEditorContext ctx, ProfessionTree tree, string professionRef)
        {
            ctx.RecordUndo("从转职树移除职业");
            tree.nodes.RemoveAll(n => n != null && n.professionRef == professionRef);
            foreach (var n in tree.nodes)
                n?.childProfessionRefs.RemoveAll(c => c == professionRef);
            ctx.MarkDirty();
        }

        private static void EnsureNode(ProfessionTree tree, string professionRef)
        {
            if (tree.FindNode(professionRef) == null)
                tree.nodes.Add(new ProfessionTreeNode { professionRef = professionRef });
        }

        // 加边 parent→child 是否成环：child 能否到达 parent（child 是 parent 的祖先 / 自身）。
        private static bool WouldCycle(ProfessionTree tree, string parentRef, string childRef)
        {
            if (parentRef == childRef) return true;
            var stack = new Stack<string>();
            var seen  = new HashSet<string>();
            stack.Push(childRef);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur == parentRef) return true;
                if (!seen.Add(cur)) continue;
                var n = tree.FindNode(cur);
                if (n != null)
                    foreach (var c in n.childProfessionRefs) stack.Push(c);
            }
            return false;
        }
    }
}

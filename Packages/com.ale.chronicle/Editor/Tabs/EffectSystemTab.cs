using System.Collections.Generic;
using Ale.Effect;
using Ale.GameplayTags;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 「效果」页签（三列）：左=本库声明的 Gameplay 标签目录、中=效果列表（按时长策略过滤 / 搜索 / 按策略新建 / 快速添加）、
    /// 右=效果 Inspector（基础信息 + 显示字段 + 内联 toolkit 效果定义 + 校验摘要）或选中左列标签时的标签 Inspector。
    /// 效果没有真正的「模板」：中列以三种时长策略充当伪模板（过滤页签 / 新建入口 / 行色点）。
    /// </summary>
    public sealed class EffectSystemTab : EditorThreeColumnTab<ChronicleEffect>
    {
        private readonly GameplayTagPanel _tagPanel  = new GameplayTagPanel();
        private readonly EffectListPanel  _listPanel = new EffectListPanel();
        private IEditorMasterListPanel<ChronicleDatabase>[] _leftPanels;

        protected override IEditorMasterListPanel<ChronicleDatabase>[] LeftPanels
            => _leftPanels ??= new IEditorMasterListPanel<ChronicleDatabase>[] { _tagPanel };

        protected override string EntityNoun => "效果";

        protected override List<ChronicleEffect> EntityList(ChronicleDatabase db) => db.Effects;

        protected override ChronicleEffect DrawEntityList(IChronicleEditorContext ctx, ChronicleEffect displaySelected)
            => _listPanel.DrawList(ctx, displaySelected);

        protected override ChronicleEffect ConsumePendingSelect() => _listPanel.ConsumePendingSelect();

        protected override void DrawEntityInspector(IChronicleEditorContext ctx, ChronicleEffect entity)
            => EffectInspectorPanel.Draw(ctx, entity);
    }

    /// <summary>效果「伪模板」：以时长策略充当中列的过滤页签 / 新建入口 / 行色点（效果本身没有模板机制）。</summary>
    public sealed class EffectPolicyTemplate
    {
        public readonly string          name;
        public readonly EDurationPolicy policy;
        public readonly Color           color;

        private EffectPolicyTemplate(string name, EDurationPolicy policy, Color color)
        {
            this.name   = name;
            this.policy = policy;
            this.color  = color;
        }

        public static readonly List<EffectPolicyTemplate> All = new List<EffectPolicyTemplate>
        {
            new EffectPolicyTemplate("瞬时", EDurationPolicy.Instant,     new Color(0.55f, 0.75f, 0.95f)),
            new EffectPolicyTemplate("持续", EDurationPolicy.HasDuration, new Color(0.55f, 0.85f, 0.55f)),
            new EffectPolicyTemplate("无限", EDurationPolicy.Infinite,    new Color(0.92f, 0.72f, 0.40f)),
        };

        public static EffectPolicyTemplate Of(EDurationPolicy policy)
        {
            foreach (var t in All) if (t.policy == policy) return t;
            return All[0];
        }

        public static EffectPolicyTemplate ByName(string name)
        {
            foreach (var t in All) if (t.name == name) return t;
            return null;
        }
    }

    /// <summary>效果列表面板（中列）：策略过滤 + 搜索 + 按策略新建 / 快速添加，每行 id / 名称 / 策略 / 内容摘要。</summary>
    public sealed class EffectListPanel : EditorEntityListPanel<ChronicleEffect, EffectPolicyTemplate>
    {
        public EffectListPanel() : base("ChronicleEffectListDrag") { }

        protected override EChronicleEntityKind Kind => EChronicleEntityKind.Effect;
        protected override string Noun => "效果";

        protected override List<ChronicleEffect>      Entities(ChronicleDatabase db)  => db.Effects;
        protected override List<EffectPolicyTemplate> Templates(ChronicleDatabase db) => EffectPolicyTemplate.All;
        protected override string TemplateName(EffectPolicyTemplate t) => t.name;
        protected override string TemplateRefOf(ChronicleEffect e) => PolicyOf(e).name;
        protected override string IdOf(ChronicleEffect e) => e.id;

        protected override Color RowDotColor(ChronicleDatabase db, ChronicleEffect e) => PolicyOf(e).color;

        protected override string SearchName(ChronicleDatabase db, ChronicleEffect e)
            => e.displayText != null ? e.displayText.GetTextValue() : null;

        private static EffectPolicyTemplate PolicyOf(ChronicleEffect e)
            => EffectPolicyTemplate.Of(e?.definition != null ? e.definition.durationPolicy : EDurationPolicy.Instant);

        protected override ChronicleEffect AddFromTemplate(IChronicleEditorContext ctx, string templateName)
        {
            var db = ctx.Database;
            var t  = EffectPolicyTemplate.ByName(templateName) ?? EffectPolicyTemplate.All[0];

            ctx.RecordUndo("添加效果");
            var e = new ChronicleEffect(GenerateId(db, "effect_", id => db.GetEffect(id) != null), t.policy);
            e.displayText.SetTextValue(0, "新效果");
            if (t.policy == EDurationPolicy.HasDuration)
                e.definition.duration = EffectMagnitude.Scalable(1f);   // HasDuration 要求时长 > 0
            e.Normalize();
            db.Effects.Add(e);
            ctx.MarkDirty();
            return e;
        }

        protected override ChronicleEffect QuickAdd(IChronicleEditorContext ctx)
        {
            var db = ctx.Database;
            if (db.Effects.Count == 0)
                return AddFromTemplate(ctx, EffectPolicyTemplate.All[0].name);

            ctx.RecordUndo("快速添加效果");
            var clone = db.Effects[db.Effects.Count - 1].Clone();
            clone.id = GenerateId(db, "effect_", id => db.GetEffect(id) != null);
            clone.Normalize();
            db.Effects.Add(clone);
            ctx.MarkDirty();
            return clone;
        }

        protected override void DrawRowColumns(ChronicleDatabase db, ChronicleEffect e,
            Rect keyRow, float contentX, float contentRight, float valY, float valH)
        {
            float w     = Mathf.Max(0f, contentRight - contentX);
            float idW   = Mathf.Min(100f, w * 0.28f);
            float polW  = 40f;
            float sumW  = Mathf.Min(120f, w * 0.30f);
            float nameX = contentX + idW + Pad;
            float sumX  = contentRight - sumW;
            float polX  = sumX - Pad - polW;
            float nameW = Mathf.Max(0f, polX - Pad - nameX);

            GUI.Label(new Rect(contentX, keyRow.y, idW,   keyRow.height), "ID",   KeyStyle);
            GUI.Label(new Rect(nameX,    keyRow.y, nameW, keyRow.height), "名称", KeyStyle);
            GUI.Label(new Rect(polX,     keyRow.y, polW,  keyRow.height), "策略", KeyStyle);
            GUI.Label(new Rect(sumX,     keyRow.y, sumW,  keyRow.height), "内容", KeyStyle);

            GUI.Label(new Rect(contentX, valY, idW, valH), string.IsNullOrEmpty(e.id) ? "(空 ID)" : e.id, IdStyle);

            string name = e.displayText != null ? e.displayText.GetTextValue() : null;
            GUI.Label(new Rect(nameX, valY, nameW, valH), string.IsNullOrEmpty(name) ? "—" : name, SubStyle);

            GUI.Label(new Rect(polX, valY, polW, valH), PolicyOf(e).name, SubStyle);
            GUI.Label(new Rect(sumX, valY, sumW, valH), Summary(e.definition), SubStyle);
        }

        private static string Summary(EffectDefinition d)
        {
            if (d == null) return "—";
            int mods = d.modifiers != null ? d.modifiers.Count : 0;
            int exec = 0;
            if (d.executions?.groups != null)
                foreach (var g in d.executions.groups)
                    if (g?.items != null) exec += g.items.Count;
            int tags = d.assetTags != null && d.assetTags.tags != null ? d.assetTags.tags.Count : 0;
            return $"{mods} 修饰 · {exec} 执行 · {tags} 标签";
        }
    }

    /// <summary>效果 Inspector（效果页右列）：ID / 名称 / 描述 / 图标 + 内联 toolkit 效果定义 + 校验摘要。</summary>
    public static class EffectInspectorPanel
    {
        public static void Draw(IChronicleEditorContext ctx, ChronicleEffect effect)
        {
            if (effect == null)
            {
                EditorGUILayout.LabelField("请选择或新建一个效果。", ToolkitEditorStyles.Placeholder);
                return;
            }

            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);
            ChronicleEntityHeader.DrawIdField(ctx, "效果", effect.id,
                ctx.DuplicateIdsOf(EChronicleEntityKind.Effect), v => effect.id = v);

            AttributeFieldDrawer.Draw(ctx, "名称", effect.displayText, null);
            AttributeFieldDrawer.Draw(ctx, "描述", effect.descriptionText, null);
            AttributeFieldDrawer.Draw(ctx, "图标", effect.iconValue, null);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("效果定义", ToolkitEditorStyles.Header);
            EditorGUILayout.LabelField("定义内的 id / 显示名由上方 ID / 名称同步；技能 / 道具以 ID 引用本效果。", EditorStyles.miniLabel);
            ChronicleEffectFields.InlineDefinition(ctx, effect);

            // 校验摘要（错误阻断导出；警告仅提示）
            var errors = new List<string>();
            var warns  = new List<string>();
            ChronicleEffectFields.ValidateForDisplay(effect, errors, warns);
            if (errors.Count > 0 || warns.Count > 0)
            {
                EditorGUILayout.Space(4);
                foreach (var m in errors) EditorGUILayout.HelpBox(m, MessageType.Error);
                foreach (var m in warns)  EditorGUILayout.HelpBox(m, MessageType.Warning);
            }
        }
    }

    /// <summary>
    /// Gameplay 标签面板（效果页左列）：绑定 <see cref="ChronicleDatabase.GameplayTags"/>；检视 名称（点分层级，非法红框）/ 注释，
    /// 并列出会被隐式登记的祖先标签。注册数据库时并入 toolkit 标签注册表，供标签字段下拉与校验；运行时匹配不依赖它。
    /// </summary>
    public sealed class GameplayTagPanel : EditorMasterListPanel<GameplayTagDefinition>
    {
        protected override List<GameplayTagDefinition> GetList(ChronicleDatabase db) => db.GameplayTags;
        protected override string Noun => "Gameplay 标签";

        protected override string HeaderHelp
            => "本库声明的层级标签（如 Status.Buff.Might）。注册数据库时并入 toolkit 标签注册表，供效果的标签字段下拉与「未登记」提示；运行时匹配不依赖此表。";

        protected override string RowLabel(GameplayTagDefinition item)
            => string.IsNullOrEmpty(item.name) ? "(空名)" : item.name;

        protected override GameplayTagDefinition CreateNew(ChronicleDatabase db, List<GameplayTagDefinition> list)
        {
            int n = list.Count + 1;
            string name;
            do { name = "Tag.New" + n; n++; } while (Contains(list, name));
            return new GameplayTagDefinition(name);
        }

        private static bool Contains(List<GameplayTagDefinition> list, string name)
        {
            foreach (var t in list) if (t != null && t.name == name) return true;
            return false;
        }

        public override void DrawInspector(IChronicleEditorContext ctx, GameplayTagDefinition tag)
        {
            if (tag == null)
            {
                EditorGUILayout.LabelField("请选择或新建一个 Gameplay 标签。", ToolkitEditorStyles.Placeholder);
                return;
            }

            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);
            bool valid = GameplayTag.IsValidName(tag.name);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("名称");
            string name = EditorGUILayout.TextField(tag.name ?? string.Empty,
                valid ? EditorStyles.textField : ToolkitEditorStyles.RedField);
            EditorGUILayout.EndHorizontal();
            string comment = EditorGUILayout.TextField("注释", tag.comment ?? string.Empty);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改 Gameplay 标签");
                tag.name    = name;
                tag.comment = comment;
                ctx.MarkDirty();
            }

            if (!valid)
            {
                EditorGUILayout.LabelField("⚠ 名称不合法：段不能为空，段内不能含空白或 '/'", ToolkitEditorStyles.StatusError);
                return;
            }

            // 祖先预览：注册时逐级隐式登记（A.B.C → A.B、A）
            string norm = GameplayTag.Normalize(tag.name);
            var segs = norm.Split('.');
            if (segs.Length > 1)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("隐式登记的祖先", ToolkitEditorStyles.Header);
                for (int i = segs.Length - 1; i >= 1; i--)
                    EditorGUILayout.LabelField("• " + string.Join(".", segs, 0, i), EditorStyles.miniLabel);
            }
        }
    }
}

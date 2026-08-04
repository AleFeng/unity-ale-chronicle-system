using System.Collections.Generic;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 模板主列表面板基类（各系统页左列）：把 4 个 <c>*TemplatePanel</c> 的雷同骨架下沉——
    /// 色点 / 行标签 / 唯一命名新建 / schema 绘制器 / 「基础信息（名称 + 列表色点）」头。
    /// 各域只需给出 <see cref="GetList"/> / <see cref="Noun"/> / <see cref="NewNamePrefix"/> /
    /// <see cref="NewTemplate"/> / <see cref="SchemaLabel"/>，并在 <see cref="DrawExtras"/> 里画各自专属字段。
    /// </summary>
    public abstract class ChronicleTemplateListPanel<TTemplate> : EditorMasterListPanel<TTemplate>
        where TTemplate : ConfigTemplateBase
    {
        private readonly AttributeDefinitionListDrawer _schemaDrawer = new AttributeDefinitionListDrawer();

        // ── 各域实现 ────────────────────────────────────────────────────────────────

        /// <summary>新建模板时的默认名前缀（如 <c>"attr_template_"</c>）。</summary>
        protected abstract string NewNamePrefix { get; }

        /// <summary>以给定名构造一个新模板（可顺带设默认显示名等）。</summary>
        protected abstract TTemplate NewTemplate(string name);

        /// <summary>schema 绘制器标题（如 <c>"身份字段 schema"</c>）。</summary>
        protected abstract string SchemaLabel { get; }

        /// <summary>各域专属字段（默认空）。绘制位置由 <see cref="ExtrasBeforeSchema"/> 决定。</summary>
        protected virtual void DrawExtras(IChronicleEditorContext ctx, TTemplate tmpl) { }

        /// <summary>专属字段是否画在 schema <b>之前</b>（true：属性 / 特质 / 技能；false：角色的生成规则画在 schema 之后）。</summary>
        protected virtual bool ExtrasBeforeSchema => true;

        // ── 共享骨架 ────────────────────────────────────────────────────────────────

        protected override bool  HasColorDot => true;
        protected override Color RowColor(TTemplate item) => item.color;

        protected override string RowLabel(TTemplate item)
            => string.IsNullOrEmpty(item.name) ? "(未命名)" : item.name;

        protected override void OnInvalidate() => _schemaDrawer.Invalidate();

        protected sealed override TTemplate CreateNew(ChronicleDatabase db, List<TTemplate> list)
        {
            int n = list.Count + 1;
            string name;
            do { name = NewNamePrefix + n; n++; } while (Contains(list, name));
            return NewTemplate(name);
        }

        private static bool Contains(List<TTemplate> list, string name)
        {
            foreach (var t in list) if (t != null && t.name == name) return true;
            return false;
        }

        public sealed override void DrawInspector(IChronicleEditorContext ctx, TTemplate tmpl)
        {
            if (tmpl == null)
            {
                EditorGUILayout.LabelField("请选择或新建一个" + Noun + "。", ToolkitEditorStyles.Placeholder);
                return;
            }

            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);
            EditorGUI.BeginChangeCheck();
            string name  = EditorGUILayout.TextField("名称(引用键)", tmpl.name);
            Color  color = EditorGUILayout.ColorField("列表色点", tmpl.color);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改" + Noun);
                tmpl.name  = name;
                tmpl.color = color;
                ctx.MarkDirty();
            }

            if (ExtrasBeforeSchema)
            {
                EditorGUILayout.Space(4);
                DrawExtras(ctx, tmpl);
                EditorGUILayout.Space(4);
                _schemaDrawer.Draw(ctx, ctx.Database, tmpl.attributes, SchemaLabel);
            }
            else
            {
                EditorGUILayout.Space(4);
                _schemaDrawer.Draw(ctx, ctx.Database, tmpl.attributes, SchemaLabel);
                EditorGUILayout.Space(6);
                DrawExtras(ctx, tmpl);
            }
        }
    }
}

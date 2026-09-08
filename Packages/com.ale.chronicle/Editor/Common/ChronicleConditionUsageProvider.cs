using System;
using System.Collections.Generic;
using Ale.Condition;
using Ale.Condition.Editor;
using UnityEditor;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 把<b>编年史里的内联条件用法</b>贡献给 toolkit 的判定器索引：编年史的条件仍然内联在各实体字段里
    /// （`ConditionExpression` 字段 + 内联绘制器，这是条件系统两种用法之一，与 toolkit 自己的
    /// <c>EffectDefinition.applicationCondition</c> / <c>EffectItem.gate</c> 同一形态），但它们此前对
    /// Condition Editor 是<b>隐形的</b>——「被引用」「悬空键」「实现体检」都统计不到，而效果侧的内联条件却统计得到。
    /// 本提供者补上这块，让 Condition Evaluators 页的引用核对覆盖全部上层系统。
    ///
    /// <para>覆盖 7 处内联字段：特质获得条件、职业从业条件、头衔获得条件、技能树的技能 / 层级解锁条件与技能点获取条件、
    /// 核心属性的按条件修改值。「跳转」回调直接落到编年史编辑器对应页签并定位到那条实体（技能树三处落到「技能」页）。</para>
    ///
    /// <para>与同目录的 <see cref="ChronicleEffectAttributeProvider"/> 一样按资产实时枚举；索引本身由 toolkit 的
    /// <c>ConditionEvaluatorIndexPostprocessor</c> 在 <c>.asset</c> 增删改后失效重建，本类无需自备缓存。</para>
    /// </summary>
    [InitializeOnLoad]
    internal static class ChronicleConditionUsageProvider
    {
        static ChronicleConditionUsageProvider()
        {
            ConditionEvaluatorIndex.RegisterUsageProvider(Provide);
        }

        private static IEnumerable<ConditionKeyUsage> Provide()
        {
            var result = new List<ConditionKeyUsage>();

            foreach (var guid in AssetDatabase.FindAssets("t:ChronicleDatabase"))
            {
                var db = AssetDatabase.LoadAssetAtPath<ChronicleDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (!db) continue;

                // ① 特质：获得条件
                foreach (var trait in db.Traits)
                {
                    if (trait == null) continue;
                    string id = trait.id;
                    Collect(result, trait.eligibility, db, id, "获得条件",
                        () => ChronicleEditorWindow.OpenTrait(db, id));
                }

                // ② 职业：从业 / 转职条件
                foreach (var profession in db.Professions)
                {
                    if (profession == null) continue;
                    string id = profession.id;
                    Collect(result, profession.requirements, db, id, "从业 / 转职条件",
                        () => ChronicleEditorWindow.OpenProfession(db, id));
                }

                // ③ 头衔：获得条件
                foreach (var title in db.Titles)
                {
                    if (title == null) continue;
                    string id = title.id;
                    Collect(result, title.acquisitionConditions, db, id, "获得条件",
                        () => ChronicleEditorWindow.OpenTitle(db, id));
                }

                // ④⑤⑥ 技能树：技能解锁 / 层级解锁 / 技能点获取（技能树是「技能」页的左列面板，只能落到页签）
                foreach (var tree in db.SkillTrees)
                {
                    if (tree == null) continue;
                    string treeId = tree.id;
                    Action jump = () => ChronicleEditorWindow.Open(db, EChronicleTab.Skill);

                    if (tree.skills != null)
                        for (int i = 0; i < tree.skills.Count; i++)
                        {
                            var entry = tree.skills[i];
                            if (entry == null) continue;
                            string what = string.IsNullOrEmpty(entry.skillRef) ? $"技能{i + 1}" : entry.skillRef;
                            Collect(result, entry.unlockCondition, db, treeId, $"技能 {what} 解锁条件", jump);
                        }

                    if (tree.tiers != null)
                        for (int i = 0; i < tree.tiers.Count; i++)
                            if (tree.tiers[i] != null)
                                Collect(result, tree.tiers[i].unlockCondition, db, treeId, $"层级{i + 1} 解锁条件", jump);

                    if (tree.pointGrants != null)
                        for (int i = 0; i < tree.pointGrants.Count; i++)
                            if (tree.pointGrants[i] != null)
                                Collect(result, tree.pointGrants[i].condition, db, treeId, $"技能点{i + 1} 获取条件", jump);
                }

                // ⑦ 核心属性：按条件修改值
                foreach (var attr in db.CoreAttributes)
                {
                    if (attr?.conditionalModifiers == null) continue;
                    string id = attr.id;
                    for (int i = 0; i < attr.conditionalModifiers.Count; i++)
                    {
                        var cm = attr.conditionalModifiers[i];
                        if (cm == null) continue;
                        Collect(result, cm.condition, db, id, $"条件修改值{i + 1}",
                            () => ChronicleEditorWindow.OpenCoreAttribute(db, id));
                    }
                }
            }

            return result;
        }

        private static void Collect(List<ConditionKeyUsage> into, ConditionExpression expr,
            ChronicleDatabase db, string ownerId, string where, Action jump)
        {
            ConditionEvaluatorIndex.CollectKeys(expr, (key, gi, ii) => into.Add(new ConditionKeyUsage
            {
                Key      = key,
                Asset    = db,
                OwnerId  = ownerId,
                Location = $"{where} · 组{gi + 1} 第{ii + 1}项",
                Jump     = jump,
            }));
        }
    }
}

using System;
using System.Collections.Generic;
using Ale.Condition;
using Ale.Modifier;

namespace Ale.Chronicle
{
    /// <summary>
    /// 角色运行时视图（静态门面）：把配置层与各运行时管理器合成「有效状态」，并做<b>运行时属性汇流</b>——
    /// 基础值 + ① 特质（配置 ∪ 运行时）② 职业成长（配置 ∪ 运行时，同职业取较高等级）③ 头衔（配置 ∪ 运行时）
    /// ④ 条件修改值（条件经 <see cref="ChronicleEffectContext"/> 求值；重入防护）⑤ 活动效果修饰器（未抑制、非周期）⑥ 效果永久落地修饰器，
    /// 经 <see cref="CoreAttributeResolver.Evaluate(CoreAttributeDefinition, float, IEnumerable{ModifierDefinition})"/> 夹紧范围并给出逐来源明细。
    /// 纯配置的求值仍用 <see cref="CoreAttributeResolver"/>。
    /// </summary>
    public static class ChronicleCharacterRuntime
    {
        [ThreadStatic] private static HashSet<string> _evaluating;

        #region 属性汇流

        /// <summary>汇流求值某角色的核心属性（角色 / 属性经数据管理器按 id 反查；缺失时按默认基础值）。</summary>
        public static ModifierEvaluation Evaluate(string characterId, string attrId, IConditionContext ctx = null)
        {
            var dm = ChronicleDataManager.Instance;
            return Evaluate(characterId, dm?.GetCharacter(characterId), dm?.GetCoreAttribute(attrId), attrId, ctx);
        }

        /// <summary>汇流求值（显式角色定义 / 属性定义；角色可未注册于数据管理器，其配置层特质 / 职业 / 头衔仍参与）。</summary>
        public static ModifierEvaluation Evaluate(CharacterDefinition character, CoreAttributeDefinition def, IConditionContext ctx = null)
            => Evaluate(character?.id, character, def, def?.id, ctx);

        /// <summary>汇流求值并只取最终值。</summary>
        public static float EvaluateValue(string characterId, string attrId, IConditionContext ctx = null)
            => Evaluate(characterId, attrId, ctx).Value;

        private static ModifierEvaluation Evaluate(string characterId, CharacterDefinition character, CoreAttributeDefinition def,
            string attrId, IConditionContext ctx)
        {
            var mods = new List<ModifierDefinition>();
            CollectModifiers(characterId, character, def, attrId, ctx, mods);
            float baseValue = character != null
                ? character.GetCoreBaseValue(attrId, def)
                : (def != null ? def.defaultBase : 0f);
            return CoreAttributeResolver.Evaluate(def, baseValue, mods);
        }

        /// <summary>收集作用于 <paramref name="attrId"/> 的全部运行时修饰器（六类来源；传空属性 id 收集全部）。</summary>
        public static void CollectModifiers(string characterId, string attrId, IConditionContext ctx, List<ModifierDefinition> into)
        {
            var dm = ChronicleDataManager.Instance;
            CollectModifiers(characterId, dm?.GetCharacter(characterId), dm?.GetCoreAttribute(attrId), attrId, ctx, into);
        }

        private static void CollectModifiers(string characterId, CharacterDefinition character, CoreAttributeDefinition def,
            string attrId, IConditionContext ctx, List<ModifierDefinition> into)
        {
            if (into == null) return;
            var dm = ChronicleDataManager.Instance;
            if (dm == null) return;

            // ① 特质（配置 ∪ 运行时，按 id 去重）
            var traitMgr = TraitRuntimeManager.Instance;
            foreach (var ti in traitMgr.GetEffectiveTraits(characterId, character))
                dm.GetTrait(ti.traitRef)?.CollectModifiers(attrId, into);

            // ② 职业成长（配置 ∪ 运行时，同职业取较高等级）
            foreach (var kv in EffectiveProfessions(characterId, character))
                dm.GetProfession(kv.Key)?.CollectGrowthModifiers(kv.Value, attrId, into);

            // ③ 头衔（配置 ∪ 运行时）
            foreach (var titleId in EffectiveTitleIds(characterId, character))
                dm.GetTitle(titleId)?.CollectModifiers(attrId, into);

            // ④ 条件修改值：条件可能再查属性（含本属性）→ 同角色同属性重入时跳过，避免无限递归
            if (def != null && def.conditionalModifiers != null && def.conditionalModifiers.Count > 0)
            {
                _evaluating ??= new HashSet<string>();
                string key = (characterId ?? string.Empty) + "\n" + (attrId ?? string.Empty);
                if (_evaluating.Add(key))
                {
                    try
                    {
                        CoreAttributeResolver.CollectConditionalModifiers(def, ctx ?? ChronicleEffectContext.Create(characterId), into);
                    }
                    finally
                    {
                        _evaluating.Remove(key);
                    }
                }
            }

            // ⑤⑥ 活动效果（未抑制、非周期）+ 永久落地
            if (!string.IsNullOrEmpty(characterId))
                EffectRuntimeManager.Instance.CollectModifiers(characterId, attrId, into);
        }

        #endregion

        #region 有效状态（配置 ∪ 运行时）

        /// <summary>有效职业：配置层 ∪ 运行时；同职业取较高等级。</summary>
        public static Dictionary<string, int> EffectiveProfessions(string characterId, CharacterDefinition configCharacter = null)
        {
            var result    = new Dictionary<string, int>();
            var character = configCharacter ?? ChronicleDataManager.Instance?.GetCharacter(characterId);
            if (character?.professions != null)
                foreach (var cp in character.professions)
                    Merge(result, cp.professionRef, cp.level);
            if (!string.IsNullOrEmpty(characterId))
                foreach (var pp in ProfessionRuntimeManager.Instance.GetProfessions(characterId))
                    if (pp != null) Merge(result, pp.professionRef, pp.level);
            return result;

            static void Merge(Dictionary<string, int> map, string id, int level)
            {
                if (string.IsNullOrEmpty(id)) return;
                if (!map.TryGetValue(id, out int cur) || level > cur) map[id] = level;
            }
        }

        /// <summary>有效头衔 id：配置层 ∪ 运行时（去重，配置层优先）。</summary>
        public static List<string> EffectiveTitleIds(string characterId, CharacterDefinition configCharacter = null)
        {
            var result    = new List<string>();
            var seen      = new HashSet<string>();
            var character = configCharacter ?? ChronicleDataManager.Instance?.GetCharacter(characterId);
            if (character?.titles != null)
                foreach (var ct in character.titles)
                    if (!string.IsNullOrEmpty(ct.titleRef) && seen.Add(ct.titleRef)) result.Add(ct.titleRef);
            if (!string.IsNullOrEmpty(characterId))
                foreach (var h in TitleRuntimeManager.Instance.GetTitles(characterId))
                    if (h != null && !string.IsNullOrEmpty(h.titleRef) && seen.Add(h.titleRef)) result.Add(h.titleRef);
            return result;
        }

        public static bool HasProfession(string characterId, string professionId)
            => !string.IsNullOrEmpty(professionId) && EffectiveProfessions(characterId).ContainsKey(professionId);

        /// <summary>有效职业等级（未从事返回 0）。</summary>
        public static int GetProfessionLevel(string characterId, string professionId)
            => !string.IsNullOrEmpty(professionId) && EffectiveProfessions(characterId).TryGetValue(professionId, out int lv) ? lv : 0;

        public static bool HasTitle(string characterId, string titleId)
            => !string.IsNullOrEmpty(titleId) && EffectiveTitleIds(characterId).Contains(titleId);

        /// <summary>某阶梯上持有的最高位阶（配置 ∪ 运行时；无返回 int.MinValue）。</summary>
        public static int GetHighestRankTier(string characterId, string ladderId)
        {
            var dm     = ChronicleDataManager.Instance;
            var ladder = dm?.GetRankLadder(ladderId);
            if (ladder?.orderedTitleRefs == null) return int.MinValue;

            int best = int.MinValue;
            foreach (var titleId in EffectiveTitleIds(characterId))
            {
                if (!ladder.orderedTitleRefs.Contains(titleId)) continue;
                var title = dm.GetTitle(titleId);
                if (title != null && title.rankTier > best) best = title.rankTier;
            }
            return best;
        }

        #endregion
    }
}

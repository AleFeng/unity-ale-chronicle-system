using System;
using System.Collections.Generic;

namespace Ale.Chronicle
{
    /// <summary>某角色单个职业的运行时进度（等级 / 当前等级内经验 / 是否主职业）。</summary>
    [Serializable]
    public class ProfessionProgress
    {
        public string professionRef;
        public int level = 1;
        public int currentExp;
        public bool isPrimary;

        public ProfessionProgress() { }

        public ProfessionProgress(string professionRef, int level = 1, int currentExp = 0, bool isPrimary = false)
        {
            this.professionRef = professionRef;
            this.level         = level;
            this.currentExp    = currentExp;
            this.isPrimary     = isPrimary;
        }

        public ProfessionProgress Clone() => new ProfessionProgress(professionRef, level, currentExp, isPrimary);
    }

    /// <summary>
    /// 单个角色的运行时职业状态：其持有的全部职业进度。由 <see cref="ProfessionRuntimeManager"/> 维护，
    /// 并作为存档单元（<see cref="ProfessionRuntimeManager.GetSaveData"/>）。
    /// </summary>
    [Serializable]
    public class RuntimeCharacterProfessionState
    {
        /// <summary>角色 ID（引用 <see cref="CharacterDefinition.id"/>）。</summary>
        public string characterId;

        /// <summary>该角色的职业进度列表。</summary>
        public List<ProfessionProgress> professions = new List<ProfessionProgress>();

        public RuntimeCharacterProfessionState() { }

        public RuntimeCharacterProfessionState(string characterId)
        {
            this.characterId = characterId;
        }

        public RuntimeCharacterProfessionState Clone()
        {
            var clone = new RuntimeCharacterProfessionState(characterId);
            foreach (var p in professions) clone.professions.Add(p != null ? p.Clone() : new ProfessionProgress());
            return clone;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Ale.Chronicle
{
    /// <summary>某角色持有的单个头衔（头衔 ID + 获得日期）。</summary>
    [Serializable]
    public class TitleHolding
    {
        public string titleRef;
        public int acquiredWorldDay;

        public TitleHolding() { }

        public TitleHolding(string titleRef, int acquiredWorldDay = 0)
        {
            this.titleRef         = titleRef;
            this.acquiredWorldDay = acquiredWorldDay;
        }

        public TitleHolding Clone() => new TitleHolding(titleRef, acquiredWorldDay);
    }

    /// <summary>
    /// 单个角色的运行时头衔状态：其持有的全部头衔。由 <see cref="TitleRuntimeManager"/> 维护，
    /// 并作为存档单元（<see cref="TitleRuntimeManager.GetSaveData"/>）。
    /// </summary>
    [Serializable]
    public class RuntimeCharacterTitleState
    {
        /// <summary>角色 ID（引用 <see cref="CharacterDefinition.id"/>）。</summary>
        public string characterId;

        /// <summary>该角色持有的头衔列表。</summary>
        public List<TitleHolding> titles = new List<TitleHolding>();

        public RuntimeCharacterTitleState() { }

        public RuntimeCharacterTitleState(string characterId)
        {
            this.characterId = characterId;
        }

        public RuntimeCharacterTitleState Clone()
        {
            var clone = new RuntimeCharacterTitleState(characterId);
            foreach (var h in titles) clone.titles.Add(h != null ? h.Clone() : new TitleHolding());
            return clone;
        }
    }
}

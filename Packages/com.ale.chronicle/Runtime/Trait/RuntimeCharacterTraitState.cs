using System;
using System.Collections.Generic;

namespace Ale.Chronicle
{
    /// <summary>某角色的运行时特质存档状态：运行期授予的特质实例（配置层 <see cref="CharacterDefinition.traits"/> 不落盘于此）。</summary>
    [Serializable]
    public class RuntimeCharacterTraitState
    {
        public string characterId;

        /// <summary>运行时授予的特质实例（剩余天数 / 层数 / 来源标记）。</summary>
        public List<CharacterTraitInstance> traits = new List<CharacterTraitInstance>();

        public RuntimeCharacterTraitState() { }

        public RuntimeCharacterTraitState(string characterId)
        {
            this.characterId = characterId;
        }

        public RuntimeCharacterTraitState Clone()
        {
            var clone = new RuntimeCharacterTraitState(characterId);
            clone.traits.AddRange(traits);   // 值类型：直接复制
            return clone;
        }
    }
}

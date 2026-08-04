using System;
using System.Collections.Generic;

namespace Ale.Chronicle
{
    /// <summary>
    /// 特定等级解锁：达到 <see cref="level"/> 级时授予特质 / 头衔资格。
    /// 授予由运行时 <c>ProfessionRuntimeManager.AddExp</c> 在升级循环里触发。
    /// </summary>
    [Serializable]
    public class LevelUnlock
    {
        /// <summary>触发等级。</summary>
        public int level;

        /// <summary>授予的特质 id 列表（→ <see cref="TraitDefinition.id"/>）。</summary>
        public List<string> grantTraitRefs = new List<string>();

        /// <summary>授予的头衔 id 列表（→ TitleDefinition.id，头衔系统落地后生效）。</summary>
        public List<string> grantTitleRefs = new List<string>();

        public LevelUnlock Clone() => new LevelUnlock
        {
            level          = level,
            grantTraitRefs = grantTraitRefs != null ? new List<string>(grantTraitRefs) : new List<string>(),
            grantTitleRefs = grantTitleRefs != null ? new List<string>(grantTitleRefs) : new List<string>(),
        };
    }
}

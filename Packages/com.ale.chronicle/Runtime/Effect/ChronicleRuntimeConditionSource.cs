namespace Ale.Chronicle
{
    /// <summary>
    /// <see cref="IChronicleConditionSource"/> 的运行时实现：按作用域解析角色 id（主体 = 效果目标、对象 = 效果来源、
    /// 第三方 = 可选、主体父 / 母 = 经角色定义反查；配偶字段尚未落地 → 空），数据取「配置 ∪ 运行时」：
    /// 特质经 <see cref="TraitRuntimeManager"/>、属性经 <see cref="ChronicleCharacterRuntime"/> 汇流求值、年龄经 <see cref="ChronicleClock"/>、
    /// 职业 / 头衔 / 阶级合并配置层与运行时管理器。作用域解析不到角色时返回安全默认（false / 0 / int.MinValue）。
    /// </summary>
    public sealed class ChronicleRuntimeConditionSource : IChronicleConditionSource
    {
        public string ActorId     { get; }
        public string RecipientId { get; }
        public string SecondaryId { get; }

        public ChronicleRuntimeConditionSource(string actorId, string recipientId = null, string secondaryId = null)
        {
            ActorId     = actorId;
            RecipientId = recipientId;
            SecondaryId = secondaryId;
        }

        /// <summary>作用域 → 角色 id（解析不到返回 null）。</summary>
        public string Resolve(EConditionScope scope)
        {
            switch (scope)
            {
                case EConditionScope.Actor:       return ActorId;
                case EConditionScope.Recipient:   return RecipientId;
                case EConditionScope.Secondary:   return SecondaryId;
                case EConditionScope.ActorFather: return ChronicleDataManager.Instance?.GetCharacter(ActorId)?.fatherRef;
                case EConditionScope.ActorMother: return ChronicleDataManager.Instance?.GetCharacter(ActorId)?.motherRef;
                case EConditionScope.ActorSpouse: return null;   // TODO(婚姻系统)：配偶字段落地后接入
                default:                          return null;
            }
        }

        public bool HasTrait(EConditionScope scope, string traitId)
        {
            string id = Resolve(scope);
            return !string.IsNullOrEmpty(id) && TraitRuntimeManager.Instance.Has(id, traitId);
        }

        public float GetCoreAttribute(EConditionScope scope, string attrId)
        {
            string id = Resolve(scope);
            return string.IsNullOrEmpty(id) ? 0f : ChronicleCharacterRuntime.EvaluateValue(id, attrId);
        }

        public int GetAge(EConditionScope scope)
        {
            string id = Resolve(scope);
            if (string.IsNullOrEmpty(id)) return 0;
            var character = ChronicleDataManager.Instance?.GetCharacter(id);
            return character != null ? character.GetAge(ChronicleClock.Instance.WorldDay) : 0;
        }

        public bool HasProfession(EConditionScope scope, string professionId)
        {
            string id = Resolve(scope);
            return !string.IsNullOrEmpty(id) && ChronicleCharacterRuntime.HasProfession(id, professionId);
        }

        public int GetProfessionLevel(EConditionScope scope, string professionId)
        {
            string id = Resolve(scope);
            return string.IsNullOrEmpty(id) ? 0 : ChronicleCharacterRuntime.GetProfessionLevel(id, professionId);
        }

        public bool HasTitle(EConditionScope scope, string titleId)
        {
            string id = Resolve(scope);
            return !string.IsNullOrEmpty(id) && ChronicleCharacterRuntime.HasTitle(id, titleId);
        }

        public int GetHighestRankTier(EConditionScope scope, string ladderId)
        {
            string id = Resolve(scope);
            return string.IsNullOrEmpty(id) ? int.MinValue : ChronicleCharacterRuntime.GetHighestRankTier(id, ladderId);
        }
    }
}

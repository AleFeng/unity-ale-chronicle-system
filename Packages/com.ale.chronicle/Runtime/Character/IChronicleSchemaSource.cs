using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 角色配置查表契约：<see cref="CharacterDefinition"/> 的属性汇流 / schema 重建需要按 id 反查
    /// 模板、特质、功能标签。抽成接口令角色域<b>不直接依赖具体数据库</b>——
    /// <c>ChronicleDatabase</c>（后续步骤）实现本接口即可，编辑器 / 测试也可用轻量替身注入。
    /// </summary>
    public interface IChronicleSchemaSource
    {
        /// <summary>按名取角色模板（承载自由字段 schema）；不存在返回 null。</summary>
        CharacterTemplate GetCharacterTemplate(string name);

        /// <summary>按名取核心属性模板（承载自定义字段 schema 与默认值）；不存在返回 null。</summary>
        CoreAttributeTemplate GetCoreAttributeTemplate(string name);

        /// <summary>按名取特质模板（承载自定义字段 schema 与默认值）；不存在返回 null。</summary>
        TraitTemplate GetTraitTemplate(string name);

        /// <summary>按 id 取特质定义（携带 modifiers 与 functionTagRef）；不存在返回 null。</summary>
        TraitDefinition GetTrait(string id);

        /// <summary>按名取功能标签（携带附加字段 schema）；不存在返回 null。</summary>
        Tag GetTag(string name);
    }
}

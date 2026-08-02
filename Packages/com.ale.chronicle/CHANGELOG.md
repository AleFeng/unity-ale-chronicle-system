# 更新日志（Changelog）

本文件记录 Chronicle System（`com.ale.chronicle`）的所有重要变更。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [0.1.0] - 2026-08-02

初始基线版本：**角色 / 核心属性 / 特质 / 技能** 四大领域的配置与运行时数据基础，构建于 [`com.ale.toolkit`](../com.ale.toolkit)（Schema 属性引擎 / 编辑器三列框架 / 虚拟滚动列表 / 序列化 / `Ale.Condition` 条件系统）之上。

### 新增

- **中心配置资产 `ChronicleDatabase`**（`ScriptableObject`，`Create > ChronicleSystem > Chronicle Database`）：聚合枚举类型、功能标签、分组标签、数字格式、核心属性（+ 模板）、特质（+ 模板）、角色（+ 模板）、技能（+ 模板）共 11 个列表；实现 `IEnumTypeSource` + `IChronicleSchemaSource`，提供按 id 查询与 `Validate`（重复 id + 悬空引用检查）、`CloneFrom`（深拷贝「以此为模板」）。
- **角色系统**：`CharacterDefinition`（身份自由字段 + 核心属性基础值 + 特质实例 + 父/母/子家族指针）+ `CharacterTemplate`；稳定身份字段常量 `WellKnownAttr`（name / birthday / sex / height / weight / health / fertility）；`GetAge(worldDay)`；Schema 源接口 `IChronicleSchemaSource`。
- **属性系统**：`CoreAttributeDefinition` / `CoreAttributeTemplate` / `CoreAttributeValue`（取值范围 / 分类枚举 / 默认基础值 / 图标 / 自定义字段）。
- **核心属性合流**：`CoreAttributeResolver` 以 toolkit `ModifierStackEvaluator` 计算「基础值 → 原始值 → 最终值」，返回逐来源 `Breakdown` 并按 min/max 钳制；高层重载直接从角色特质收集修正器。
- **特质系统**：`TraitDefinition`（永久 / 临时生命周期、修正器、硬互斥「等价组 + 显式」、软兼容「好感增减」、遗传「继承 / 出生几率」、AI 权重、**`Ale.Condition` 获得条件**）+ `TraitTemplate` + `CharacterTraitInstance`（持续天数 / 叠加 / 到期）。
- **条件系统整合**：内置三个 `[ConditionEvaluator]` 插件——`Chronicle.Age`（年龄区间）、`Chronicle.AttributeCompare`（核心属性比较）、`Chronicle.HasTrait`（是否拥有特质）；`EConditionScope` 支持本人 / 收受方 / 次要 / 配偶 / 父 / 母 多作用域；运行时经 `IChronicleConditionSource` 提供数据。
- **技能系统**：`Skill` / `SkillTemplate`（共用 `ISkillConfig` 绘制器）——显示名 / 描述（Text 带本地化 fallback）、图标（Sprite / Addressable）、主 + 副分组标签、自定义属性。
- **技能运行时 `SkillRuntimeManager`**（`ISaveable`）：**两层模型**——① 永久学会层（入存档）；② 外部提供者层（如装备授予，不入存档，读档后由业务层重算）；合并为**有效技能集**（`GetEffectiveSkills` / `HasSkill`），变化触发 `OnLearnedChanged`。提供者层 `SetProvidedSkills` 为「整组并集替换」，天然满足「共享技能被多来源提供时不误删」「永不动永久层」。另有无状态**一次性使用 / 施放** `UseSkill` → 派发 `OnSkillUsed(SkillUseEvent)`，效果由业务层订阅实现。
- **运行时管理器**：`ChronicleDataManager`（`ToolkitSingleton`，非 Mono，跨库 O(1) 查询、`LoadFromBinary`）、`ChronicleRuntimeManager`（`ToolkitMonoSingleton`，覆盖式 UI 根 / Layer + 全局技能 Tooltip 宿主，经 `ISkillTooltip` 依赖倒置）。
- **技能 UI**（程序集 `Ale.Chronicle.Runtime.UI`）：`UiwSkillView`（搜索 + 主/副分组 AND 过滤 + 网格/顺序双视图 + 目录/角色双来源）、`UiwSkillGridList` / `UiwSkillOrderList`（虚拟滚动）、`UiwSkillEntry` / `UiwSkillTooltip`、`SkillCollector`。
- **二进制序列化 `ChronicleConfigSerializer`**：`ChronicleDatabase` ↔ 紧凑二进制，魔数 `CHRO`、格式 `Version = 3`、`MinReadableVersion = 1`（v1→v2→v3 版本化追加块，向后兼容旧文件）；对象引用经 `IAssetRefResolver` 以 GUID 承载，特质获得条件以条件系统 JSON 存储。
- **编辑器**：`ChronicleEditorWindow`（`Tools > Ale Toolkit > Chronicle System > Chronicle Editor`）三列布局、五页签 **角色 / 属性 / 特质 / 技能 / 通用**（「通用」含 枚举 / 功能标签 / 分组标签 / 数字格式 子页签）；重复 ID 高亮、角色属性合流活预览、导出二进制（导出前校验）；`ChronicleDatabaseInspector`「在编辑器中打开」按钮；可复用 `ModifierListDrawer` / `SkillConfigDrawer` / `ChronicleEntityHeader`。
- **Demo Wizard**（`Tools > Ale Toolkit > Chronicle System > Demo Wizard`）：按依赖闭包一键生成技能 UI 预制体。
- **Chronicle × Inventory 整合 Demo**（`Assets/DemoInventory/`，依赖 `com.ale.inventory`，编入 `Assembly-CSharp`）：`EquipmentSkillBridge`（装备「持有」技能 → `SetProvidedSkills`）、`ConsumableSkillUse`（消耗品「触发」技能 → `UseSkill` + 扣道具）、`SkillEffectDemoListener`（订阅 `OnSkillUsed` 示例）、`EquipmentSkillDemo`（自包含 IMGUI 驱动）。**两个包互不依赖**，整合仅经道具的通用 `AttributeValue`（存技能 id 的纯 String）。
- **本地化**：包内文本经 toolkit `AttributeValue.ResolveText()` 解析、`ATK_LOCALIZATION` 下接 Unity Localization；演示字符串表覆盖 7 种 Locale（en / fr / ja / ko / ru / zh-Hans / zh-Hant）+ CJK 字体。
- **测试**：`Assets/Tests/`（`Ale.Chronicle.Tests`，EditMode NUnit）12 个测试文件，覆盖数据库 / 数据管理器 / 二进制序列化 / 属性合流 / 条件求值 / 特质 / 模板层 / 角色组合 / 技能数据 / 技能运行时 / 技能 UI。

### 依赖

- **`com.ale.toolkit`（必需，先安装；建议 1.4.0 或更新，需含 `Ale.Condition`）**。UPM 不支持在 `dependencies` 写 git URL，故 `package.json` 的 `dependencies` 留空——**必须手动先装 toolkit、再装本插件**。
- 可选：TextMeshPro（`ATK_TMP`）/ Unity Localization（`ATK_LOCALIZATION`）/ Unity Addressables（`ATK_ADDRESSABLE`）。
- `com.ale.inventory`：**仅整合 Demo 需要**，核心包不依赖。

### 说明

- **预留、暂未接入求值**：`CharacterTemplate` 的角色随机生成规则（种族 / 保底特质 / 随机特质池 / 属性点预算 / 出生年龄区间）、`EModifierTargetKind` 的身体机能 / 派生属性 / 常识字段目标、`TraitAiWeight` 的 AI 权重轴——字段已就位，将在后续版本接入。
- 技能功能自 `com.ale.inventory` 迁移而来：库存包已于其 `1.11.0` 移除整个技能子系统，技能能力统一由本包提供。

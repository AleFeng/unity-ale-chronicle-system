# 更新日志（Changelog）

本文件记录 Chronicle System（`com.ale.chronicle`）的所有重要变更。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [0.3.0] - 2026-08-05

新增**技能树（SkillTree）**一等配置对象与**属性按条件修改值**功能，并让**职业关联技能树**。技能树与属性条件修改的门控条件全部复用 Toolkit 条件框架，**Chronicle 端零新增判定器**（跨系统条件由上层系统实现、各系统共用）。序列化随之升级至 **v6**（append-only 向后兼容，旧 v3 / v4 / v5 存档均可载）。

### 新增

- **技能树 `SkillTree`**：一等配置对象（技能页左列新增「技能树」子页签），按 `ESkillTreeKind` 三种类型组织一组技能——**列表**（无层级）/ **层级**（`SkillTreeTier`：先分层、层内加技能，层级与技能均可配解锁条件）/ **树状**（`SkillTreeEntry.prerequisiteSkillRefs`：每技能以前置技能作解锁，AND 语义）。每技能 / 层级可内联配 `ConditionExpression` 解锁条件；条件承载体铺平为技能树下的一层数组（技能以 `tierKey` 关联层级，重排不失联）。
- **技能点获取 `SkillPointGrant`**：技能树可配多个获取条目——点数 + 获取方式 `ESkillPointGrantMode`（**一次性达成即得 / 持续生效 / 每级可重复**）+ 获取条件（`Ale.Condition`，一般为某职业等级，复用现成 `Chronicle.ProfessionLevelAtLeast`）。
- **职业关联技能树**：`ProfessionDefinition` 新增 `skillTreeRefs`（可关联一或多棵技能树）；检视器加「关联技能树」多选区，`Validate` 加悬空引用检查。
- **属性按条件修改值**：`CoreAttributeDefinition` 新增 `conditionalModifiers`（`ConditionalModifier` = 一条 `ModifierDefinition` 修改值 + 一个内联 `ConditionExpression` 门控，payload + gate 范式，仿 toolkit `EffectItem`）。例：兴趣值随剧情章节完成而变。`CoreAttributeResolver` 新增带 `IConditionContext` 的重载，在合流**收集期按条件过滤**这些修改值（来源 `attr:{id}:cond`）；旧三参重载委托 `ctx=null`，既有汇流零回归。
- **数据库 / 运行时**：`ChronicleDatabase` 增 `SkillTrees` 列表（+ 访问器 + `GetSkillTree` + `CloneFrom`）与 `Validate`（技能树 id 查重、`skillRef` / `tierKey` / 前置悬空、**树状前置成环 DFS 检测**、职业 `skillTreeRefs` 悬空）；`ChronicleDataManager` 增技能树索引与 `GetSkillTree` / `GetAllSkillTrees`。
- **编辑器**：技能页左列扩为「技能模板 / 技能树」双子页签；`SkillTreeDrawer` 三分支（列表 / 层级复用阶级序列「延迟应用」/ 树状复用转职树「缩进 + 折叠 + 防环」，加后继带前置防环）；技能点获取条目编辑；`ChronicleEditorFields.InlineConditionAt` 新增（任意嵌套路径的内联条件助手，供技能树与属性条件修改共用）；属性检视器加「根据条件修改属性值」区（隐藏目标下拉的单条修改值 + 内联门控条件）。
- **测试**：`Assets/Tests/` 新增 2 个测试文件（现 20 个）——`SkillTreeSerializerTests`（v6 往返：三类型 / 三种获取方式 / 各处条件 / 职业引用 / 属性条件修改，及旧 v5 兼容）、`AttributeConditionalModifierTests`（属性条件修改：空条件计入 / 非空条件无上下文排除 / 三参回归）。

### 变更

- **序列化 `Version` 5 → 6**：尾部追加 技能树 一块；职业块尾追加 `skillTreeRefs`；核心属性块尾追加 `conditionalModifiers`（条件修改值）——三处均 `if (version >= 6)` 门控，**append-only 向后兼容**，旧 v3 / v4 / v5 二进制仍可导入（新字段为空）。
- **术语统一**：六大领域编辑器界面的「自定义字段」/「自定义属性」名称统一为**「自定义属性字段」**（纯 UI 文案，零功能影响）。
- `CoreAttributeResolver.Evaluate` 新增带条件上下文的高层重载；既有三参入口委托到新重载（`ctx=null`），行为不变。

### 说明

- **条件实现**：技能树解锁 / 技能点获取 / 属性条件修改的门控中，「某职业等级」复用现成 `Chronicle.ProfessionLevelAtLeast`；「层级总技能点数」「章节完成 / 世界标志」等**跨系统条件**建模为 Toolkit 通用 `Condition.NumberCompare` / `Condition.HasFlag`，由上层系统（如剧情系统）实现判定器与条件源、各系统共用——**Chronicle 端不新增判定器、不改 `IChronicleConditionSource`**。
- **运行时降级**：属性条件修改的门控在编辑器无真实条件源（`ctx=null`）时按「空条件计入、门控条件不计入」近似预览，运行时传入真实 `IConditionContext` 方生效。

## [0.2.0] - 2026-08-04

新增**职业系统（Profession）**与**头衔系统（Title）**两大配置域，接续 0.1.0 中 `CharacterDefinition` 预留的「产出 Modifier 汇入同一主干」切片；并为职业 / 头衔补齐**模板层**（`ProfessionTemplate` / `TitleTemplate`，`templateRef` + schema 自定义字段），使六大领域模板模式统一。序列化随之升级至 **v5**（append-only 向后兼容，旧 v3 / v4 存档均可载）。

### 新增

- **职业系统**：`ProfessionDefinition`（显示信息 / 分组 / 等级上限 / `ExpCurve` 经验曲线 / 每级成长 / 等级解锁 / 从业条件 / 允许种族）+ `ExpCurve`（**三模式**：公式 / 表格 / 曲线，曲线经 `AttributeValue(AnimationCurve)` 承载）+ `LevelGrowthEntry`（线性 / 曲线成长）+ `LevelUnlock`（授予特质 / 头衔）。
- **转职树 `ProfessionTree`**：一等配置对象——职业间「父 → 子 = 可转职为」的进阶 DAG（`ProfessionTreeNode.childProfessionRefs`）；`Roots()` 派生根、`FindNode` 查节点。
- **头衔系统**：`TitleDefinition`，两类由 `ETitleKind` 区分——**阶级头衔（RankTitle）** 逐级晋升 / 可继承，**称号（Epithet）** 靠事迹获得 / 多为唯一；含位阶 / 唯一 / 可剥夺 / 修饰器 / 好感修饰器 / 获得条件。
- **阶级序列 `RankLadder`**：承载阶级头衔的有序阶梯（低 → 高），运行时「同时只持其一」。
- **角色侧汇流**：`CharacterDefinition` 新增 `professions`（`CharacterProfession`：等级 / 经验 / 主职业）与 `titles`（`CharacterTitle`）持有字段；`CollectModifiers` 扩展为**特质 / 职业每级成长（`prof:{id}:growth`，按等级折算）/ 头衔加成（`title:{id}`）三来源统一汇入核心属性**（头衔 `opinionModifiers` 面向社交轴、不汇入核心）。
- **运行时管理器**（均 `ToolkitSingleton` + `ISaveable`，存 / 读档不触发事件）：
  - `ProfessionRuntimeManager`——习得 / 放弃 / 单一主职业；**`AddExp`** 按 `ExpCurve` 跑升级循环、`maxLevel` 封顶，每升级触发 `OnLevelUp` 并施加 `LevelUnlock`（授头衔经 `TitleRuntimeManager`、授特质经 `OnUnlockTrait` 事件）；存档持每角色职业进度。
  - `TitleRuntimeManager`——**`Grant`**：阶级头衔按阶梯「晋升替换」（同序列只持其一）、唯一头衔从他人剥夺并触发 `OnTitleTransferred`；`Revoke` 受 `isRevocable` 约束；`GetHighestRankTier` 查某序列最高位阶；存档持每角色持有头衔。
- **条件系统**：新增 4 个 `[ConditionEvaluator]`——`Chronicle.HasProfession` / `Chronicle.ProfessionLevelAtLeast` / `Chronicle.HasTitle` / `Chronicle.HasRankAtLeast`；`IChronicleConditionSource` 相应新增 4 个查询方法。
- **职业 / 头衔模板**：`ProfessionTemplate` / `TitleTemplate`（`ConfigTemplateBase`：名称 / 色点 / 自定义字段 schema + 默认预设——职业「默认等级上限」、头衔「默认种类 / 可剥夺」）。`ProfessionDefinition` / `TitleDefinition` 升级为 `AttributeOwner`，获得 `templateRef` 与模板 schema 驱动的自定义字段 `values`（`RebuildAttributes` 依 schema 对账）；编辑器双页签左列首位加模板面板（复用 `ChronicleTemplateListPanel`），中列过滤维由分组改为**模板**、「从模板添加」复制预设并按 schema 建字段，检视器补只读来源模板与自定义字段区。分组标签降为检视器可编辑字段（与技能系统一致）。
- **数据库**：`ChronicleDatabase` 增 `ProfessionTemplates` / `Professions` / `ProfessionTrees` / `TitleTemplates` / `Titles` / `RankLadders` 六列表（+ 访问器 + id/name 查询）；`Validate` 增职业 / 头衔悬空引用检查（含 `templateRef`）、模板 name 查重、**转职树成环 DFS 检测**、阶级序列须为阶级头衔校验；`CloneFrom` 覆盖新列表。运行时 `ChronicleDataManager` 增六类索引与 `GetAllProfessions` / `GetAllTitles` / `GetAllRankLadders`。
- **编辑器**：新增 **职业**、**头衔** 两页签（`ChronicleEditorWindow` 现 **七页签**：角色 / 属性 / 特质 / 技能 / 职业 / 头衔 / 通用）。
  - 职业页——左列子页签 **职业模板**（首位）/ 转职树；中列职业列表（按模板过滤 / 从模板添加）+ 右列**只读来源模板**、**ExpCurve 三模式编辑器 + 每级所需经验预览折线**、每级成长表、等级解锁、内联从业条件、**自定义字段区**；转职树子页签右列为**缩进结构编辑器**（折叠 / 加子带防环 / 移除）。
  - 头衔页——左列子页签 **头衔模板**（首位）/ 阶级序列；中列头衔列表（按模板过滤 / 从模板添加）+ 右列**只读来源模板**、按 `kind` 动态字段、修饰器（复用 `ModifierListDrawer`）+ 好感修饰器、内联获得条件、**自定义字段区**；阶级序列子页签右列为**有序阶梯编辑器**（上下移 / 移除 / 加菜单仅列阶级头衔）。
  - 角色检视器补「职业」（等级 / 经验 / 主职业 + 该级成长预览）与「头衔」（按 kind 标注）分区；核心属性明细自动含 `prof:*` / `title:*` 来源行。
- **测试**：`Assets/Tests/` 新增 6 个测试文件（现 18 个），覆盖 ExpCurve 三模式一致性 / 汇流端到端 / 序列化 v5 往返（含模板 + 自定义字段）+ 旧 v3 / v4 兼容 / 数据库校验（含成环）/ 数据管理器索引 / 新条件判定器 / 运行时管理器（升级 / 解锁 / 一序列一持有 / 唯一头衔易主 / 存档）。

### 变更

- **序列化 `Version` 3 → 5**：v4 尾部追加 职业 / 转职树 / 头衔 / 阶级序列 四块 + 角色块尾职业 / 头衔持有字段；**v5** 再追加 职业模板 / 头衔模板 两块，并在职业 / 头衔块尾追加 `templateRef` + 自定义字段 `values`——**append-only 向后兼容**，旧 v3 / v4 二进制仍可导入（缺失部分为空）。
- `CharacterDefinition.CollectModifiers` 由「仅特质」扩展为「特质 + 职业 + 头衔」三来源汇流（既有特质路径不变）。
- **职业 / 头衔中列过滤维由分组标签改为模板**（分组标签保留为检视器可编辑字段），与角色 / 技能一致。

### 说明

- **预留、暂未接入求值**：`ProfessionDefinition.allowedRaceRefs`（种族系统未落地）、`TitleDefinition.successionPolicyRef` / `heritable`（继承结算未落地）——存为不透明串、宽松校验。0.1.0 已列的其它预留项（`CharacterTemplate` 生成规则、`EModifierTargetKind` 扩展目标、`TraitAiWeight`）仍然预留。

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

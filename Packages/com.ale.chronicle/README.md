# 编年史系统（Chronicle System）

<p align="center">
  🌍
  中文
</p>

面向设计师的 Unity 数据驱动**角色 / 人生模拟**配置系统。用一个 `ChronicleDatabase` 资产集中配置 **角色 / 核心属性 / 特质 / 技能 / 职业 / 头衔** 六大领域，以及配套的 **枚举类型 / 功能标签 / 分组标签 / 数字格式**；动态运行时状态（已学技能、外部来源提供的技能、职业等级 / 经验、持有头衔、核心属性合流结果等）由对应的运行时管理器维护。

构建于通用底层包 [`com.ale.toolkit`](../com.ale.toolkit) 之上，直接复用它的 **Schema 属性引擎（`AttributeOwner` / `AttributeValue`）**、**编辑器三列框架**、**虚拟滚动列表**、**序列化基元** 与 **`Ale.Condition` 条件系统**。

- 编辑器始终且仅在 ScriptableObject 上工作，全程支持 Undo / Redo；二进制为**单向导出**格式。
- 角色 / 核心属性 / 特质 / 技能等实体统一走 toolkit 的**灵活属性系统**，无需改代码即可扩展字段。
- 特质「获得条件」接入 `Ale.Condition`（年龄 / 核心属性比较 / 是否拥有某特质）。
- 核心属性走 **基础值 + 修正器合流**（来源含特质 / 职业成长 / 头衔），带逐来源拆解。
- 文本本地化（Unity Localization）、TextMeshPro、Addressable 均通过编译宏可选启用（与 toolkit 统一）。

> ⚠️ **当前版本 `0.3.0`**：角色 / 属性 / 特质 / 技能 / 职业 / 头衔 六大领域的配置与运行时数据基础已可用（本版技能新增**技能树**、属性新增**按条件修改值**、职业可**关联技能树**）；「百万级人生模拟」中的世代推进、角色随机生成规则（`CharacterTemplate` 的种族 / 保底特质 / 属性点预算等）、继承结算（`TitleDefinition.heritable` / `successionPolicyRef`）、派生属性与身体机能修正目标（`EModifierTargetKind` 的部分取值）等尚为**预留**、暂未接入求值。下文只描述**已实现**能力。

---

## 领域概览

| 领域 | 配置内容 | 运行时 |
|------|---------|--------|
| **角色系统** | 角色模板、角色（身份字段 + 核心属性基础值 + 特质实例 + 父/母/子家族指针） | `CharacterDefinition` 组合 + 属性合流 + `GetAge(worldDay)` |
| **属性系统** | 核心属性模板、核心属性（取值范围 / 分类枚举 / 默认基础值 / 图标 / **按条件修改值**） | `CoreAttributeResolver`（基础值 + 修正器合流，带来源拆解、min/max 钳制；收集期按条件过滤修改值） |
| **特质系统** | 特质模板、特质（生命周期 / 修正器 / 硬互斥 / 软兼容 / 遗传 / AI 权重 / 获得条件） | `TraitDefinition.CollectModifiers` → 属性合流；条件求值经 `Ale.Condition` |
| **技能系统** | 技能模板、技能（显示名 / 描述 / 图标 / 主+副分组标签 / 自定义属性）、**技能树**（列表 / 层级 / 树状 + 技能点获取） | `SkillRuntimeManager`（永久学会 + 外部提供者 两层 + 一次性使用派发） |
| **职业系统** | 职业模板、职业（等级上限 / 经验曲线 / 每级成长 / 解锁 / 从业条件）、转职树 | `ProfessionRuntimeManager`（`AddExp` 按曲线升级 + 等级解锁）；成长汇入核心属性 |
| **头衔系统** | 头衔模板、头衔（阶级头衔 / 称号 · 位阶 / 修饰器 / 获得条件）、阶级序列 | `TitleRuntimeManager`（授予 / 晋升替换 / 唯一头衔易主）；加成汇入核心属性 |
| **通用（General）** | 枚举类型、功能标签、分组标签、数字格式 | 被上述领域引用（枚举下拉、字段成组、技能/职业/头衔分组、数值格式化） |

> **六大领域统一采用 模板（蓝图）+ 定义 / 实例 模式**：共享 toolkit 的 `AttributeOwner` Schema，`RebuildAttributes` 按 Schema 增补 / 移除自定义字段，深拷贝 `Clone` 支持「以此为模板」。定义各有其固定的强类型字段（职业的 `expCurve/growth/unlocks`、头衔的 `kind/rankTier/modifiers` 等），另经模板承载**可选的自定义字段与默认预设**；各页中列均以**模板**作过滤维（分组标签降为检视器可编辑字段）。**转职树 / 阶级序列 / 技能树** 是额外组织职业 / 头衔 / 技能的结构对象。

---

## 各领域说明

### 角色系统（Character）
- **角色 = 三部分组合**：① 身份自由字段（Schema = 模板 ∪ 特质携带的功能标签）；② 核心属性基础值（`CoreAttributeValue` 列表）；③ 特质实例（`CharacterTraitInstance` 列表）。另含 `fatherRef` / `motherRef` / `childRefs` 家族指针。
- **稳定身份字段**：`WellKnownAttr` 固定 id——`name` / `birthday` / `sex` / `height` / `weight` / `health` / `fertility`，供业务层稳定读取。
- **年龄**：`GetAge(worldDay)` 由出生日与世界日推算。
- **模板生成规则字段**（种族 `raceRef`、保底特质、随机特质池、属性点预算、最小/最大出生年龄）已在 `CharacterTemplate` 定义，**当前预留、暂未消费**。

### 属性系统（Core Attribute）
- **核心属性**（力量 / 敏捷 / …）由 `CoreAttributeDefinition` 承载：显示名 / 缩写 / 描述（Text）、图标、分类枚举、取值范围 `minValue` / `maxValue`、默认基础值，以及自定义属性字段。
- **按条件修改值** `conditionalModifiers`：属性自身可挂一组「一条 `ModifierDefinition` 修改值 + 一个内联 `ConditionExpression` 门控」（payload + gate 范式）。例：兴趣值随剧情章节完成而变——门控条件由上层系统（如剧情系统）经通用 `Condition.HasFlag` / `Condition.NumberCompare` 提供，Chronicle 端只负责存储 / 编辑 / 求值。
- **合流求值**：`CoreAttributeResolver.Evaluate(...)` 以 toolkit `ModifierStackEvaluator` 计算 `基础值 → 原始值 → 最终值`，返回 `ModifierEvaluation`（含逐来源 `Breakdown`），并按定义 min/max 钳制。高层重载直接从角色的**特质 / 职业成长 / 头衔**收集修正器（`CharacterDefinition.CollectModifiers`）；带 `IConditionContext` 的重载另在收集期把**属性自身条件通过的 `conditionalModifiers`** 也汇入（来源 `attr:{id}:cond`）——编辑器无真实条件源时按「空条件计入、门控条件不计入」近似预览。
- **编辑器实时预览**：角色 Inspector 展示「基础 → 当前 + 逐来源拆解」的活预览。

### 特质系统（Trait）
- **生命周期**：`ETraitLifetime` 永久 / 临时；临时特质有默认持续天数、可配置叠加是否刷新时长；`CharacterTraitInstance.Ticked(days)` 推进、`remainingDays < 0` 表示永久。
- **修正器**：`modifiers`（toolkit `ModifierDefinition`）作用于核心属性——特质是属性合流来源之一（`SourceTag() = "trait:{id}"`，与职业成长 / 头衔并列汇入同一栈）。
- **互斥 / 兼容**：硬互斥（等价组 `groupEquivalenceRef` + 显式 `incompatibleTraitRefs`，`IsIncompatibleWith`）；软兼容 `TraitCompatibility`（`opinionDelta` 社交好感增减）。
- **遗传 / AI**：`genetic` / `inheritChance` / `birthChance` 与 `TraitAiWeight`（AI 权重轴，**预留**）。
- **获得条件**：`eligibility`（`Ale.Condition` 的 `ConditionExpression`），在编辑器内联表达式绘制器中配置，运行时经条件系统求值。

### 技能系统（Skill）
- **技能目录条目** `Skill`：显示名 / 描述（Text，带本地化 fallback）、`iconValue`（Sprite / Addressable）、模板引用、主分组标签 + 副分组标签、自定义属性；`SkillTemplate` 为其蓝图（默认显示信息 + 字段 Schema，用于筛选 / 创建）。`ISkillConfig` 让二者共用同一套编辑器绘制器。
- **技能树** `SkillTree`（一等配置对象，技能页左列第二子页签）：把一组技能按 `ESkillTreeKind` 组织成 **列表**（无层级）/ **层级**（先分层、层内加技能，层级与技能均可配解锁条件）/ **树状**（每技能以前置技能作解锁，编辑器加后继带防环、`Validate` 亦做前置成环 DFS 检测）三种形态；每技能 / 层级可内联配 `ConditionExpression` 解锁条件。另配多个**技能点获取条目** `SkillPointGrant`（点数 + 获取方式 `ESkillPointGrantMode`：一次性达成即得 / 持续生效 / 每级可重复 + 获取条件，一般为某职业等级）。职业经 `skillTreeRefs` 关联一或多棵技能树。**层级总技能点数 / 章节完成等跨系统条件复用 Toolkit 通用条件框架**（`Condition.NumberCompare` / `Condition.HasFlag`），由上层系统实现判定器与条件源，Chronicle 端不新增判定器。
- 运行时的**已学 / 提供 / 使用**语义见下文 [技能运行时](#技能运行时两层模型--一次性使用)。

### 职业系统（Profession）
- **职业模板** `ProfessionTemplate`（`ConfigTemplateBase`）：一族职业的蓝图——名称 / 色点 / 自定义字段 schema + 预设「默认等级上限」。编辑器职业页左列首个子页签，中列以其作过滤维。
- **职业条目** `ProfessionDefinition`（`AttributeOwner`）：`templateRef` 来源模板 / 显示信息 / 分组标签 / 等级上限 `maxLevel` / 经验曲线 `ExpCurve` / 每级成长 `LevelGrowthEntry` / 等级解锁 `LevelUnlock` / 从业条件（`Ale.Condition`）/ 允许种族（预留）/ 模板 schema 驱动的自定义字段 `values`（`RebuildAttributes` 对账）。「从模板添加」复制预设并按 schema 建字段。
- **经验曲线** `ExpCurve` 三模式：**公式**（`baseExp × level^exponent + linear`）/ **表格**（逐级显式）/ **曲线**（`AnimationCurve`，经 `AttributeValue` 承载），统一 `ExpToNext` / `TotalExpForLevel` 求值。
- **每级成长汇流**：`CollectGrowthModifiers(level, …)` 按角色当前等级把成长折算为 `Add` 修正器（`sourceTag = "prof:{id}:growth"`）汇入核心属性——与特质同栈、升级即刷新。
- **转职树** `ProfessionTree`：职业间「父 → 子 = 可转职为」的进阶 DAG（`ProfessionTreeNode.childProfessionRefs`）；编辑器左列列出、右列缩进结构编辑（加子带**防环**，`Validate` 亦做成环 DFS 检测）。
- **运行时** `ProfessionRuntimeManager`（`ISaveable`）：习得 / 放弃 / 单一主职业；`AddExp` 按曲线跑升级循环、`maxLevel` 封顶，每升级触发 `OnLevelUp` 并施加 `LevelUnlock`（授头衔经 `TitleRuntimeManager`、授特质经 `OnUnlockTrait` 事件）。

### 头衔系统（Title）
- **头衔模板** `TitleTemplate`（`ConfigTemplateBase`）：一族头衔的蓝图——名称 / 色点 / 自定义字段 schema + 预设「默认种类 / 可剥夺」。编辑器头衔页左列首个子页签，中列以其作过滤维。
- **头衔条目** `TitleDefinition`（`AttributeOwner`），`ETitleKind` 两类：**阶级头衔（RankTitle）**——有位阶 `rankTier`、可继承（预留）、逐级晋升同时只持其一；**称号（Epithet）**——靠事迹获得、多为唯一。另含 `templateRef` 来源模板 / `isUnique` / `isRevocable` / 修饰器（汇入核心属性 `title:{id}`）/ 好感修饰器（社交轴，暂不汇入核心）/ 获得条件（`Ale.Condition`）/ 模板 schema 驱动的自定义字段 `values`。
- **阶级序列** `RankLadder`：承载阶级头衔的有序阶梯（低 → 高）；编辑器左列列出、右列有序链编辑（加菜单仅列阶级头衔，`Validate` 校验成员须为 RankTitle）。
- **运行时** `TitleRuntimeManager`（`ISaveable`）：`Grant` 授予——阶级头衔按阶梯「晋升替换」（同序列只持其一）、唯一头衔从他人剥夺并触发 `OnTitleTransferred`；`Revoke` 受 `isRevocable` 约束；`GetHighestRankTier` 查序列最高位阶。

### 通用（General）
两列子页签面板：**枚举类型 / 功能标签 / 分组标签 / 数字格式**。枚举值由系统自动分配、永不复用；功能标签定义一组属性字段并被角色身份字段 Schema 合并；分组标签（`ChronicleGroupTag : GroupTag`）作为技能主/副分组的统一标签池；数字格式配置供数值显示。

---

## 技能运行时（两层模型 + 一次性使用）

`SkillRuntimeManager`（`ToolkitSingleton`，非 Mono、首次访问自动创建，实现 `ISaveable<RuntimeLearnedSkillState>`）把技能状态分为**三种语义**：

1. **永久学会层** `_learned`：`角色 → 有序去重的技能 id 列表`，**入存档**（`GetSaveData` / `LoadSaveData`）。
2. **外部提供者层** `_provided`：`角色 → 提供者 key → 技能 id 列表`，**不入存档**——读档后由业务层按当前来源（如已装备道具）**重算**。
3. **一次性使用 / 施放**：无状态派发，不改任何层、不入存档。

**有效技能集 = 永久层 ∪ 全部提供者**（去重、保序）。有效集变化触发 `OnLearnedChanged(characterId)`（供 UI 刷新）。

| 类别 | API |
|------|-----|
| 永久层查询 | `HasLearned` / `GetLearnedSkillIds` / `GetLearnedSkills` |
| 永久层增删 | `Learn` / `Forget` / `ClearLearned` |
| 提供者层 | `SetProvidedSkills(characterId, providerKey, ids)`（整组并集替换，返回**有效集**是否变化；传 null/空 = 清该提供者）、`ClearProvider` / `ClearAllProviders` |
| 合并视图 | `HasSkill`（有效集成员判定）、`GetEffectiveSkillIds` / `GetEffectiveSkills` |
| 一次性使用 | `UseSkill(target, skillId, sourceKey = null)`——校验技能存在后派发 `OnSkillUsed(SkillUseEvent)` 返回 true；不存在则不派发返回 false。**无状态**、不触发 `OnLearnedChanged`、不入存档 |
| 存档 | `GetSaveData` / `LoadSaveData`（只重建永久层）/ `ResetAll`（清两层） |

> `SetProvidedSkills` 的「整组并集替换」语义天然满足两条常见业务规则：**当另一个提供者仍在提供某共享技能时，卸下一件装备不会移除它**；且**永久学会的技能永不被提供者层影响**。`UseSkill` + `OnSkillUsed` 只做派发，技能**效果**由业务层订阅事件自行实现（本包不内置效果执行）。

---

## UI 组件

位于 `Runtime/UI/`，独立程序集 `Ale.Chronicle.Runtime.UI`（命名空间 `Ale.Chronicle.Runtime.UI`），依赖 toolkit 的虚拟滚动引擎与通用控件：

- **`UiwSkillView`**（`UiwViewBase`）：技能主界面——标题 + 搜索 + 主/副分组标签 AND 过滤页签 + 网格/顺序双视图；来源可在 `Database`（目录）与 `Character`（已学，含提供者）之间切换，订阅 `OnLearnedChanged` 自动刷新。
- **`UiwSkillGridList` / `UiwSkillOrderList`**：技能虚拟网格 / 单列列表（继承 toolkit `UiwVirtualGridList` / `UiwVirtualOrderList`）。
- **`UiwSkillEntry`**（`UiwHoverTooltipSource`）：技能格（图标 / 名称 / 阶级背景 + 可选描述与自定义字段行），悬停触发全局 Tooltip。
- **`UiwSkillTooltip`**（`UiwTooltipBase<Skill>`, `ISkillTooltip`）：全局悬停详情弹窗。
- **`SkillCollector`**（静态）：`Collect(ESkillSource, configId)`——`Database` → 全部注册技能；`Character` → 该角色有效技能。
- 文本解析 `UiwSkillText`、阶级解析 `SkillRankUtil`（本地化优先）。

主机 `ChronicleRuntimeManager`（`ToolkitMonoSingleton`，场景内 Mono 单例）统一管理**覆盖式 UI 根节点 / Layer** 与**全局技能悬停 Tooltip**（经 `ISkillTooltip` 接口持有，避免核心运行时反向依赖 UI 程序集）。

---

## 运行时与序列化

- **`ChronicleDataManager`**（`ToolkitSingleton`，非 Mono）：注册一个 / 多个 `ChronicleDatabase`，提供跨库 O(1) 惰性字典查询（`GetSkill` / `GetSkillTree` / `GetProfession` / `GetTitle` / `GetRankLadder` / `GetAllSkills` / `GetAllSkillTrees` / `GetAllProfessions` / `GetAllTitles` / `GetAllRankLadders` / …）；id 冲突「先注册者优先」；`Register` / `Unregister` / `ClearDatabases` / `LoadFromBinary` / `InvalidateIndex`。
- **`ChronicleRuntimeManager`**（`ToolkitMonoSingleton`，Mono）：唯一运行时 Mono 主机——覆盖式 UI 与技能 Tooltip 宿主。
- **`SkillRuntimeManager`**（`ToolkitSingleton`，非 Mono，`ISaveable`）：见上。
- **`ProfessionRuntimeManager`**（`ToolkitSingleton`，非 Mono，`ISaveable`）：每角色职业进度（等级 / 经验 / 主职业）；`AddExp` 按 `ExpCurve` 升级 + 施加 `LevelUnlock`；存档持进度。
- **`TitleRuntimeManager`**（`ToolkitSingleton`，非 Mono，`ISaveable`）：每角色持有头衔；阶级头衔「一序列一持有」晋升替换、唯一头衔易主；存档持持有。
- **二进制导出**：`ChronicleConfigSerializer` 把 `ChronicleDatabase` ↔ 紧凑二进制。魔数 `CHRO`、**当前格式 `Version = 6`**、`MinReadableVersion = 1`；按版本追加块（v2 加属性/特质模板 + 分组标签 + 数字格式 + 模板引用/属性值；v3 追加技能模板 + 技能；v4 追加 职业 / 转职树 / 头衔 / 阶级序列，及角色的职业 / 头衔 持有字段；v5 追加 职业模板 / 头衔模板 两块，并在职业 / 头衔块尾追加 `templateRef` + 自定义字段 `values`；**v6 追加 技能树 一块，并在职业块尾追加 `skillTreeRefs`、核心属性块尾追加 `conditionalModifiers`（条件修改值）**）——**append-only 向后兼容**，旧版本（含 v3 / v4 / v5）导出的二进制仍可导入。对象引用经 `IAssetRefResolver` 以 GUID 承载；特质 / 职业 / 头衔 / 技能树 / 属性条件修改的条件表达式以条件系统 JSON 存储。

---

## Chronicle × Inventory 整合（Demo）

编年史与[仓库系统 `com.ale.inventory`](https://github.com/AleFeng/unity-ale-inventory-system) 可**协同使用**：让装备系统里的道具「持有 / 触发」编年史技能。**两个包互不依赖**——整合胶水只用道具的通用 `AttributeValue`（存 Chronicle 技能 id 的纯 String）+ 装备 / 仓库管理器，**从不引用对方的领域类型**。

演示位于工程 `Assets/DemoInventory/`（命名空间 `Ale.Chronicle.Inventory`，编入 `Assembly-CSharp`，是唯一同时引用两包的地方；依赖 `com.ale.inventory`）：

- **装备「持有」技能**（`EquipmentSkillBridge`）：订阅 `EquipmentRuntimeManager.OnEquipmentChanged`，把已装备道具「技能」属性里的技能 id 取并集，作为一个提供者推入 `SetProvidedSkills(角色, "equipment", …)`。卸下时并集重算——**别的装备仍提供则保留，永不动永久层**。
- **消耗品「触发」技能**（`ConsumableSkillUse`）：读道具「使用技能」属性 → 对指定角色 `UseSkill` → 至少一次施放成功则扣 1 个（`InventoryRuntimeManager.TryRemoveItemById`）。治疗药水 / 魔法卷轴即此类。
- **效果监听示例**（`SkillEffectDemoListener`）：订阅 `OnSkillUsed`，打印占位效果。
- **一键驱动**（`EquipmentSkillDemo`）：自包含 IMGUI 驱动，代码内建技能与道具数据，现场演示装备/卸下/使用 + 永久学会/遗忘，实时显示有效技能与施放日志（含「多来源保留」「不删永久」验证）。

---

## 依赖

> ⚠️ **本插件依赖通用底层包 [`com.ale.toolkit`](../com.ale.toolkit)（其中已内置 `Ale.Condition` 条件系统），必须先装它、再装本插件。** Unity Package Manager 不支持在 `package.json` 的 `dependencies` 里写 git URL，故 `dependencies` 留空——**顺序不能颠倒**，否则会报 `找不到 Ale.Toolkit.* / Ale.Condition.*` 一类编译错。

- **`com.ale.toolkit`（必需，先安装；建议 1.4.0 或更新，需含 `Ale.Condition`）** —— 属性系统 / 虚拟滚动列表 / 编辑器三列框架 / 编辑器界面多语言 / 序列化基元 / 条件系统。
- Unity 2022.3+（`package.json` 声明的最低版本；本仓库基于 `Unity 6000.3` 开发与维护）。
- TextMeshPro（可选，`ATK_TMP` 宏）、Unity Localization（可选，`ATK_LOCALIZATION` 宏）、Unity Addressables（可选，`ATK_ADDRESSABLE` 宏）。
- `com.ale.inventory`（**仅整合 Demo 需要**，核心包不依赖）。

> 三个可选宏均为项目级全局设定，在 **Ale Toolkit 欢迎窗口**（`Tools > Ale Toolkit > Welcome`）一键开关并检测对应包是否安装。

---

## 快速开始

### 1. 创建数据文件
```
Project 面板右键 > Create > ChronicleSystem > Chronicle Database
```

### 2. 打开编辑器并配置
- 选中 `.asset`，在 Inspector 顶部点「在 Chronicle Editor 中编辑」；或菜单 `Tools > Ale Toolkit > Chronicle System > Chronicle Editor`。
- 编辑器为**顶部系统页签 + 三列布局**（左：模板 / 转职树 / 阶级序列，中：条目列表，右：详细 Inspector），页签依次为 **角色 / 属性 / 特质 / 技能 / 职业 / 头衔 / 通用**（「通用」内含 枚举 / 功能标签 / 分组标签 / 数字格式 子页签）。含实时重复 ID 检查、角色属性合流活预览。

### 3. 导出（可选）
工具栏「导出二进制」（校验通过、无非空重复 ID 时可用）。编辑器始终在 ScriptableObject 上工作，二进制为单向格式。

### 4. 运行时接入
```csharp
using Ale.Chronicle;

// 注册配置数据库（或 ChronicleDataManager.Instance.LoadFromBinary(bytes) 从导出的二进制加载）
ChronicleDataManager.Instance.Register(chronicleDatabase);

// 查询
Skill skill = ChronicleDataManager.Instance.GetSkill("fireball");

// 永久学会 / 遗忘
SkillRuntimeManager.Instance.Learn("hero", "fireball");

// 外部来源（如装备）成组提供技能（并集重算，不动永久层）
SkillRuntimeManager.Instance.SetProvidedSkills("hero", "equipment", new[] { "guard" });

// 一次性使用 / 施放（无状态，派发 OnSkillUsed 事件）
SkillRuntimeManager.Instance.UseSkill("hero", "heal", sourceKey: "potion");

// 有效技能 = 永久 ∪ 全部提供者
var effective = SkillRuntimeManager.Instance.GetEffectiveSkills("hero");

// 存档 / 读档（仅持久化永久层；提供者层读档后由业务层重算）
var save = SkillRuntimeManager.Instance.GetSaveData();
SkillRuntimeManager.Instance.LoadSaveData(save);

// 职业：习得 + 加经验（按 ExpCurve 升级、达阈值施加等级解锁）
ProfessionRuntimeManager.Instance.Learn("hero", "warrior", primary: true);
ProfessionRuntimeManager.Instance.AddExp("hero", "warrior", 100);

// 头衔：授予（阶级头衔按阶梯晋升替换、唯一头衔从他人剥夺）
TitleRuntimeManager.Instance.Grant("hero", "duke", worldDay: 0);
```

### 5. 一键 Demo
菜单 `Tools > Ale Toolkit > Chronicle System > Demo Wizard` 一键生成技能 UI 预制体；工程内另有 `Assets/Demo/`（技能 UI 演示场景）与 `Assets/DemoInventory/`（Chronicle × Inventory 整合演示）。

---

## 本地化

包内 UI 文本经 toolkit `AttributeValue.ResolveText()` 解析，启用 `ATK_LOCALIZATION` 时接 Unity Localization（本地化优先、取不到回退纯文本）。演示的字符串表 `Assets/Demo/Localization/ChronicleSystem` 覆盖 **7 种 Locale**（en / fr / ja / ko / ru / zh-Hans / zh-Hant），并配套 CJK 字体。`LocalizedTextEvent` / `LocalizedFontEvent` 仅由 Demo Wizard 在生成预制体时挂载（`ATK_TMP && ATK_LOCALIZATION` 门控）。

---

## 目录结构

```
Packages/com.ale.chronicle/          ← 包根
├── package.json  LICENSE.md  README.md  CHANGELOG.md
├── Runtime/                          程序集 Ale.Chronicle.Runtime（命名空间 Ale.Chronicle）
│   ├── Attribute/     核心属性 定义 / 模板 / 值
│   ├── Character/     角色 定义 / 模板 / 身份字段常量 / Schema 源接口
│   ├── Condition/     Ale.Condition 整合（比较算子 / 作用域 / 七个求值器）
│   ├── Database/      ChronicleDatabase（中心配置 ScriptableObject）
│   ├── Manager/       DataManager / RuntimeManager / Skill·Profession·Title RuntimeManager
│   ├── Modifier/      CoreAttributeResolver（属性合流）
│   ├── Profession/    职业 定义 / ExpCurve / 转职树 / 角色职业状态 / 运行时状态
│   ├── Serialization/ 二进制序列化 + DTO
│   ├── Skill/         Skill / SkillTemplate / 技能树 / ISkillConfig / 已学状态 / SkillUseEvent
│   ├── Tagging/       ChronicleGroupTag（分组标签）
│   ├── Title/         头衔 定义 / 阶级序列 / 角色头衔 / 运行时状态
│   └── Trait/         特质 定义 / 模板 / 实例 / 生命周期 / AI 权重 / 兼容
├── Runtime/UI/                       程序集 Ale.Chronicle.Runtime.UI（技能 UI 组件）
├── Editor/                           程序集 Ale.Chronicle.Editor（三列编辑器 + 七页签）
│   ├── Common/  Drawers/  Inspectors/  Tabs/
└── Docs~/                            （预留）
```

---

## 测试

工程 `Assets/Tests/`（程序集 `Ale.Chronicle.Tests`，EditMode NUnit）含 20 个测试文件，覆盖数据库 / 数据管理器 / 二进制序列化（含 v6 往返、模板 + 自定义字段往返、旧 v3 / v4 / v5 兼容）/ 属性合流（含**条件修改值按条件过滤**）/ 条件求值 / 特质 / 模板层 / 角色组合 / 技能数据 / 技能运行时 / 技能 UI / 职业与头衔（ExpCurve 三模式 / 数据库校验含成环 / 汇流 / 条件判定器 / 运行时管理器）/ **技能树（v6 序列化往返 / 三类型 / 技能点获取 · 前置成环校验）**。

---

## 许可

本项目基于 [MIT License](LICENSE.md) 开源，可自由用于商业与非商业项目。

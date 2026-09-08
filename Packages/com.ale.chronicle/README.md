# 编年史系统（Chronicle System）

<p align="center">
  🌍
  中文
</p>

面向设计师的 Unity 数据驱动**角色 / 人生模拟**配置系统。用一个 `ChronicleDatabase` 资产集中配置 **角色 / 核心属性 / 特质 / 技能 / 职业 / 头衔** 六大领域，以及配套的 **枚举类型 / 功能标签 / 分组标签 / 数字格式**；动态运行时状态（已学技能、外部来源提供的技能、职业等级 / 经验、持有头衔、核心属性合流结果等）由对应的运行时管理器维护。

构建于通用底层包 [`com.ale.toolkit`](../com.ale.toolkit) 之上，直接复用它的 **Schema 属性引擎（`AttributeOwner` / `AttributeValue`）**、**编辑器三列框架**、**虚拟滚动列表**、**序列化基元**、**`Ale.Condition` 条件系统**、**`Ale.Effect` 效果系统（GAS 层）**、**共用效果库 `EffectDatabase` / Effect Editor**（`0.5.0` 起效果在此配置）与 **`Ale.GameplayTags` 层级标签**。

- 编辑器始终且仅在 ScriptableObject 上工作，全程支持 Undo / Redo；二进制为**单向导出**格式。
- 角色 / 核心属性 / 特质 / 技能等实体统一走 toolkit 的**灵活属性系统**，无需改代码即可扩展字段。
- 特质「获得条件」接入 `Ale.Condition`（年龄 / 核心属性比较 / 是否拥有某特质）；`0.6.0` 起条件里的各类 id 在编辑器中是**分组下拉**而非手打字符串。
- 核心属性走 **基础值 + 修正器合流**（来源含特质 / 职业成长 / 头衔 / 条件修改值 / 活动效果 / 效果永久落地），带逐来源拆解。
- 效果存放在 toolkit 的**共用效果库 `EffectDatabase`**（`0.5.0` 起；Effect Editor 一处配置、所有上层系统按 id 引用），走 **GAS 式 `EffectDefinition`**（时长 / 周期 / 叠加 / Gameplay 标签 / 施加条件 / 修饰器 / 执行阶段），技能「使用」按序施加；运行时经 `EffectRuntimeManager` 落到属性汇流、特质、头衔、职业经验、技能。
- 文本本地化（Unity Localization）、TextMeshPro、Addressable 均通过编译宏可选启用（与 toolkit 统一）。

> ⚠️ **当前版本 `0.6.0`**：角色 / 属性 / 特质 / 技能 / 职业 / 头衔 六大领域 + **效果**领域的配置与运行时数据基础已可用（`0.6.0` 条件参数里的 属性 / 特质 / 头衔 / 职业 / 阶级序列 id 由裸文本框改为**分组下拉**（判定器 schema 标注 `catalogRef` + 编辑器侧登记候选提供者，接入 toolkit 1.12.0 的 `ConditionDrawerHooks`）；`0.5.0` 效果与 Gameplay 标签外移至 toolkit 1.10.0 的**共用效果库**（Effect Editor 一处配置、跨系统按 id 引用，附一键迁移），编年史库只保留技能的效果 id 引用；`0.4.0` 接入 toolkit 的 **GAS 式效果系统与 Gameplay 标签**：技能「使用」施加效果，效果落到属性 / 特质 / 头衔 / 职业 / 技能，新增世界时钟与运行时特质；`0.3.1` 新增运行时**角色信息面板 `UiwCharacterView`** 与「角色系统」演示 Sample；`0.3.0` 技能新增**技能树**、属性新增**按条件修改值**、职业可**关联技能树**）；「百万级人生模拟」中的世代推进、角色随机生成规则（`CharacterTemplate` 的种族 / 保底特质 / 属性点预算等）、继承结算（`TitleDefinition.heritable` / `successionPolicyRef`）、派生属性与身体机能修正目标（`EModifierTargetKind` 的部分取值）等尚为**预留**、暂未接入求值。下文只描述**已实现**能力。

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
| **效果系统**（`0.4.0`；`0.5.0` 效果外移） | 效果（toolkit GAS 定义：时长 / 周期 / 叠加 / Gameplay 标签 / 施加条件 / 修饰器 / 执行阶段）与 Gameplay 标签在 toolkit **共用效果库 `EffectDatabase`**（Effect Editor 配置）；本库只保留技能 `onUseEffectRefs`（效果 id 引用 + 跳转） | `EffectRuntimeManager`（每角色效果容器 + 永久落地）+ `ChronicleClock`（世界日推进）+ `TraitRuntimeManager`（运行时特质）；`ChronicleCharacterRuntime` 运行时汇流；执行器 `Chronicle.*` |
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

### 效果系统（Effect，`0.4.0`；`0.5.0` 效果外移至 toolkit 共用效果库）

- **效果在哪**：`0.5.0` 起效果条目（显示名 / 描述 / 图标 + 模板驱动的自定义属性 + toolkit `EffectDefinition`——GAS 式 GameplayEffect：时长策略 瞬时 / 持续 / 无限、周期、叠加类型与上限 / 刷新 / 到期策略、资产 / 授予 / 移除 / 免疫标签、施加 / 持续标签要求、施加条件、概率、修饰器（幅度 可缩放 / 基于属性 / 调用方给定）、按阶段执行 `onApply / onStack / onPeriod / onExpire / onRemove`、线索标签）与 Gameplay 标签存放在 toolkit 的共用效果库 **`EffectDatabase`**，在 **Effect Editor**（`Tools > Ale Toolkit > Effect System > Effect Editor`）一处配置，所有上层系统（Chronicle / Inventory …）按 id 引用；运行时经 `EffectDataManager.Instance.Register(effectDatabase)`（或放 `Resources` 随启动自动登记）进入全局效果注册表。编年史库**不再**持有效果 / 标签——0.4.0 资产里的数据落入隐藏 legacy 字段，见下文「迁移」。
- **引用**：`Skill.onUseEffectRefs`——技能「使用」时按序对目标施加；技能 Inspector 用 toolkit `EditorEffectRefListDrawer`：「+」从工程内全部效果库的目录选择（按库分组）、拖拽重排、「打开」跳转到 Effect Editor 并定位、未找到仅标注不阻断、可自由输入 id；道具「使用」同理（见 `com.ale.inventory`）。
- **执行器 `Chronicle.*`**（类别「角色」，在效果定义的「执行」里选用）：`GrantTrait`（特质 / 天数：0 按定义、>0 指定、<0 永久 / 层数）、`RevokeTrait`、`GrantTitle`、`RevokeTitle`、`AddProfessionExp`、`LearnSkill`、`ForgetSkill`。属性增减不做执行器，由定义的修饰器承担：持续效果参与汇流；瞬时 / 周期效果**永久落地**（来源 `effect:{id}#句柄`，可追溯、不改配置基础值）。效果本质是「对某个系统的操作」，新系统只需实现自己的 `[EffectExecutor]` 执行器即可复用同一效果库。
- **属性 id 候选**：`ChronicleEffectAttributeProvider`（`[InitializeOnLoad]`）向 toolkit 效果定义绘制器登记系统「Chronicle」的核心属性（扫描工程内全部 `ChronicleDatabase` 资产），Effect Editor 里修饰器 / 属性幅度的属性 id 下拉可直接选编年史属性。
- **迁移（0.4.0 → 0.5.0）**：`Tools > Ale Toolkit > Chronicle System > 迁移效果到 Effect Database`（资产 Inspector 检测到 legacy 数据时也给出入口）——`ChronicleLegacyEffects.MigrateInto(源编年史库, 目标效果库)` 逐条搬入（显示字段 / 定义深拷贝，不挂模板）并清空 legacy；目标库已有同 id 的跳过并报告、不覆盖（条目留在 legacy，处理后可重跑）；标签按名称去重并入。
- **校验**：本库不再校验效果 / 标签，也不把 `onUseEffectRefs` 作悬空校验（效果在其它库、运行时按 id 解析）；效果库自身的校验（id / 模板 / 枚举重复、定义错误、非法标签名）在 Effect Editor。

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
| 一次性使用 | `UseSkill(target, skillId, sourceKey = null, sourceCharacterId = null)`——校验技能存在后，按序对目标施加 `Skill.onUseEffectRefs`（经 `EffectRuntimeManager`），再派发 `OnSkillUsed(SkillUseEvent)`（含 `SourceCharacterId` / 逐效果 `EffectResults` / `EffectsApplied`）返回 true；不存在则不派发返回 false。不触发 `OnLearnedChanged`、自身不入存档（施加的效果由效果运行时存档） |
| 存档 | `GetSaveData` / `LoadSaveData`（只重建永久层）/ `ResetAll`（清两层） |

> `SetProvidedSkills` 的「整组并集替换」语义天然满足两条常见业务规则：**当另一个提供者仍在提供某共享技能时，卸下一件装备不会移除它**；且**永久学会的技能永不被提供者层影响**。`UseSkill` 的效果由技能引用的效果定义决定（`0.4.0`）；无引用的技能仍只派发事件，业务层可订阅自行处理。

---

## UI 组件

位于 `Runtime/UI/`，独立程序集 `Ale.Chronicle.Runtime.UI`（命名空间 `Ale.Chronicle.Runtime.UI`），依赖 toolkit 的虚拟滚动引擎与通用控件：

- **`UiwCharacterView`**（`UiwViewBase`，`0.3.1`；`0.4.0` 接入运行时）：**角色信息面板**——从 `ChronicleDataManager` 取一个 `CharacterDefinition`，按「配置 ∪ 运行时」分区展示 **个人档案**（姓名 / 性别 / 年龄 / 身高 / 体重 / 三围 / 血型 / 兴趣，枚举经数据管理器直解）、**能力**（6 项核心属性经 `ChronicleCharacterRuntime` 求「基础 → 当前」+ 逐来源明细，含活动效果与永久落地）、**特质**（临时特质附剩余时长 / 层数）、**效果**（活动效果：策略 / 剩余 / 周期 / 层数 / 抑制 / 修饰器 + 永久落地汇总；无独立节点时并入特质卡）、**职业（等级 + 主职业）/ 头衔（阶级头衔附阶级序列位次）/ 技能（职业关联技能树导出 + 已学）**；打开期间订阅各运行时管理器与世界时钟事件，同帧合并刷新；年龄按 `ChronicleClock` 世界日推算（`useWorldClock`）。TMP 富文本信息卡排版，随内容自适应高度，无额外美术依赖。
- **`UiwSkillView`**（`UiwViewBase`）：技能主界面——标题 + 搜索 + 主/副分组标签 AND 过滤页签 + 网格/顺序双视图；来源可在 `Database`（目录）与 `Character`（已学，含提供者）之间切换，订阅 `OnLearnedChanged` 自动刷新。
- **`UiwSkillGridList` / `UiwSkillOrderList`**：技能虚拟网格 / 单列列表（继承 toolkit `UiwVirtualGridList` / `UiwVirtualOrderList`）。
- **`UiwSkillEntry`**（`UiwHoverTooltipSource`）：技能格（图标 / 名称 / 阶级背景 + 可选描述与自定义字段行），悬停触发全局 Tooltip。
- **`UiwSkillTooltip`**（`UiwTooltipBase<Skill>`, `ISkillTooltip`）：全局悬停详情弹窗。
- **`SkillCollector`**（静态）：`Collect(ESkillSource, configId)`——`Database` → 全部注册技能；`Character` → 该角色有效技能。
- 文本解析 `UiwSkillText`、阶级解析 `SkillRankUtil`（本地化优先）。

主机 `ChronicleRuntimeManager`（`ToolkitMonoSingleton`，场景内 Mono 单例）统一管理**覆盖式 UI 根节点 / Layer** 与**全局技能悬停 Tooltip**（经 `ISkillTooltip` 接口持有，避免核心运行时反向依赖 UI 程序集）。

---

## 运行时与序列化

- **`ChronicleDataManager`**（`ToolkitSingleton`，非 Mono）：注册一个 / 多个 `ChronicleDatabase`，提供跨库 O(1) 惰性字典查询（`GetSkill` / `GetSkillTree` / `GetProfession` / `GetTitle` / `GetRankLadder` / `GetAllSkills` / `GetAllSkillTrees` / `GetAllProfessions` / `GetAllTitles` / `GetAllRankLadders` / …）；id 冲突「先注册者优先」；`Register` / `Unregister` / `ClearDatabases` / `LoadFromBinary` / `InvalidateIndex`。`0.5.0` 起不再登记为 toolkit 效果定义源、不再并入 Gameplay 标签（效果经 toolkit `EffectDataManager`）。
- **`ChronicleRuntimeManager`**（`ToolkitMonoSingleton`，Mono）：唯一运行时 Mono 主机——覆盖式 UI 与技能 Tooltip 宿主。
- **`SkillRuntimeManager`**（`ToolkitSingleton`，非 Mono，`ISaveable`）：见上。
- **`ProfessionRuntimeManager`**（`ToolkitSingleton`，非 Mono，`ISaveable`）：每角色职业进度（等级 / 经验 / 主职业）；`AddExp` 按 `ExpCurve` 升级 + 施加 `LevelUnlock`；存档持进度。
- **`TitleRuntimeManager`**（`ToolkitSingleton`，非 Mono，`ISaveable`）：每角色持有头衔；阶级头衔「一序列一持有」晋升替换、唯一头衔易主；存档持持有。
- **`ChronicleClock`**（`ToolkitSingleton`，`ISaveable`，`0.4.0`）：世界日时钟；`AdvanceDays` / `AdvanceYears` 依次驱动特质到期、效果周期 / 到期，派发 `OnDaysAdvanced`。
- **`TraitRuntimeManager`**（`ToolkitSingleton`，`ISaveable`，`0.4.0`）：运行期授予的特质（`Grant` / `Revoke` / `RevokeBySource` / `Has` / `GetEffectiveTraits` / `Tick`）；互斥 / 等价组拒绝、临时到期、重复授予按 `durationStacksRefresh` 刷新 + 叠层；与配置层合成有效特质。
- **`EffectRuntimeManager`**（`ToolkitSingleton`，`ISaveable`，`0.4.0`）：每角色一个 toolkit `EffectContainer` + 永久落地修饰器列表；`Apply` / `Remove` / `RemoveById` / `RemoveBySourceTag` / `RemoveWithTag(s)` / `RemoveAll` / `ClearCharacter` / `Tick` / `GetActiveEffects` / `GetPermanentModifiers` / `CollectModifiers`；实现 toolkit 的 `IEffectContainerSource` / `IEffectAttributeSink` / `IEffectAttributeSource` / `IGameplayTagSource`；`CueSink` 可挂线索落地（演示用 `ChronicleDebugCueSink`）。
- **`ChronicleCharacterRuntime`**（静态，`0.4.0`）：运行时属性汇流 `Evaluate(characterId, attrId)`——特质 / 职业成长 / 头衔各取配置 ∪ 运行时，+ 条件修改值（条件经 `ChronicleEffectContext` 求值、重入防护）+ 活动效果修饰器（未抑制、非周期）+ 永久落地；另提供 `HasProfession` / `GetProfessionLevel` / `HasTitle` / `GetHighestRankTier` 等有效状态查询。
- **`ChronicleEffectContext.Create(target, source)`** / **`ChronicleRuntimeConditionSource`**（`0.4.0`）：组装效果 / 条件上下文（主体 = 目标、对象 = 来源、父 / 母经角色定义反查），注册定义源（toolkit `EffectDataManager`，未命中回退全局注册表）/ 容器源 / 属性读写 / 标签源 / 执行器落地门面 `IChronicleRuntimeSink`；业务层直接调 toolkit `EffectApplier` 时可复用。
- **二进制导出**：`ChronicleConfigSerializer` 把 `ChronicleDatabase` ↔ 紧凑二进制。魔数 `CHRO`、**当前格式 `Version = 8`**、`MinReadableVersion = 1`；按版本追加块（v2 加属性/特质模板 + 分组标签 + 数字格式 + 模板引用/属性值；v3 追加技能模板 + 技能；v4 追加 职业 / 转职树 / 头衔 / 阶级序列，及角色的职业 / 头衔 持有字段；v5 追加 职业模板 / 头衔模板 两块，并在职业 / 头衔块尾追加 `templateRef` + 自定义字段 `values`；**v6 追加 技能树 一块，并在职业块尾追加 `skillTreeRefs`、核心属性块尾追加 `conditionalModifiers`（条件修改值）；v7 追加 效果 / Gameplay 标签 两块（效果定义以 Effect System JSON 串承载），并在技能块尾追加 `onUseEffectRefs`；v8 不再写出效果 / Gameplay 标签两块（外移至 toolkit 效果库，由 `EffectConfigSerializer` 单独导出），读 v7 文件时两块读入 legacy 字段供迁移**）——**append-only 向后兼容**，旧版本（含 v3 ~ v7）导出的二进制仍可导入。对象引用经 `IAssetRefResolver` 以 GUID 承载；特质 / 职业 / 头衔 / 技能树 / 属性条件修改的条件表达式以条件系统 JSON 存储，效果定义以效果系统 JSON 存储。

---

## Chronicle × Inventory 整合（Demo）

编年史与[仓库系统 `com.ale.inventory`](https://github.com/AleFeng/unity-ale-inventory-system) 可**协同使用**：让装备系统里的道具「持有」编年史技能、消耗品「使用」时施加编年史效果。**两个包互不依赖**——装备桥只用道具的通用 `AttributeValue`（存 Chronicle 技能 id 的纯 String）；道具使用走 toolkit 效果契约（`Item.onUseEffectRefs` + `UseItem` + `IEffectContext`），**从不引用对方的领域类型**。

演示位于工程 `Assets/DemoInventory/`（命名空间 `Ale.Chronicle.Inventory`，编入 `Assembly-CSharp`，是唯一同时引用两包的地方；依赖 `com.ale.inventory` ≥ 1.13.0）：

- **装备「持有」技能**（`EquipmentSkillBridge`）：订阅 `EquipmentRuntimeManager.OnEquipmentChanged`，把已装备道具「技能」属性里的技能 id 取并集，作为一个提供者推入 `SetProvidedSkills(角色, "equipment", …)`。卸下时并集重算——**别的装备仍提供则保留，永不动永久层**。
- **消耗品「使用」施加效果**（`ConsumableEffectUse`，`0.4.0`）：`InventoryRuntimeManager.UseItem(背包, 道具, ChronicleEffectContext.Create(目标角色))`——道具的 `onUseEffectRefs` 引用 toolkit 共用效果库里的效果 id（`0.5.0` / Inventory `1.13.0` 起两包都不再自持效果：回复药水 → `regen_draught`：5 天内每天耐力 +5 永久落地；磨刀油 → `sharpen_oil`：3 天 战力 +3，同目标上限 1 层刷新），定义先经上下文的 toolkit `EffectDataManager` 定义源、再经全局 `EffectDefinitionRegistry.Default` 按 id 解析（效果库注册即登记为来源，任何系统定义的效果都可引用）；至少一个效果施加成功才扣 1 个，无使用效果的道具不扣减。
- **结果监听示例**（`SkillEffectDemoListener`）：订阅 `SkillRuntimeManager.OnSkillUsed` 与 `InventoryRuntimeManager.OnItemUsed`，打印逐效果施加结果。
- **一键驱动**（`EquipmentSkillDemo`）：自包含 IMGUI 驱动，代码内建 toolkit 效果库（回复药剂 / 磨刀）与技能 / 道具数据，现场演示装备/卸下/使用 + 永久学会/遗忘 + 推进世界时钟，实时显示有效技能、角色耐力 / 战力、活动效果与日志（含「多来源保留」「不删永久」「无使用效果不扣减」验证）。

---

## 依赖

> ⚠️ **本插件依赖通用底层包 [`com.ale.toolkit`](../com.ale.toolkit)（其中已内置 `Ale.Condition` 条件系统），必须先装它、再装本插件。** Unity Package Manager 不支持在 `package.json` 的 `dependencies` 里写 git URL，故 `dependencies` 留空——**顺序不能颠倒**，否则会报 `找不到 Ale.Toolkit.* / Ale.Condition.* / Ale.Effect.*` 一类编译错。

- **`com.ale.toolkit`（必需，先安装；`0.6.0` 起最低 1.12.0——需含共用效果库 `EffectDatabase` / Effect Editor 与条件参数候选注入点 `ConditionDrawerHooks`，及 `Ale.Condition` / `Ale.Effect` / `Ale.GameplayTags` / `Ale.Modifier.Core`）** —— 属性系统 / 虚拟滚动列表 / 编辑器三列框架 / 编辑器界面多语言 / 序列化基元 / 条件系统 / 效果系统 / 层级标签 / 修饰器。
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
- 编辑器为**顶部系统页签 + 三列布局**（左：模板 / 转职树 / 阶级序列，中：条目列表，右：详细 Inspector），页签依次为 **通用 / 角色 / 属性 / 特质 / 职业 / 技能 / 头衔**（「通用」内含 枚举 / 功能标签 / 分组标签 / 数字格式 子页签）。含实时重复 ID 检查、角色属性合流活预览。**效果 / Gameplay 标签**在 toolkit 的 Effect Editor（`Tools > Ale Toolkit > Effect System > Effect Editor`）配置；技能 Inspector 的「使用时施加的效果」可从目录选择并一键「打开」跳转。

### 3. 导出（可选）
工具栏「导出二进制」（校验通过、无非空重复 ID 时可用）。编辑器始终在 ScriptableObject 上工作，二进制为单向格式。

### 4. 运行时接入
```csharp
using Ale.Chronicle;

// 注册配置数据库（或 ChronicleDataManager.Instance.LoadFromBinary(bytes) 从导出的二进制加载）
ChronicleDataManager.Instance.Register(chronicleDatabase);
// 注册 toolkit 效果库（0.5.0；放在 Resources 下则随启动自动登记）——技能 onUseEffectRefs 按 id 在此解析
EffectDataManager.Instance.Register(effectDatabase);

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

// 效果（0.4.0；0.5.0 起定义来自 toolkit 效果库）：技能使用按序施加 onUseEffectRefs；也可直接施加效果 / 推进世界时钟
SkillRuntimeManager.Instance.UseSkill("hero", "war_cry", sourceKey: "ui", sourceCharacterId: "hero");
EffectRuntimeManager.Instance.Apply("regen_draught", "hero");           // 直接施加（如道具使用）
ChronicleClock.Instance.AdvanceDays(30);                                 // 特质到期 / 效果周期与到期
float might = ChronicleCharacterRuntime.EvaluateValue("hero", "might");  // 运行时汇流（含活动效果 / 永久落地）
var active  = EffectRuntimeManager.Instance.GetActiveEffects("hero");    // 活动效果（剩余 / 层数 / 修饰器）
```

### 5. 一键 Demo
- **Sample 场景**：`CharacterSystemDemo` 演示场景（同屏 `UiwCharacterView` 角色信息面板 + `UiwSkillView` 技能界面，由代码全量生成的示例 `ChronicleDatabase` 驱动、含示例角色露娜）已作为 Sample 打包于 **`Samples~/Demo`**——在 Package Manager 本包详情页 **`Samples`** 区一键 `Import` 后打开场景 Play 即见。右侧「效果演示」按钮面板（`ChronicleEffectDemo`）：使用「精神篡改 / 战意 / 心智护盾」、饮用「回复药剂」、推进 30 天 / 1 年、重置——角色面板随之刷新（精神异常特质三年后自愈、战力 +10 三十天后回落、心智护盾阻断篡改、耐力逐日永久落地）。示例数据经 `Tools > Ale Toolkit > Chronicle System > Character Seeder` 的 D1~D7 逐步生成（D7 生成 toolkit 效果库 `Data/EffectDatabase.asset`：枚举「效果类别」+ 模板「通用」+ 4 标签 + 4 效果）。
- **Demo Wizard**：菜单 `Tools > Ale Toolkit > Chronicle System > Demo Wizard` 一键生成技能 UI 预制体。
- **整合演示**：`Assets/DemoInventory/`（Chronicle × Inventory 整合，需 `com.ale.inventory`）。

---

## 本地化

包内 UI 文本经 toolkit `AttributeValue.ResolveText()` 解析，启用 `ATK_LOCALIZATION` 时接 Unity Localization（本地化优先、取不到回退纯文本）。演示的字符串表 `Samples~/Demo/Localization/ChronicleSystem` 覆盖 **7 种 Locale**（en / fr / ja / ko / ru / zh-Hans / zh-Hant），并配套 CJK 字体。技能 UI 预制体由 Demo Wizard 生成时挂 `LocalizedTextEvent` / `LocalizedFontEvent`（`ATK_TMP && ATK_LOCALIZATION` 门控）；角色面板 `UiwCharacterView` 为全视图驱动文本，直接绑定 CJK 字体、不走本地化字体事件。

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
│   ├── Effect/        效果上下文 / 运行时条件源 / IChronicleRuntimeSink / Executors（Chronicle.* 执行器）/ 运行时状态 / legacy 迁移 ChronicleLegacyEffects（ChronicleEffect 已过时）
│   ├── Manager/       DataManager / RuntimeManager / Skill·Profession·Title·Trait·Effect RuntimeManager / ChronicleClock / ChronicleCharacterRuntime
│   ├── Modifier/      CoreAttributeResolver（属性合流）
│   ├── Profession/    职业 定义 / ExpCurve / 转职树 / 角色职业状态 / 运行时状态
│   ├── Serialization/ 二进制序列化 + DTO
│   ├── Skill/         Skill / SkillTemplate / 技能树 / ISkillConfig / 已学状态 / SkillUseEvent
│   ├── Tagging/       ChronicleGroupTag（分组标签）
│   ├── Title/         头衔 定义 / 阶级序列 / 角色头衔 / 运行时状态
│   └── Trait/         特质 定义 / 模板 / 实例 / 生命周期 / AI 权重 / 兼容 / 运行时状态
├── Runtime/UI/                       程序集 Ale.Chronicle.Runtime.UI（角色面板 UiwCharacterView + 技能 UI 组件）
├── Editor/                           程序集 Ale.Chronicle.Editor（三列编辑器 + 七页签 + 效果迁移窗口 + 属性 id provider）
│   ├── Common/  Drawers/  Inspectors/  Migration/  Tabs/
├── Docs~/                            （预留）
└── Samples~/Demo/                    「Chronicle 演示」Sample：CharacterSystemDemo 场景 + 角色/技能 UI 预制体 + 代码生成的示例数据库与 toolkit 效果库 + 本地化（经 package.json samples 声明，Package Manager 可导入）
```

---

## 测试

工程 `Assets/Tests/`（程序集 `Ale.Chronicle.Tests`，EditMode NUnit）含 23 个测试文件，覆盖数据库 / 数据管理器 / 二进制序列化（含 v6 往返、模板 + 自定义字段往返、旧 v3 / v4 / v5 兼容）/ 属性合流（含**条件修改值按条件过滤**）/ 条件求值 / 特质 / 模板层 / 角色组合 / 技能数据 / 技能运行时 / 技能 UI / 职业与头衔（ExpCurve 三模式 / 数据库校验含成环 / 汇流 / 条件判定器 / 运行时管理器）/ **技能树（v6 序列化往返 / 三类型 / 技能点获取 · 前置成环校验）** / **效果（`0.5.0`：v8 往返 · 旧 v7 效果读入 legacy · legacy → toolkit 效果库迁移（冲突 / 重跑 / 干跑）· 数据管理器不再作定义源 / 运行时特质 · 效果施加汇流到期 · 执行器 · 免疫 · 周期永久落地 · 存档往返 · UseSkill）**。

---

## 许可

本项目基于 [MIT License](LICENSE.md) 开源，可自由用于商业与非商业项目。

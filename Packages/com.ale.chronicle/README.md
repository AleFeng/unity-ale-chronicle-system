# 编年史系统（Chronicle System）

<p align="center">
  🌍
  中文
</p>

面向设计师的 Unity 数据驱动**角色 / 人生模拟**配置系统。用一个 `ChronicleDatabase` 资产集中配置 **角色 / 核心属性 / 特质 / 技能** 四大领域，以及配套的 **枚举类型 / 功能标签 / 分组标签 / 数字格式**；动态运行时状态（已学技能、外部来源提供的技能、核心属性合流结果等）由对应的运行时管理器维护。

构建于通用底层包 [`com.ale.toolkit`](../com.ale.toolkit) 之上，直接复用它的 **Schema 属性引擎（`AttributeOwner` / `AttributeValue`）**、**编辑器三列框架**、**虚拟滚动列表**、**序列化基元** 与 **`Ale.Condition` 条件系统**。

- 编辑器始终且仅在 ScriptableObject 上工作，全程支持 Undo / Redo；二进制为**单向导出**格式。
- 角色 / 核心属性 / 特质 / 技能等实体统一走 toolkit 的**灵活属性系统**，无需改代码即可扩展字段。
- 特质「获得条件」接入 `Ale.Condition`（年龄 / 核心属性比较 / 是否拥有某特质）。
- 核心属性走 **基础值 + 修正器合流**（当前来源为特质），带逐来源拆解。
- 文本本地化（Unity Localization）、TextMeshPro、Addressable 均通过编译宏可选启用（与 toolkit 统一）。

> ⚠️ **当前为初始基线版本 `0.1.0`**：角色 / 属性 / 特质 / 技能的配置与运行时数据基础已可用；「百万级人生模拟」中的世代推进、生成规则（`CharacterTemplate` 的种族 / 保底特质 / 属性点预算等字段）、派生属性与身体机能修正目标（`EModifierTargetKind` 的部分取值）等尚为**预留**、暂未接入求值。下文只描述**已实现**能力。

---

## 领域概览

| 领域 | 配置内容 | 运行时 |
|------|---------|--------|
| **角色系统** | 角色模板、角色（身份字段 + 核心属性基础值 + 特质实例 + 父/母/子家族指针） | `CharacterDefinition` 组合 + 属性合流 + `GetAge(worldDay)` |
| **属性系统** | 核心属性模板、核心属性（取值范围 / 分类枚举 / 默认基础值 / 图标） | `CoreAttributeResolver`（基础值 + 修正器合流，带来源拆解、min/max 钳制） |
| **特质系统** | 特质模板、特质（生命周期 / 修正器 / 硬互斥 / 软兼容 / 遗传 / AI 权重 / 获得条件） | `TraitDefinition.CollectModifiers` → 属性合流；条件求值经 `Ale.Condition` |
| **技能系统** | 技能模板、技能（显示名 / 描述 / 图标 / 主+副分组标签 / 自定义属性） | `SkillRuntimeManager`（永久学会 + 外部提供者 两层 + 一次性使用派发） |
| **通用（General）** | 枚举类型、功能标签、分组标签、数字格式 | 被上述领域引用（枚举下拉、字段成组、技能分组、数值格式化） |

> 全部实体统一采用 **模板（蓝图）+ 定义 / 实例** 模式，共享 toolkit 的 `AttributeOwner` Schema，`RebuildAttributes` 按 Schema 增补 / 移除字段，深拷贝 `Clone` 支持「以此为模板」。

---

## 各领域说明

### 角色系统（Character）
- **角色 = 三部分组合**：① 身份自由字段（Schema = 模板 ∪ 特质携带的功能标签）；② 核心属性基础值（`CoreAttributeValue` 列表）；③ 特质实例（`CharacterTraitInstance` 列表）。另含 `fatherRef` / `motherRef` / `childRefs` 家族指针。
- **稳定身份字段**：`WellKnownAttr` 固定 id——`name` / `birthday` / `sex` / `height` / `weight` / `health` / `fertility`，供业务层稳定读取。
- **年龄**：`GetAge(worldDay)` 由出生日与世界日推算。
- **模板生成规则字段**（种族 `raceRef`、保底特质、随机特质池、属性点预算、最小/最大出生年龄）已在 `CharacterTemplate` 定义，**当前预留、暂未消费**。

### 属性系统（Core Attribute）
- **核心属性**（力量 / 敏捷 / …）由 `CoreAttributeDefinition` 承载：显示名 / 缩写 / 描述（Text）、图标、分类枚举、取值范围 `minValue` / `maxValue`、默认基础值，以及自定义属性字段。
- **合流求值**：`CoreAttributeResolver.Evaluate(...)` 以 toolkit `ModifierStackEvaluator` 计算 `基础值 → 原始值 → 最终值`，返回 `ModifierEvaluation`（含逐来源 `Breakdown`），并按定义 min/max 钳制。高层重载直接从角色的特质收集修正器。
- **编辑器实时预览**：角色 Inspector 展示「基础 → 当前 + 逐来源拆解」的活预览。

### 特质系统（Trait）
- **生命周期**：`ETraitLifetime` 永久 / 临时；临时特质有默认持续天数、可配置叠加是否刷新时长；`CharacterTraitInstance.Ticked(days)` 推进、`remainingDays < 0` 表示永久。
- **修正器**：`modifiers`（toolkit `ModifierDefinition`）作用于核心属性——特质是当前属性合流的**主要来源**（`SourceTag() = "trait:{id}"`）。
- **互斥 / 兼容**：硬互斥（等价组 `groupEquivalenceRef` + 显式 `incompatibleTraitRefs`，`IsIncompatibleWith`）；软兼容 `TraitCompatibility`（`opinionDelta` 社交好感增减）。
- **遗传 / AI**：`genetic` / `inheritChance` / `birthChance` 与 `TraitAiWeight`（AI 权重轴，**预留**）。
- **获得条件**：`eligibility`（`Ale.Condition` 的 `ConditionExpression`），在编辑器内联表达式绘制器中配置，运行时经条件系统求值。

### 技能系统（Skill）
- **技能目录条目** `Skill`：显示名 / 描述（Text，带本地化 fallback）、`iconValue`（Sprite / Addressable）、模板引用、主分组标签 + 副分组标签、自定义属性；`SkillTemplate` 为其蓝图（默认显示信息 + 字段 Schema，用于筛选 / 创建）。`ISkillConfig` 让二者共用同一套编辑器绘制器。
- 运行时的**已学 / 提供 / 使用**语义见下文 [技能运行时](#技能运行时两层模型--一次性使用)。

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

- **`ChronicleDataManager`**（`ToolkitSingleton`，非 Mono）：注册一个 / 多个 `ChronicleDatabase`，提供跨库 O(1) 惰性字典查询（`GetSkill` / `GetAllSkills` / `GetAllGroupTags` / …）；id 冲突「先注册者优先」；`Register` / `Unregister` / `ClearDatabases` / `LoadFromBinary` / `InvalidateIndex`。
- **`ChronicleRuntimeManager`**（`ToolkitMonoSingleton`，Mono）：唯一运行时 Mono 主机——覆盖式 UI 与技能 Tooltip 宿主。
- **`SkillRuntimeManager`**（`ToolkitSingleton`，非 Mono，`ISaveable`）：见上。
- **二进制导出**：`ChronicleConfigSerializer` 把 `ChronicleDatabase` ↔ 紧凑二进制。魔数 `CHRO`、**当前格式 `Version = 3`**、`MinReadableVersion = 1`；按版本追加块（v2 加属性/特质模板 + 分组标签 + 数字格式 + 模板引用/属性值；v3 追加技能模板 + 技能）——**向后兼容**，旧版本导出的二进制仍可导入。对象引用经 `IAssetRefResolver` 以 GUID 承载；特质获得条件以条件系统 JSON 存储。

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
- 编辑器为**顶部系统页签 + 三列布局**（左：模板 / 中：条目列表 / 右：详细 Inspector），页签依次为 **角色 / 属性 / 特质 / 技能 / 通用**（「通用」内含 枚举 / 功能标签 / 分组标签 / 数字格式 子页签）。含实时重复 ID 检查、角色属性合流活预览。

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
│   ├── Condition/     Ale.Condition 整合（比较算子 / 作用域 / 三个求值器）
│   ├── Database/      ChronicleDatabase（中心配置 ScriptableObject）
│   ├── Manager/       ChronicleDataManager / ChronicleRuntimeManager / SkillRuntimeManager
│   ├── Modifier/      CoreAttributeResolver（属性合流）
│   ├── Serialization/ 二进制序列化 + DTO
│   ├── Skill/         Skill / SkillTemplate / ISkillConfig / 已学状态 / SkillUseEvent
│   ├── Tagging/       ChronicleGroupTag（分组标签）
│   └── Trait/         特质 定义 / 模板 / 实例 / 生命周期 / AI 权重 / 兼容
├── Runtime/UI/                       程序集 Ale.Chronicle.Runtime.UI（技能 UI 组件）
├── Editor/                           程序集 Ale.Chronicle.Editor（三列编辑器 + 五页签）
│   ├── Common/  Drawers/  Inspectors/  Tabs/
└── Docs~/                            （预留）
```

---

## 测试

工程 `Assets/Tests/`（程序集 `Ale.Chronicle.Tests`，EditMode NUnit）含 12 个测试文件，覆盖数据库 / 数据管理器 / 二进制序列化 / 属性合流 / 条件求值 / 特质 / 模板层 / 角色组合 / 技能数据 / 技能运行时 / 技能 UI。

---

## 许可

本项目基于 [MIT License](LICENSE.md) 开源，可自由用于商业与非商业项目。

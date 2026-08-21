<p align="center">
  <img alt="Version" src="https://img.shields.io/badge/version-0.3.1-orange">
  <img alt="Unity Version" src="https://img.shields.io/badge/Unity-2022.3%2B-black?logo=unity">
  <img alt="Unity Version" src="https://img.shields.io/badge/Unity-6000.3%2B-black?logo=unity">
  <img alt="License" src="https://img.shields.io/badge/license-MIT-blueviolet">
</p>

<p align="center">
  🌍 中文
</p>

<p align="center">
  📥
  <a href="#-安装">安装</a> |
  <a href="#-快速开始">快速开始</a> |
  <a href="Packages/com.ale.chronicle/README.md">详细文档</a>
</p>

# Ale Chronicle System - 编年史系统
Ale Chronicle System 是一款面向 `Unity` 的**数据驱动角色 / 人生模拟配置系统插件**，把 **角色 / 核心属性 / 特质 / 技能 / 职业 / 头衔** 六大领域整合进同一套工具链。
它用一个 `ChronicleDatabase` 资产集中配置各领域的**静态定义数据**（角色 / 属性 / 特质 / 技能 / 职业 / 头衔，及配套的枚举 / 功能标签 / 分组标签 / 数字格式），配套**运行时管理器**维护动态状态（已学技能、外部来源提供的技能、职业等级 / 经验、持有头衔、核心属性合流结果等）。
面向**设计师**：编辑器始终且仅在 ScriptableObject 上工作，全程支持 Undo / Redo；二进制仅作为**单向导出**格式。构建于通用底层包 [`com.ale.toolkit`](Packages/com.ale.chronicle/README.md#依赖)（Schema 属性引擎 / 编辑器三列框架 / 虚拟滚动列表 / 序列化 / `Ale.Condition` 条件系统）之上，本插件包自身零硬依赖（TMP / Localization / Addressable 均经编译宏可选启用）。

> ⚠️ **当前版本 `0.3.1`**：角色 / 属性 / 特质 / 技能 / 职业 / 头衔 六大领域的配置与运行时数据基础已可用（`0.3.1` 新增运行时**角色信息面板 `UiwCharacterView`** 与「角色系统」演示 Sample；`0.3.0` 技能新增**技能树**、属性新增**按条件修改值**、职业可**关联技能树**）；「百万级人生模拟」中的世代推进、角色随机生成、继承结算、派生属性等尚为**预留**、暂未接入。

## 📜 目录
- [简介](#简介)
  - [项目特性](#项目特性)
  - [六大领域](#六大领域)
- [💻 环境要求](#-环境要求)
- [📦 安装](#-安装)
- [🚀 快速开始](#-快速开始)
- [🧩 可选宏开关](#-可选宏开关)
- [📖 详细文档](#-详细文档)
- [📁 目录结构](#-目录结构)
- [📄 许可](#-许可)

## 简介
「角色 + 属性 + 特质 + 技能 + 职业 + 头衔」是角色扮演 / 模拟经营 / 人生模拟类游戏的共同数据底座，但这些系统往往各自零散、互相耦合。Ale Chronicle System 把它们收拢到**同一份数据资产**与**同一套编辑器**下：

1. **集中配置** —— 一个 `ChronicleDatabase` 承载六大领域的全部静态定义，编辑器为「顶部系统页签 + 三列布局（模板 / 转职树 / 阶级序列 · 条目列表 · 详细 Inspector）」，支持搜索、拖拽、键盘导航、实时重复 ID 检查、角色属性合流活预览。
2. **灵活属性** —— 各实体的字段由 toolkit 的**灵活属性系统**（`AttributeOwner` + `AttributeValue`）承载，按功能标签成组增删，无需改代码即可扩展数据结构。
3. **模板 + 实例** —— 六大领域统一「模板（蓝图）+ 定义 / 实例」两层，Schema 自动对齐、深拷贝复用；职业 / 头衔在强类型字段之外，模板另承载可选自定义字段与默认预设，转职树 / 阶级序列 为额外组织职业 / 头衔的结构对象。
4. **统一合流 + 条件** —— 核心属性走「基础值 + 修正器合流」（来源含特质 / 职业每级成长 / 头衔加成 / **属性自身的按条件修改值**，带逐来源拆解）；特质 / 职业 / 头衔的「获得 / 从业条件」、**技能树的解锁与技能点获取条件**接入 `Ale.Condition` 条件系统。
5. **零硬依赖** —— TextMeshPro / Localization / Addressables 全部经编译宏可选启用，未开启时插件照常工作。

### 项目特性
| 特性 | 描述 |
| --- | --- |
| 单资产集中配置 | 一个 `ChronicleDatabase` 集中六大领域全部静态数据；编辑器仅在 ScriptableObject 上工作，全程 Undo / Redo。 |
| 灵活属性系统 | 复用 toolkit 的属性引擎，20+ 字段类型（含数组形态）；按功能标签成组增删，字段扩展无需改代码。 |
| 模板 + 实例 | 角色 / 核心属性 / 特质 / 技能 / 职业 / 头衔 各有模板蓝图与实例，Schema 对齐（`RebuildAttributes`）+ 深拷贝（`Clone`）；中列以模板作过滤维、「从模板添加」复制预设。 |
| 核心属性合流 | `CoreAttributeResolver` 计算「基础值 + 修正器」，返回逐来源拆解与 min/max 钳制；编辑器实时预览。 |
| 特质系统 | 永久 / 临时生命周期、修正器、硬互斥（等价组 + 显式）、软兼容（好感增减）、遗传（继承 / 出生几率）、AI 权重、**条件系统获得条件**。 |
| 技能两层模型 | 运行时分「永久学会（入存档）」与「外部提供者（如装备授予，不入存档）」两层，合并为有效集；另有无状态「一次性使用 / 施放」派发。 |
| 职业系统 | 等级上限 / 经验曲线（公式·表格·曲线）/ 每级成长汇入核心属性 / 等级解锁 / 转职树（进阶 DAG，编辑器加子防环）/ 关联技能树；运行时 `AddExp` 按曲线升级、封顶、施加解锁。 |
| 头衔系统 | 阶级头衔（逐级晋升、一序列一持有）与称号（多为唯一）两类；修饰器汇入核心属性 / 获得条件 / 阶级序列有序阶梯；运行时 `Grant` 晋升替换 + 唯一头衔易主。 |
| 条件系统整合 | 内置 7 个 `Ale.Condition` 求值器（年龄 / 属性比较 / 拥有特质 / 拥有职业 / 职业等级 / 持有头衔 / 位阶达到），支持多作用域（本人 / 配偶 / 父 / 母 …）。 |
| 运行时技能 UI | 虚拟滚动网格 / 顺序列表、主+副分组标签 AND 过滤、搜索、悬停 Tooltip、目录 / 角色双来源。 |
| 运行时角色面板 | `UiwCharacterView`（`0.3.1`）：一屏展示角色个人档案 + 6 项能力「基础→当前」求值明细 + 特质 / 职业 / 头衔（阶级位次）/ 技能；TMP 富文本信息卡排版、随内容自适应，无额外美术依赖。 |
| 单向导出 | `ChronicleConfigSerializer` → 紧凑二进制（魔数 `CHRO`，格式 v6），**append-only 向后兼容**旧版本（含 v3 / v4 / v5）；对象引用以 AssetGUID 承载。 |
| 三个可选宏 | TextMeshPro（`ATK_TMP`）/ Unity Localization（`ATK_LOCALIZATION`）/ Unity Addressables（`ATK_ADDRESSABLE`），在 Ale Toolkit 欢迎窗口一键开关；插件包本身零硬依赖。 |
| 跨包整合 | 与 `com.ale.inventory` 协同的「装备持有技能 / 消耗品触发技能」整合 Demo，两包互不依赖。 |

### 六大领域
| 领域 | 配置内容 | 运行时 |
| --- | --- | --- |
| **角色系统** | 角色模板、角色（身份字段 + 核心属性基础值 + 特质实例 + 职业/头衔持有 + 家族指针） | `CharacterDefinition` 组合 + 属性合流（特质/职业/头衔）+ 年龄推算 |
| **属性系统** | 核心属性模板、核心属性（范围 / 分类 / 默认基础值 / 图标 / **按条件修改值**） | `CoreAttributeResolver`（基础值 + 修正器合流，逐来源拆解；收集期按条件过滤修改值） |
| **特质系统** | 特质模板、特质（生命周期 / 修正器 / 互斥 / 兼容 / 遗传 / AI 权重 / 获得条件） | `CollectModifiers` → 属性合流；条件经 `Ale.Condition` 求值 |
| **技能系统** | 技能模板、技能（显示 / 图标 / 分组标签 / 自定义属性）、**技能树**（列表 / 层级 / 树状 + 技能点获取） | `SkillRuntimeManager`（永久 + 提供者两层 + 使用派发）+ 技能 UI |
| **职业系统** | 职业模板、职业（等级上限 / 经验曲线 / 每级成长 / 解锁 / 从业条件）、转职树 | `ProfessionRuntimeManager`（AddExp 按曲线升级 + 解锁）；成长汇入核心属性 |
| **头衔系统** | 头衔模板、头衔（阶级头衔 / 称号 · 位阶 / 修饰器 / 获得条件）、阶级序列 | `TitleRuntimeManager`（授予 / 晋升替换 / 唯一头衔易主）；加成汇入核心属性 |

> 另有 **通用（General）** 领域：枚举类型 / 功能标签 / 分组标签 / 数字格式，被上述系统引用。每个领域的完整说明见[详细文档](#-详细文档)。

## 💻 环境要求
- `Unity 2022.3` 或更新版本（`package.json` 声明的最低版本；本仓库基于 `Unity 6000.3` 开发与维护）。
- 核心插件为纯 C#，**不引入任何硬依赖**——TextMeshPro / Unity Localization / Unity Addressables 均通过编译宏**可选**启用。

## 📦 安装

> ⚠️ **本插件依赖通用底层包 [`com.ale.toolkit`](https://github.com/AleFeng/unity-ale-toolkit)（其中已内置 `Ale.Condition` 条件系统），必须先装它、再装本插件。** Unity Package Manager 不支持在 `package.json` 的 `dependencies` 里写 git URL，无法自动拉取，故**顺序不能颠倒**。先安装 toolkit（建议 1.4.0 或更新）：`https://github.com/AleFeng/unity-ale-toolkit.git?path=/Packages/com.ale.toolkit`。漏装或颠倒会报 `找不到 Ale.Toolkit.* / Ale.Condition.*` 一类编译错——补装 toolkit 并等重新编译即可。

### 使用 UPM（推荐）
`Window > Package Manager` → 左上角 `+` → `Install package from git URL...` → 先粘贴 toolkit，再粘贴本插件：

```
https://github.com/AleFeng/unity-ale-chronicle-system.git?path=/Packages/com.ale.chronicle
```

**要固定版本，把 `#<tag>` 加在整条 URL 的最末尾**（必须在 `?path=` 之后）：

```
https://github.com/AleFeng/unity-ale-chronicle-system.git?path=/Packages/com.ale.chronicle#0.3.1
```

### 其他方式
也可以下载仓库，把 `Packages/com.ale.chronicle` 整个文件夹拷进你项目的 **`Packages/` 目录**（不是 `Assets/`）—— Unity 会自动识别为本地包。

安装成功后，菜单栏会出现 **`Tools > Ale Toolkit > Chronicle System`**（Chronicle Editor / Demo Wizard）。

> **整合 Demo（可选）**：`Assets/DemoInventory/` 的 Chronicle × Inventory 整合演示额外依赖 [`com.ale.inventory`](https://github.com/AleFeng/unity-ale-inventory-system)；核心包不需要它。

## 🚀 快速开始
下面是最短路径，**完整说明见 [详细文档](#-详细文档)**。

### 1. 创建数据文件
```
Project 面板右键 > Create > ChronicleSystem > Chronicle Database
```

### 2. 打开编辑器并配置
选中 `.asset`，Inspector 顶部点「在 Chronicle Editor 中编辑」，或菜单 `Tools > Ale Toolkit > Chronicle System > Chronicle Editor`。依次在 **角色 / 属性 / 特质 / 技能 / 职业 / 头衔 / 通用** 页签中配置。

### 3. 运行时接入
```csharp
using Ale.Chronicle;

// 注册配置数据库（或 LoadFromBinary(bytes) 从导出的二进制加载）
ChronicleDataManager.Instance.Register(chronicleDatabase);

// 技能：永久学会 / 外部提供 / 一次性使用 / 有效集
SkillRuntimeManager.Instance.Learn("hero", "fireball");
SkillRuntimeManager.Instance.SetProvidedSkills("hero", "equipment", new[] { "guard" });
SkillRuntimeManager.Instance.UseSkill("hero", "heal", sourceKey: "potion");
var effective = SkillRuntimeManager.Instance.GetEffectiveSkills("hero");

// 职业：习得 + 加经验（按 ExpCurve 升级、施加等级解锁）
ProfessionRuntimeManager.Instance.Learn("hero", "warrior", primary: true);
ProfessionRuntimeManager.Instance.AddExp("hero", "warrior", 100);

// 头衔：授予（阶级头衔按阶梯晋升替换、唯一头衔从他人剥夺）
TitleRuntimeManager.Instance.Grant("hero", "duke", worldDay: 0);
```

### 4. 一键 Demo
**`CharacterSystemDemo` 演示场景**（同屏 `UiwCharacterView` 角色信息面板 + `UiwSkillView` 技能界面，由代码全量生成的示例数据库 + 示例角色露娜驱动）已作为 **Sample** 打包于 `Packages/com.ale.chronicle/Samples~/Demo`，在 Package Manager 本包详情页「Samples」区一键导入即可 Play；另有 `Assets/DemoInventory/` Chronicle × Inventory 整合演示，及菜单 `Tools > Ale Toolkit > Chronicle System > Demo Wizard` 一键生成技能 UI 预制体。

## 🧩 可选宏开关
三个宏均在 **Ale Toolkit 欢迎窗口**（`Tools > Ale Toolkit > Welcome`）的「插件支持（编译宏）」区一键开关，并实时检测对应 Package 是否已安装：

| 开关 | 宏 | 作用 |
| --- | --- | --- |
| TextMeshPro | `ATK_TMP` | UI 文本组件使用 `TMP_Text`，否则用 `UnityEngine.UI.Text`。 |
| Unity Localization | `ATK_LOCALIZATION` | `Text` 字段可挂本地化引用（表 + 条目），支持多语言。 |
| Unity Addressables | `ATK_ADDRESSABLE` | 运行时资源经 Addressable 按需异步加载；导出时登记被引用资源。 |

> 切换宏后需等待 Unity 重新编译生效。

## 📖 详细文档
本 README 面向整体介绍与快速上手。**完整的使用说明**——每个领域的配置细节、运行时 API、技能两层模型、UI 组件、整合 Demo、序列化等——请见插件内文档：

👉 **[Packages/com.ale.chronicle/README.md](Packages/com.ale.chronicle/README.md)**

## 📁 目录结构
```
Packages/com.ale.chronicle/          ← 包根
├── package.json  CHANGELOG.md  LICENSE.md  README.md   ← 详细使用文档
├── Runtime/                          Ale.Chronicle.Runtime（命名空间 Ale.Chronicle）
│   ├── Attribute/     核心属性 定义 / 模板 / 值
│   ├── Character/     角色 定义 / 模板 / 身份字段常量
│   ├── Condition/     Ale.Condition 整合（作用域 + 七个求值器）
│   ├── Database/      ChronicleDatabase（中心配置 ScriptableObject）
│   ├── Manager/       DataManager / RuntimeManager / Skill·Profession·Title RuntimeManager
│   ├── Modifier/      CoreAttributeResolver（属性合流）
│   ├── Profession/    职业 / ExpCurve / 转职树 / 角色职业状态 / 运行时状态
│   ├── Serialization/ 二进制序列化 + DTO
│   ├── Skill/         Skill / SkillTemplate / 技能树 / 已学状态 / SkillUseEvent
│   ├── Tagging/       ChronicleGroupTag（分组标签）
│   ├── Title/         头衔 / 阶级序列 / 角色头衔 / 运行时状态
│   └── Trait/         特质 定义 / 模板 / 实例 / 兼容 / AI 权重
├── Runtime/UI/                       Ale.Chronicle.Runtime.UI（角色面板 UiwCharacterView + 技能 UI 组件）
├── Editor/                           Ale.Chronicle.Editor（三列编辑器 + 七页签）
├── Docs~/                            （预留）
└── Samples~/Demo/                    「Chronicle 演示」Sample（CharacterSystemDemo 场景 + 角色/技能 UI 预制体 + 代码生成示例数据库 + 本地化）
```

工程内演示与测试：`Packages/com.ale.chronicle/Samples~/Demo/`（**角色面板 + 技能 UI** 演示场景 `CharacterSystemDemo`，Package Manager 可导入）、`Assets/DemoInventory/`（Chronicle × Inventory 整合）、`Assets/Editor/DemoWizard/`（技能预制体生成）、`Assets/Tests/`（20 个 EditMode 测试，含职业 / 头衔 / 技能树）。

## 📄 许可
本项目基于 [MIT License](LICENSE) 开源，可自由用于商业与非商业项目。

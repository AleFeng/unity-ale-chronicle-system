# 更新日志（Changelog）

本文件记录 Chronicle System（`com.ale.chronicle`）的所有重要变更。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [0.6.0] - 2026-09-08

**条件参数里的各类 id 从裸文本框变成分组下拉。** 编年史的 7 个 `Chronicle.*` 判定器里，`attrId` / `traitId` / `titleId` / `professionId` / `ladderId` 一直是手打字符串——打错一个字静默返回 false，`ConditionEngine` 只对**未注册的判定器键**告警，对写错的**参数值**完全无声；而同一个属性 id 在 Effect Editor 里早就是按系统名分组的下拉（`ChronicleEffectAttributeProvider`）。toolkit 1.12.0 为条件系统补上了对称的注入点，本版接入：判定器在 schema 里声明参数属于哪个候选目录，编辑器侧登记提供者供给候选。纯编辑期改善，运行时语义与序列化格式一字未动。

### 新增

- **`ChronicleConditionCatalogs`**（`Runtime/Condition/`）：5 个候选目录引用常量——`Attribute` / `Trait` / `Title` / `Profession` / `RankLadder`。放在运行时程序集是因为判定器 schema（运行时代码）要引用它。
- **`ChronicleConditionParamProvider`**（`Editor/Common/`，`[InitializeOnLoad]`）：向 toolkit 的 `ConditionDrawerHooks` 登记 5 个 `IConditionParamCatalogProvider`（系统名「Chronicle」），扫描工程内全部 `ChronicleDatabase` 资产，为核心属性 / 特质 / 头衔 / 职业 / 阶级序列供给「显示名 (id)」候选。形状与同目录的 `ChronicleEffectAttributeProvider` 一致；`ChronicleDatabasePostprocessor` 现在一并失效两者的资产缓存。

### 变更

- **6 个判定器的字符串参数标注 `catalogRef`**：`Chronicle.AttributeCompare.attrId` → 属性、`HasTrait.traitId` → 特质、`HasTitle.titleId` → 头衔、`HasProfession.professionId` / `ProfessionLevelAtLeast.professionId` → 职业、`HasRankAtLeast.ladderId` → 阶级序列。于是条件表达式绘制器把这些字段渲染为分组下拉（悬空值保留并标「（未知）」）。`Chronicle.Age` 无字符串参数，未改动。

### 说明

- 依赖底线随之提高到 toolkit **1.12.0**（需要 `ConditionParamDef.catalogRef` 与 `ConditionDrawerHooks`）。
- 本版**没有**把 9 个内联 `ConditionExpression` 字段（特质 / 职业 / 头衔 / 技能树 ×3 / 技能点 / 条件修改器）改成按 id 引用 toolkit 的新条件库——「可以引用」的能力已由 toolkit 1.12.0 的 `EditorConditionRefListDrawer` 铺好，实际迁移按需分批推进。

## [0.5.0] - 2026-09-07

**效果与 Gameplay 标签外移至 toolkit 1.10.0 的共用效果库 `EffectDatabase`；编年史库只保留技能的效果 id 引用。** 0.4.0 把效果存在编年史库里、编辑器自带一份「效果」页签——Inventory 也有一份结构相同的，效果只能在各自库内定义。本版按 toolkit 1.10.0 的共用化方案改造：效果 / 标签在 Effect Editor 一处配置、所有上层系统按 id 引用；Chronicle 只保留「效果引用列表 + 跳转」，并继续实现自己的 `Chronicle.*` 执行器（效果本质是「对某个系统的操作」，系统如何被操作由系统自己实现）。运行时语义不变：`CharacterSystemDemo` 的精神篡改 / 战意 / 心智护盾 / 回复药剂 / 推进时钟流程与 0.4.0 逐位一致，只是效果来自 `EffectDatabase.asset`。

### 破坏性变更

- ⚠️ **`ChronicleDatabase.Effects` / `GameplayTags` / `GetEffect` 与 `IEffectDefinitionSource` 实现移除**；`ChronicleEffect` 标记 `[Obsolete]`，仅用于承接旧数据。0.4.0 资产里的效果 / 标签经 `FormerlySerializedAs` 落入隐藏字段 `legacyEffects` / `legacyGameplayTags`（`[Obsolete]` 访问器 `LegacyEffects` / `LegacyGameplayTags`，`HasLegacyEffectData`），运行时不再读取——请用迁移菜单搬入效果库（见下文「迁移指引」）。legacy 字段保留一个版本，下一版删除。
- ⚠️ **`ChronicleDataManager` 不再登记为 toolkit 效果定义源、不再并入 Gameplay 标签、删除 `GetEffect`**；效果按 id 经 toolkit `EffectDataManager`（`ChronicleEffectContext` 把它注册为上下文定义源）→ 全局 `EffectDefinitionRegistry.Default` 解析。宿主须注册效果库：放在 `Resources` 下随启动自动登记，或 `EffectDataManager.Instance.Register(effectDatabase)`（Demo 的 `ChronicleDemoBootstrap` 新增 `effectDatabase` 字段）。`EffectRuntimeManager.LoadSaveData` 亦改按全局注册表解析定义。
- **二进制格式 v8**：不再写出 v7 的效果 / Gameplay 标签两块（效果库改由 toolkit `EffectConfigSerializer` 单独导出）；技能块的 `onUseEffectRefs` 保留。读 v7 文件时两块读入 legacy 字段（同样经迁移菜单进入效果库）；`LoadFromBinary` 的旧文件效果不再直接生效。
- `ChronicleDatabase.Validate` 不再校验效果 / 标签，也不再把技能 `onUseEffectRefs` 作悬空校验（效果在其它库，运行时按 id 解析；编辑器以「未找到」标注、不阻断）。
- 编辑器删除「效果」页签（`EffectSystemTab` / `ChronicleEffectFields` / `EChronicleEntityKind.Effect`，及旧的属性 id 整字段委托）；效果在 toolkit 的 **Effect Editor**（`Tools > Ale Toolkit > Effect System > Effect Editor`）配置。
- `UiwCharacterView` 的效果显示名改经 `EffectDataManager` 解析（`Ale.Chronicle.Runtime.UI` 新增引用 `Ale.Effect.Runtime`）。

### 新增

- **迁移**：`Tools > Ale Toolkit > Chronicle System > 迁移效果到 Effect Database`（`EditorChronicleEffectMigration` 窗口：来源编年史库 + 目标效果库（可就地新建）→ 迁移 → 报告 → 在 Effect Editor 打开）；`ChronicleDatabase` 资产 Inspector 检测到 legacy 数据时给出提示与同一入口。纯数据部分 `ChronicleLegacyEffects.MigrateInto(source, target)`（运行时程序集，可测试）：逐条 `ChronicleEffect → EffectEntry`（显示名 / 描述 / 图标 / 定义深拷贝并归一，不挂模板）并从 legacy 移除；目标库已有同 id 的跳过、报告并留在 legacy（不覆盖，处理后可重跑）；标签按归一名去重并入。
- **技能 Inspector 的效果引用**改用 toolkit `EditorEffectRefListDrawer`：「+」从工程内全部效果库的目录选择（按库分组）、拖拽重排 / 删除、「打开」跳转到 Effect Editor 并定位、未找到标注（不阻断）、自由输入。
- **属性 id 候选 provider** `ChronicleEffectAttributeProvider`（`[InitializeOnLoad]`）：向 toolkit 效果定义绘制器登记系统「Chronicle」的核心属性候选（扫描工程内全部 `ChronicleDatabase` 资产，资产变动后重扫），Effect Editor 里修饰器 / 属性幅度的属性 id 下拉可直接选编年史属性（多系统并存时按系统名分组）。
- Demo：Character Seeder **D7 改为「效果库+特质技能」**——生成 `Assets/Demo/Data/EffectDatabase.asset`（枚举「效果类别」、模板「通用」schema：category 枚举 + priority 整数、4 个 Gameplay 标签、4 个效果均挂模板并填自定义属性）并清空编年史库 legacy；`ChronicleDemoBootstrap` 增 `effectDatabase` 字段并注册；`Samples~/Demo` 同步（新增 `Data/EffectDatabase.asset`）。整合 Demo `EquipmentSkillDemo` 的 `regen_draught` / `sharpen_oil` 均改建在同一 toolkit 效果库（Inventory 1.13.0 起同样不再自持效果；`ConsumableEffectUse` 不变）。
- 测试：`ChronicleEffectDatabaseTests` 重建（校验不再查效果引用、数据管理器不再是定义源、v8 往返、手工 v7 字节流读入 legacy、迁移含冲突 / 重跑 / 干跑）；`EffectRuntimeManagerTests` 改建 `EffectDatabase` + `EffectDataManager`。

### 依赖

- ⚠️ **最低 `com.ale.toolkit` 版本提至 1.10.0**（效果库 `EffectDatabase` / `EffectDataManager` / Effect Editor / `EditorEffectRefListDrawer` / 属性 id provider）。
- 程序集引用：`Ale.Chronicle.Runtime.UI` / `Ale.Chronicle.Editor` / 测试程序集新增 `Ale.Effect.Runtime`。
- 整合 Demo（`Assets/DemoInventory`）需 `com.ale.inventory` ≥ 1.13.0（其效果亦外移至 toolkit 效果库）；核心包仍不依赖它。

### 迁移指引（0.4.0 → 0.5.0）

1. 升级 toolkit 至 1.10.0，重新编译（旧资产的效果 / 标签自动落入 legacy 字段，Inspector 出现黄色提示）。
2. 选中旧的 `ChronicleDatabase` 资产 → 「迁移效果到 Effect Database…」→ 目标库「新建…」（或选已有效果库）→ 迁移。报告有同 id 冲突时在目标库处理后重跑。
3. 把效果库放进 `Resources`，或在引导代码里 `EffectDataManager.Instance.Register(effectDatabase)`；`ChronicleDataManager.Register` 照旧。
4. 旧的 v7 二进制：导入编年史库后同样经迁移菜单进入效果库，再用 toolkit `EffectConfigSerializer` 导出效果库的二进制 / JSON。

## [0.4.0] - 2026-09-07

**接入 toolkit 1.9.0 的效果系统（GAS 式 GameplayEffect）与 Gameplay 标签：技能「使用」施加效果，效果落到角色的属性 / 特质 / 头衔 / 职业 / 技能；新增世界时钟与运行时特质。**

### 新增

- **效果（Effect）领域**：`ChronicleDatabase.Effects`（`ChronicleEffect` = 显示名 / 描述 / 图标 + toolkit `EffectDefinition`：时长策略 / 周期 / 叠加 / 标签 / 施加条件 / 概率 / 修饰器 / 执行阶段）、`ChronicleDatabase.GameplayTags`（本库声明的层级标签，注册数据库时并入 toolkit 标签注册表）；`Skill.onUseEffectRefs`（使用时按序施加的效果 id）。`Validate` 校验效果 id 重复、技能效果引用与修饰器 / 属性幅度目标属性悬空、定义错误（toolkit 警告不阻断）、非法标签名。
- **编辑器「效果」页签**：左列 Gameplay 标签目录、中列效果列表（以时长策略充当过滤 / 新建入口）、右列 ID / 名称 / 描述 / 图标 + 内联 toolkit 效果定义绘制器（属性 id 走核心属性下拉）+ 校验摘要；技能 Inspector 增「使用时施加的效果」。
- **运行时**（均为 `ToolkitSingleton` + `ISaveable`）：
  - `ChronicleClock`：世界日时钟，`AdvanceDays` / `AdvanceYears` 依次驱动特质到期与效果周期 / 到期，派发 `OnDaysAdvanced`。
  - `TraitRuntimeManager`：运行期授予的特质与配置层合成有效特质；互斥 / 等价组拒绝、临时特质到期、重复授予刷新 / 叠层、按来源成组撤销。
  - `EffectRuntimeManager`：每角色一个 toolkit `EffectContainer` + 永久落地修饰器列表；`Apply` / `Remove*` / `Tick` / 驱散 / 存档；实现 toolkit 的容器源 / 属性读写 / 标签源。
  - `ChronicleCharacterRuntime`：运行时属性汇流（特质、职业成长、头衔各取配置 ∪ 运行时，条件修改值，活动效果，永久落地）。
  - `ChronicleRuntimeConditionSource` / `ChronicleEffectContext`：首个真实条件源（主体 = 效果目标、对象 = 效果来源）与效果上下文组装；`IChronicleRuntimeSink` 执行器落地门面。
  - 七个 `Chronicle.*` 执行器：授予 / 移除特质、授予 / 剥夺头衔、增加职业经验、学会 / 遗忘技能。
- `UiwCharacterView`：改为按「配置 ∪ 运行时」展示，新增「效果」分区（活动效果 / 剩余 / 层数 / 修饰器 + 永久落地汇总）与临时特质剩余时长；订阅运行时事件自动刷新；年龄按世界时钟推算。
- 演示：Character Seeder 新增 **D7 效果+标签**（精神异常 / 精神篡改 / 战意 / 心智护盾 / 回复药剂）；`CharacterSystemDemo` 场景新增「效果演示」按钮面板（`ChronicleEffectDemo`）。
- 整合 Demo（`Assets/DemoInventory`）：`ConsumableSkillUse` 改为 `ConsumableEffectUse`（`InventoryRuntimeManager.UseItem` + `ChronicleEffectContext`），回复药水 → Chronicle 库的 `regen_draught`、磨刀油 → Inventory 库自定义的 `sharpen_oil`；驱动器增世界时钟推进与角色耐力 / 战力、活动效果显示；监听器改为打印逐效果结果。需 `com.ale.inventory` ≥ 1.12.0。
- 测试：`ChronicleEffectDatabaseTests` / `TraitRuntimeManagerTests` / `EffectRuntimeManagerTests`。

### 变更

- `SkillRuntimeManager.UseSkill(target, skillId, sourceKey, sourceCharacterId)`：校验后按序施加 `onUseEffectRefs`，`SkillUseEvent` 增 `SourceCharacterId` / `EffectResults` / `EffectsApplied`。
- 职业等级解锁的特质改为直接经 `TraitRuntimeManager` 授予（`OnUnlockTrait` 事件照旧派发）。
- 二进制格式 **v7**：技能块尾追加 `onUseEffectRefs`；尾部追加 效果 / Gameplay 标签 两块（效果定义以 Effect System JSON 串承载）。旧 v6 及更早文件仍可导入。

### 依赖

- ⚠️ **最低 `com.ale.toolkit` 版本提至 1.9.0**（`Ale.Effect` GAS 层、`Ale.GameplayTags`、`Ale.Modifier.Core`）。修饰器类型（`ModifierDefinition` 等）随 toolkit 1.9.0 迁入命名空间 `Ale.Modifier`——本包已同步；宿主项目若直接引用这些类型需补 `using Ale.Modifier;`。

## [0.3.2] - 2026-08-15

**比较符改用 toolkit 的公共实现，移除本地副本。** 判定结果逐位不变；最低 toolkit 版本提至 1.8.0。

### 破坏性变更

- **删除 `ChronicleCompareOp`**（`Ale.Chronicle` 命名空间下的 public 静态类）。它的五个索引常量、`Labels` 与 `Compare`
  已由 toolkit 1.8.0 的 `Ale.Condition.ConditionCompare` 提供，一一对应：
  - `ChronicleCompareOp.Greater` … → `ConditionCompare.Greater` …（值不变，仍是 0~4）
  - `ChronicleCompareOp.Labels` → `ConditionCompare.Labels`（文本与顺序逐字不变）
  - `ChronicleCompareOp.Compare(a, op, b)` → `ConditionCompare.Compare(value, amount, op)`
    ⚠️ **形参顺序不同**：比较符从中间挪到了末尾。外部若有调用，务必逐个核对实参顺序——三个参数里有两个是 `double`，传反了编译器不会报错，表现是「大于」和「小于」互换。
  - 未留 `[Obsolete]` 转发壳：壳必须保持旧形参顺序才不破坏外部调用，而那与新 API 的顺序相反，本身就是个陷阱。

### 变更

- `AttributeCompareEvaluator` 改用 `ConditionCompare`，并把局部变量 `value` 改名为 `amount`——它其实是**阈值**，与新签名第一个形参 `value`（被测值）同名不同义。参数 id 仍是 `"value"`（已序列化，不改），已配置的条件资产与存档不受影响。
- **「等于」的容差仍是 `1e-6`**：`ConditionCompare` 的浮点重载默认值与本包原先一致。这一点是刻意核对过的——toolkit 内置的 `NumberCompareEvaluator` 用的是更严的 `1e-9`，若公共实现照抄那个值，本包的属性比较会静默变严（`GetCoreAttribute` 返回 `float`，`10.1f` 扩宽后与配置里的 `double 10.1` 相差约 `3.8e-7`，恰好卡在两个容差之间）。

### 新增

- `ChronicleConditionEvaluatorsTests` 补两组用例：
  - **方向断言**——原有用例里「大于」「小于」各只测了一个方向，实参传反仍会通过；新增的反向断言把方向钉死。
  - **`AttributeCompare_EqualToleratesFloatWidening`**——属性 `10.1f` 对阈值 `10.1d` 判为相等，钉住上面那条容差。原有用例全用整数 `10f`，测不出这个回归。

### 修复

- `ChronicleInfo.Version` 由 `0.3.0` 订正为 `0.3.2`（自 0.3.1 起漏同步）。

### 依赖

- ⚠️ **最低 `com.ale.toolkit` 版本提至 1.8.0。** 本包的 asmdef 硬引用 `Ale.Condition.Core`，toolkit 版本不足时会直接编译报错（提示找不到 `ConditionCompare`），升级 toolkit 即可。

## [0.3.1] - 2026-08-06

新增**运行时角色信息面板 `UiwCharacterView`** 与一个**「角色系统」演示 Sample**，把六大领域的配置串成一个「Play 即见」的角色档案界面。纯运行时 UI 组件 + 演示新增，**核心数据模型 / 编辑器 / 序列化（仍为 v6）零变化**。

### 新增

- **角色信息面板 `UiwCharacterView`**（`Ale.Chronicle.Runtime.UI`，继承 `UiwViewBase`，与 `UiwSkillView` 同规格）：从 `ChronicleDataManager` 取一个 `CharacterDefinition`，分区展示——**个人档案**（姓名 / 性别 / 年龄 / 身高 / 体重 / 三围 / 血型 / 兴趣，枚举经数据管理器直解）、**能力**（6 项核心属性经 `CoreAttributeResolver` 求「基础 → 当前」+ 逐来源明细，超上限标注封顶）、**特质 / 职业（等级 + 主职业 + 授头衔）/ 头衔（阶级头衔附阶级序列位次）/ 技能（由职业关联技能树导出）**。TMP 富文本做配色 / 分栏 / 层级的桌游式信息卡排版（头部卡 + 能力条 + 各分区卡），随内容自适应高度；无额外美术依赖。
- **Sample「Chronicle 演示（角色面板 + 技能 UI）」**（`Samples~/Demo`，`package.json` 声明、可经 Package Manager 一键导入）：一个由代码**全量生成**的 `ChronicleDatabase`（6 能力属性 + 角色个人字段 schema + ~6 特质 + 爵位五连/阶级序列/职业里程碑/好感·疯狂头衔 + 8 技能/5 技能树 + 主职业 99 级·好感·疯狂 5 级职业 + 示例角色**露娜**）驱动 `CharacterSystemDemo` 场景，同屏展示 `UiwCharacterView` 与 `UiwSkillView`；含 `PF_UiwCharacterView` / `PF_UiwSkillView` 预制体、演示宿主 `ChronicleDemoManager`、7 语言本地化串表与 CJK 字体图集。

### 说明

- 本版仅新增 UI 组件与演示，**不改任何配置数据结构、编辑器或二进制格式**；`UiwCharacterView` 展示的是角色**静态配置**（起始配装），运行时进度（升级 / 授予）仍由各 `*RuntimeManager` 维护。

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

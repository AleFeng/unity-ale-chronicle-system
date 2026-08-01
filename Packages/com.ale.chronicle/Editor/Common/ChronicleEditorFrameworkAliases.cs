using System.Collections.Generic;
using Ale.Chronicle;
using Ale.Toolkit.Editor;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 编年史编辑器框架的领域别名：把 toolkit 的泛型基类闭合到 <see cref="ChronicleDatabase"/>，
    /// 并把「吃 <see cref="IEditorDbContext{ChronicleDatabase}"/> 的抽象方法」桥接到本包重新声明的
    /// <see cref="IChronicleEditorContext"/> 版本——子类的 <c>DrawInspector</c>/<c>AddFromTemplate</c>/
    /// <c>DrawEntityList</c> 等直接拿到领域上下文。运行时 <c>ctx</c> 恒为窗口本身（实现
    /// <see cref="IChronicleEditorContext"/>），向下转型安全。
    /// </summary>
    public abstract class EditorMasterListPanel<T>
        : Ale.Toolkit.Editor.EditorMasterListPanel<ChronicleDatabase, T> where T : class
    {
        public sealed override void DrawInspector(IEditorDbContext<ChronicleDatabase> ctx, T item)
            => DrawInspector((IChronicleEditorContext)ctx, item);

        /// <summary>绘制选中项 Inspector（<paramref name="item"/> 为 null 表示未选中）。</summary>
        public abstract void DrawInspector(IChronicleEditorContext ctx, T item);

        protected sealed override void BeforeDrawList(IEditorDbContext<ChronicleDatabase> ctx)
            => BeforeDrawList((IChronicleEditorContext)ctx);

        /// <summary>绘制列表之前的钩子（如按需重建数据源）。默认空。</summary>
        protected virtual void BeforeDrawList(IChronicleEditorContext ctx) { }
    }

    /// <summary>中栏实体列表面板的编年史别名。重复 id 经 <see cref="Kind"/> + <see cref="IChronicleEditorContext.DuplicateIdsOf"/> 提供。</summary>
    public abstract class EditorEntityListPanel<TEntity, TTemplate>
        : Ale.Toolkit.Editor.EditorEntityListPanel<ChronicleDatabase, TEntity, TTemplate>
        where TEntity : class where TTemplate : class
    {
        protected EditorEntityListPanel(string dragId) : base(dragId) { }

        /// <summary>实体种类（用于取重复 id 集合）。</summary>
        protected abstract EChronicleEntityKind Kind { get; }

        protected sealed override HashSet<string> DuplicateIds(IEditorDbContext<ChronicleDatabase> ctx)
            => ((IChronicleEditorContext)ctx).DuplicateIdsOf(Kind);

        protected sealed override TEntity AddFromTemplate(IEditorDbContext<ChronicleDatabase> ctx, string templateName)
            => AddFromTemplate((IChronicleEditorContext)ctx, templateName);

        /// <summary>从模板新建一个实体并加入数据库，返回之（含 RecordUndo / MarkDirty）。</summary>
        protected abstract TEntity AddFromTemplate(IChronicleEditorContext ctx, string templateName);

        protected sealed override TEntity QuickAdd(IEditorDbContext<ChronicleDatabase> ctx)
            => QuickAdd((IChronicleEditorContext)ctx);

        /// <summary>复制末尾条目新建一个实体并加入数据库，返回之（含 RecordUndo / MarkDirty）。</summary>
        protected abstract TEntity QuickAdd(IChronicleEditorContext ctx);
    }

    /// <summary>三列系统页签的编年史别名。</summary>
    public abstract class EditorThreeColumnTab<TEntity>
        : Ale.Toolkit.Editor.EditorThreeColumnTab<ChronicleDatabase, TEntity> where TEntity : class
    {
        protected sealed override TEntity DrawEntityList(IEditorDbContext<ChronicleDatabase> ctx, TEntity displaySelected)
            => DrawEntityList((IChronicleEditorContext)ctx, displaySelected);

        /// <summary>绘制中列实体列表，返回其当前选中项。</summary>
        protected abstract TEntity DrawEntityList(IChronicleEditorContext ctx, TEntity displaySelected);

        protected sealed override void DrawEntityInspector(IEditorDbContext<ChronicleDatabase> ctx, TEntity entity)
            => DrawEntityInspector((IChronicleEditorContext)ctx, entity);

        /// <summary>绘制右列的实体 Inspector。</summary>
        protected abstract void DrawEntityInspector(IChronicleEditorContext ctx, TEntity entity);
    }
}

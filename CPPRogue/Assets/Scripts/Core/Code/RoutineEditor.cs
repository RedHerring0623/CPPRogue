using System;
using CPPRogue.Core.Code.Ast;

namespace CPPRogue.Core.Code
{
    /// <summary>
    /// 拼装位置：Owner 为 null 表示根；Branch 0 = if 的 then / 循环体，1 = if 的 else。
    /// Index 是缝隙下标（插到这个位置之前；删除/移动时指向该下标上的语句）。
    /// </summary>
    public readonly struct Slot
    {
        public readonly Block Owner;
        public readonly int Branch;
        public readonly int Index;

        public Slot(Block owner, int branch, int index)
        {
            Owner = owner;
            Branch = branch;
            Index = index;
        }
    }

    /// <summary>
    /// Routine 拼装编辑器：管理语句树的插入/移动/删除，并做防呆
    /// （行数上限、位置合法性、不能把语句拖进它自己的子树）。
    /// UI 只调用这些操作，不直接改树——拼装规则因此可以单测。
    /// </summary>
    public sealed class RoutineEditor
    {
        private Block[] _root = Array.Empty<Block>();

        public int MaxLines { get; }

        public RoutineEditor(int maxLines = Routine.DefaultMaxLines)
        {
            MaxLines = maxLines;
        }

        public Block[] Root => _root;

        /// <summary>当前语句总数（含嵌套）。</summary>
        public int StatementCount => BlockTree.CountStatements(_root);

        public Routine BuildRoutine()
        {
            return new Routine(_root);
        }

        public bool CanInsert(Slot slot, Block block)
        {
            Block[] body = ResolveBody(slot);
            if (body == null || slot.Index < 0 || slot.Index > body.Length)
                return false;
            return StatementCount + BlockTree.CountStatements(new[] { block }) <= MaxLines;
        }

        public void Insert(Slot slot, Block block)
        {
            if (!CanInsert(slot, block))
                throw new InvalidOperationException("这个位置放不下（位置非法或超过行数上限）。");
            if (slot.Owner == null)
                _root = InsertAt(_root, slot.Index, block);
            else
                SetBody(slot.Owner, slot.Branch, InsertAt(ResolveBody(slot), slot.Index, block));
        }

        public bool CanRemove(Slot slot)
        {
            Block[] body = ResolveBody(slot);
            return body != null && slot.Index >= 0 && slot.Index < body.Length;
        }

        /// <summary>删除语句（连同它的子语句）。</summary>
        public void Remove(Slot slot)
        {
            if (!CanRemove(slot))
                throw new InvalidOperationException("要删除的语句不存在。");
            if (slot.Owner == null)
                _root = RemoveAt(_root, slot.Index);
            else
                SetBody(slot.Owner, slot.Branch, RemoveAt(ResolveBody(slot), slot.Index));
        }

        public bool CanMove(Slot from, Slot to)
        {
            if (!CanRemove(from))
                return false;
            Block[] fromBody = ResolveBody(from);
            Block moving = fromBody[from.Index];

            Block[] toBody = ResolveBody(to);
            if (toBody == null || to.Index < 0 || to.Index > toBody.Length)
                return false;
            // 不能拖进自己（或自己的子树）里面
            if (to.Owner != null && BlockTree.Contains(moving, to.Owner))
                return false;
            // 原地或紧邻其后 = 没有变化
            if (ReferenceEquals(to.Owner, from.Owner) && to.Branch == from.Branch
                && (to.Index == from.Index || to.Index == from.Index + 1))
                return false;
            return true; // 移动不改变总行数，无需 MaxLines 检查
        }

        public void Move(Slot from, Slot to)
        {
            if (!CanMove(from, to))
                throw new InvalidOperationException("这个移动不合法（目标在自己子树内或位置无效）。");

            Block[] fromBody = ResolveBody(from);
            Block moving = fromBody[from.Index];
            Block[] removed = RemoveAt(fromBody, from.Index);
            if (from.Owner == null)
                _root = removed;
            else
                SetBody(from.Owner, from.Branch, removed);

            int index = to.Index;
            if (ReferenceEquals(to.Owner, from.Owner) && to.Branch == from.Branch && to.Index > from.Index)
                index = to.Index - 1;

            Block[] toBody = ResolveBody(to);
            if (to.Owner == null)
                _root = InsertAt(toBody, index, moving);
            else
                SetBody(to.Owner, to.Branch, InsertAt(toBody, index, moving));
        }

        private Block[] ResolveBody(Slot slot)
        {
            if (slot.Owner == null)
                return _root;
            if (slot.Branch != 0 && !(slot.Owner.Kind == BlockKind.If && slot.Branch == 1))
                return null;
            if (slot.Owner.Kind != BlockKind.If && slot.Owner.Kind != BlockKind.For && slot.Owner.Kind != BlockKind.While)
                return null;
            return BlockTree.GetBody(slot.Owner, slot.Branch);
        }

        private static void SetBody(Block owner, int branch, Block[] body)
        {
            if (owner.Kind == BlockKind.If && branch == 1)
                owner.ElseBody = body;
            else
                owner.Body = body;
        }

        private static Block[] InsertAt(Block[] body, int index, Block block)
        {
            var arr = new Block[body.Length + 1];
            Array.Copy(body, arr, index);
            arr[index] = block;
            Array.Copy(body, index, arr, index + 1, body.Length - index);
            return arr;
        }

        private static Block[] RemoveAt(Block[] body, int index)
        {
            var arr = new Block[body.Length - 1];
            Array.Copy(body, arr, index);
            Array.Copy(body, index + 1, arr, index, body.Length - index - 1);
            return arr;
        }
    }
}

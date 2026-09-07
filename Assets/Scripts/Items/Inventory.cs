using System;
using System.Collections.Generic;

namespace FarmMVP
{
    /// <summary>
    /// Player inventory. Slots 0..HotbarSize-1 are the hotbar (shown in the quick bar);
    /// the rest are backpack slots shown in the full inventory UI.
    /// </summary>
    [Serializable]
    public class Inventory
    {
        public const int HotbarSize = 10;      // 퀵 인벤토리 한 줄 (숫자 1~0)
        public const int BackpackPages = 3;    // 기본 1 + 증축 2
        public const int TotalSlots = HotbarSize * BackpackPages; // 최대 30칸
        public const int ShippingSlots = 20;   // 배송함 (10칸 2줄)

        public ItemStack[] slots;

        /// <summary>실제로 쓸 수 있는 칸 수. 배낭을 증축하면 늘어난다 (잠긴 칸에는 아이템이 들어가지 않는다).</summary>
        public int unlockedSlots;

        public event Action OnChanged;

        public Inventory(int slotCount = TotalSlots)
        {
            slots = new ItemStack[slotCount];
            unlockedSlots = slotCount;
        }

        private int Usable => Math.Min(slots.Length, unlockedSlots);

        public void RaiseChanged() => OnChanged?.Invoke();

        /// <summary>Add items, stacking where possible. Returns leftover that didn't fit.</summary>
        public int Add(string itemId, int count)
        {
            var def = ItemDatabase.Get(itemId);
            if (def == null) return count;

            // fill existing stacks
            for (int i = 0; i < Usable && count > 0; i++)
            {
                var s = slots[i];
                if (s != null && !s.IsEmpty && s.itemId == itemId && s.count < def.maxStack)
                {
                    int space = def.maxStack - s.count;
                    int move = Math.Min(space, count);
                    s.count += move;
                    count -= move;
                }
            }
            // new stacks in empty slots
            for (int i = 0; i < Usable && count > 0; i++)
            {
                if (slots[i] == null || slots[i].IsEmpty)
                {
                    int move = Math.Min(def.maxStack, count);
                    slots[i] = new ItemStack(itemId, move);
                    count -= move;
                }
            }
            RaiseChanged();
            return count;
        }

        public bool Remove(string itemId, int count)
        {
            if (CountOf(itemId) < count) return false;
            for (int i = 0; i < slots.Length && count > 0; i++)
            {
                var s = slots[i];
                if (s != null && !s.IsEmpty && s.itemId == itemId)
                {
                    int move = Math.Min(s.count, count);
                    s.count -= move;
                    count -= move;
                    if (s.count <= 0) slots[i] = null;
                }
            }
            RaiseChanged();
            return true;
        }

        public int CountOf(string itemId)
        {
            int total = 0;
            foreach (var s in slots)
                if (s != null && !s.IsEmpty && s.itemId == itemId)
                    total += s.count;
            return total;
        }

        public ItemStack GetSlot(int index)
        {
            if (index < 0 || index >= slots.Length) return null;
            return slots[index];
        }

        /// <summary>Swap or merge two slots (used by drag & drop).</summary>
        public void MoveSlot(int from, int to)
        {
            if (from == to || from < 0 || to < 0 || from >= slots.Length || to >= slots.Length) return;
            var a = slots[from];
            var b = slots[to];

            if (a != null && b != null && !a.IsEmpty && !b.IsEmpty && a.itemId == b.itemId)
            {
                var def = a.Def;
                int space = def.maxStack - b.count;
                int move = Math.Min(space, a.count);
                b.count += move;
                a.count -= move;
                if (a.count <= 0) slots[from] = null;
            }
            else
            {
                slots[from] = b;
                slots[to] = a;
            }
            RaiseChanged();
        }

        /// <summary>
        /// 서로 다른 인벤토리(예: 가방 ↔ 배송함) 사이에서 한 칸을 옮긴다.
        /// 같은 아이템이면 합치고, 아니면 두 칸을 맞바꾼다.
        /// </summary>
        public static void MoveBetween(Inventory from, int fromIndex, Inventory to, int toIndex)
        {
            var a = from.GetSlot(fromIndex);
            if (a == null || a.IsEmpty) return;
            var b = to.GetSlot(toIndex);

            if (b != null && !b.IsEmpty && a.itemId == b.itemId)
            {
                int space = b.Def.maxStack - b.count;
                int move = Math.Min(space, a.count);
                b.count += move;
                a.count -= move;
                if (a.count <= 0) from.slots[fromIndex] = null;
            }
            else
            {
                to.slots[toIndex] = a;
                from.slots[fromIndex] = b;
            }

            from.RaiseChanged();
            if (!ReferenceEquals(from, to)) to.RaiseChanged();
        }

        /// <summary>
        /// 한 칸을 통째로 다른 인벤토리에 밀어 넣는다 (쉬프트+클릭). 자리가 부족하면 들어간 만큼만 옮긴다.
        /// </summary>
        public static void QuickMove(Inventory from, int fromIndex, Inventory to)
        {
            var stack = from.GetSlot(fromIndex);
            if (stack == null || stack.IsEmpty) return;

            int leftover = to.Add(stack.itemId, stack.count);
            if (leftover <= 0) from.slots[fromIndex] = null;
            else stack.count = leftover;

            from.RaiseChanged();
        }

        public void ConsumeOne(int slotIndex)
        {
            var s = GetSlot(slotIndex);
            if (s == null || s.IsEmpty) return;
            s.count--;
            if (s.count <= 0) slots[slotIndex] = null;
            RaiseChanged();
        }
    }
}

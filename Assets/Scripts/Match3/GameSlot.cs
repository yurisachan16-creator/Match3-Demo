using System;
using Match3.Core.Interfaces;
using Match3.Core.Structs;

namespace Match3.App
{
    public class GameSlot : IGridSlot
    {
        public GridPosition GridPosition { get; private set; }
        public int ItemId { get; private set; }
        
        public bool HasItem { get; private set; }
        
        public bool IsMovable => true;
        public bool IsLocked => false;

        // Implement CanContainItem
        public bool CanContainItem => true;

        // Implement State
        public IGridSlotState State { get; } = new GameSlotState();

        public event Action<GameSlot> OnItemChanged;

        public void SetPosition(GridPosition position)
        {
            GridPosition = position;
        }

        public void SetItem(int itemId)
        {
            ItemId = itemId;
            HasItem = true;
            OnItemChanged?.Invoke(this);
        }

        public void Clear()
        {
            ItemId = -1; 
            HasItem = false;
            OnItemChanged?.Invoke(this);
        }

        public void Dispose()
        {
            OnItemChanged = null;
        }

        private class GameSlotState : IGridSlotState
        {
            public int GroupId => 0;
            public bool IsLocked => false;
            public bool CanContainItem => true;
        }
    }
}

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.App.Interfaces;

namespace Match3.App
{
    public class SimpleFillJob : IJob
    {
        private readonly IEnumerable<GameSlot> _slotsToFill;
        private readonly IItemsPool _itemsPool;

        public int ExecutionOrder => 0;

        public SimpleFillJob(IEnumerable<GameSlot> slotsToFill, IItemsPool itemsPool)
        {
            _slotsToFill = slotsToFill;
            _itemsPool = itemsPool;
        }

        public UniTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            foreach (var slot in _slotsToFill)
            {
                // In a real game, you might want to check if it's already filled 
                // or if it's an obstacle, but for Simple Fill, we overwrite.
                int newItemId = _itemsPool.GetRandomItemId();
                slot.SetItem(newItemId);
            }

            return UniTask.CompletedTask;
        }
    }
}

using System.Collections.Generic;
using Match3.App.Interfaces;

namespace Match3.App
{
    public class SimpleFillStrategy : IBoardFillStrategy<GameSlot>
    {
        public string Name => "Simple Fill Strategy";
        
        private readonly IItemsPool _itemsPool;

        public SimpleFillStrategy(IItemsPool itemsPool)
        {
            _itemsPool = itemsPool;
        }

        public IEnumerable<IJob> GetFillJobs(IGameBoard<GameSlot> gameBoard)
        {
            var slotsToFill = new List<GameSlot>();

            for (int row = 0; row < gameBoard.RowCount; row++)
            {
                for (int col = 0; col < gameBoard.ColumnCount; col++)
                {
                    var slot = gameBoard[row, col];
                    // Fill if it's not locked/obstacle? 
                    // For initial fill, we usually fill everything that can hold an item.
                    if (slot.IsMovable) 
                    {
                        slotsToFill.Add(slot);
                    }
                }
            }

            yield return new SimpleFillJob(slotsToFill, _itemsPool);
        }

        public IEnumerable<IJob> GetSolveJobs(IGameBoard<GameSlot> gameBoard, SolvedData<GameSlot> solvedData)
        {
            // We refill all the slots that were part of the solution.
            // Note: If you support special items (bombs), you might want to exclude
            // the slot where the bomb is created from being overwritten by a random item.
            // For this Simple Fill, we just refill everything.
            
            var solvedSlots = solvedData.GetSolvedGridSlots(onlyMovable: true);
            
            yield return new SimpleFillJob(solvedSlots, _itemsPool);
        }
    }
}

namespace Match3.App
{
    public interface IItemsPool
    {
        int GetRandomItemId();
    }

    public class ArrayItemsPool : IItemsPool
    {
        private readonly int[] _availableItemIds;
        private readonly System.Random _random;

        public ArrayItemsPool(int[] availableItemIds, int? seed = null)
        {
            _availableItemIds = availableItemIds;
            _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        public int GetRandomItemId()
        {
            if (_availableItemIds == null || _availableItemIds.Length == 0)
                return 0;
                
            int index = _random.Next(0, _availableItemIds.Length);
            return _availableItemIds[index];
        }
    }
}

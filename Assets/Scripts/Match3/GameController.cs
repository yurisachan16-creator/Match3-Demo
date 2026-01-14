using UnityEngine;
using Match3.App;
using Match3.App.Interfaces;
using Match3.Infrastructure;
using Match3.Infrastructure.SequenceDetectors;
using Match3.Core.Structs;
using Cysharp.Threading.Tasks;
using System.Text;
using System.Threading;

namespace Match3.App.Demo
{
    public class GameController : MonoBehaviour
    {
        private MyMatch3Game _game;
        
        [SerializeField] private int rows = 8;
        [SerializeField] private int cols = 8;
        [SerializeField] private float tileSize = 1f;
        [SerializeField] private int[] itemIds = new[] { 1, 2, 3, 4, 5 };
        [SerializeField] private Color[] palette = new[]
        {
            new Color(0.95f, 0.25f, 0.25f),
            new Color(0.20f, 0.55f, 0.95f),
            new Color(0.25f, 0.85f, 0.35f),
            new Color(0.95f, 0.85f, 0.20f),
            new Color(0.75f, 0.30f, 0.95f)
        };
        [SerializeField] private bool autoFitCamera = true;
        [SerializeField] private Transform boardRoot;
        [SerializeField] private GameBoardView boardView;
        [SerializeField] private Vector2Int debugSwapA = new Vector2Int(0, 0);
        [SerializeField] private Vector2Int debugSwapB = new Vector2Int(0, 1);

        void Start()
        {
            if (boardRoot == null)
            {
                var rootGo = new GameObject("Board");
                rootGo.transform.SetParent(transform, false);
                boardRoot = rootGo.transform;
            }

            if (boardView == null)
            {
                boardView = GetComponent<GameBoardView>();
                if (boardView == null)
                {
                    boardView = gameObject.AddComponent<GameBoardView>();
                }
            }

            var config = new GameConfig<GameSlot>
            {
                GameBoardDataProvider = new MyGameBoardDataProvider(rows, cols),
                GameBoardSolver = new GameBoardSolver<GameSlot>(new ISequenceDetector<GameSlot>[]
                {
                    new HorizontalLineDetector<GameSlot>(),
                    new VerticalLineDetector<GameSlot>()
                }, new ISpecialItemDetector<GameSlot>[0]),
                ItemSwapper = new MyItemSwapper(),
                LevelGoalsProvider = new MyLevelGoalsProvider(),
                SolvedSequencesConsumers = new ISolvedSequencesConsumer<GameSlot>[] { }
            };

            _game = new MyMatch3Game(config);
            
            _game.InitGameLevel(0);

            var itemsPool = new ArrayItemsPool(itemIds);
            var fillStrategy = new SimpleFillStrategy(itemsPool);
            _game.SetGameBoardFillStrategy(fillStrategy);

            boardView.Build(_game.Board, tileSize, palette, boardRoot, autoFitCamera);

            _game.StartAsync().Forget(Debug.LogException);
            
            Debug.Log("Match3 Game Started!");
        }

        public void TrySwap(int r1, int c1, int r2, int c2)
        {
            if (_game != null)
            {
                _game.SwapItemsAsync(new GridPosition(r1, c1), new GridPosition(r2, c2)).Forget();
            }
        }
        
        public void DebugSwap()
        {
            TrySwap(debugSwapA.x, debugSwapA.y, debugSwapB.x, debugSwapB.y);
        }

        public void RebuildBoard()
        {
            RebuildBoardAsync().Forget(Debug.LogException);
        }

        public void PrintBoard()
        {
            if (_game == null)
            {
                Debug.Log("Game not created.");
                return;
            }

            var board = _game.Board;
            var sb = new StringBuilder();
            for (int r = 0; r < board.RowCount; r++)
            {
                for (int c = 0; c < board.ColumnCount; c++)
                {
                    var slot = board[r, c];
                    sb.Append(slot.HasItem ? slot.ItemId.ToString() : ".");
                    if (c < board.ColumnCount - 1)
                    {
                        sb.Append(' ');
                    }
                }
                sb.AppendLine();
            }
            Debug.Log(sb.ToString());
        }

        private async UniTask RebuildBoardAsync()
        {
            if (_game != null && _game.IsStarted)
            {
                await _game.StopAsync();
            }

            _game?.ResetGameBoard();

            var config = new GameConfig<GameSlot>
            {
                GameBoardDataProvider = new MyGameBoardDataProvider(rows, cols),
                GameBoardSolver = new GameBoardSolver<GameSlot>(new ISequenceDetector<GameSlot>[]
                {
                    new HorizontalLineDetector<GameSlot>(),
                    new VerticalLineDetector<GameSlot>()
                }, new ISpecialItemDetector<GameSlot>[0]),
                ItemSwapper = new MyItemSwapper(),
                LevelGoalsProvider = new MyLevelGoalsProvider(),
                SolvedSequencesConsumers = new ISolvedSequencesConsumer<GameSlot>[] { }
            };

            _game = new MyMatch3Game(config);
            _game.InitGameLevel(0);

            var itemsPool = new ArrayItemsPool(itemIds);
            var fillStrategy = new SimpleFillStrategy(itemsPool);
            _game.SetGameBoardFillStrategy(fillStrategy);

            boardView.Build(_game.Board, tileSize, palette, boardRoot, autoFitCamera);
            await _game.StartAsync();
        }
    }

    public class MyMatch3Game : Match3Game<GameSlot>
    {
        public MyMatch3Game(GameConfig<GameSlot> config) : base(config) { }
        
        public IGameBoard<GameSlot> Board => GameBoard;
        public bool IsStarted { get; private set; }

        protected override void OnGameStarted()
        {
            IsStarted = true;
            Debug.Log("Match3 Game Started");
        }

        protected override void OnGameStopped()
        {
            IsStarted = false;
            Debug.Log("Match3 Game Stopped");
        }
        
        // Expose SwapItemsAsync for testing if needed, or use the protected method via a public wrapper
        public new async UniTask SwapItemsAsync(GridPosition pos1, GridPosition pos2, CancellationToken token = default)
        {
            await base.SwapItemsAsync(pos1, pos2, token);
        }
    }

    public class MyGameBoardDataProvider : IGameBoardDataProvider<GameSlot>
    {
        private readonly int _rows;
        private readonly int _cols;

        public MyGameBoardDataProvider(int rows, int cols)
        {
            _rows = rows;
            _cols = cols;
        }

        public GameSlot[,] GetGameBoardSlots(int level)
        {
            var slots = new GameSlot[_rows, _cols];
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    var slot = new GameSlot();
                    slot.SetPosition(new GridPosition(r, c));
                    slots[r, c] = slot;
                }
            }
            return slots;
        }
    }

    public class MyItemSwapper : IItemSwapper<GameSlot>
    {
        public async UniTask SwapItemsAsync(GameSlot gridSlot1, GameSlot gridSlot2, CancellationToken cancellationToken = default)
        {
            Debug.Log($"Swapping {gridSlot1.GridPosition} (Item: {gridSlot1.ItemId}) <-> {gridSlot2.GridPosition} (Item: {gridSlot2.ItemId})");
            
            // Swap Data
            int tempId = gridSlot1.ItemId;
            bool tempHas = gridSlot1.HasItem;
            
            if (gridSlot2.HasItem) gridSlot1.SetItem(gridSlot2.ItemId);
            else gridSlot1.Clear();
            
            if (tempHas) gridSlot2.SetItem(tempId);
            else gridSlot2.Clear();
            
            await UniTask.Yield();
        }
    }

    public class MyLevelGoalsProvider : ILevelGoalsProvider<GameSlot>
    {
        public LevelGoal<GameSlot>[] GetLevelGoals(int level, IGameBoard<GameSlot> gameBoard)
        {
            return new LevelGoal<GameSlot>[0];
        }
    }
}

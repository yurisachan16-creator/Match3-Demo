using UnityEngine;
using Match3.App;
using Match3.App.Interfaces;
using Match3.Infrastructure;
using Match3.Infrastructure.SequenceDetectors;
using Match3.Core.Structs;
using Cysharp.Threading.Tasks;
using System;
using System.Text;
using System.Threading;

namespace Match3.App.Demo
{
    public class GameController : MonoBehaviour
    {
        private MyMatch3Game _game;
        private bool _busy;
        private CancellationToken _lifetimeToken;
        
        [SerializeField] private int rows = 8;
        [SerializeField] private int cols = 8;
        [SerializeField] private float tileSize = 1f;
        [SerializeField] private ItemVisual[] itemVisuals = new[]
        {
            new ItemVisual { itemId = 1, color = new Color(0.95f, 0.25f, 0.25f) },
            new ItemVisual { itemId = 2, color = new Color(0.20f, 0.55f, 0.95f) },
            new ItemVisual { itemId = 3, color = new Color(0.25f, 0.85f, 0.35f) },
            new ItemVisual { itemId = 4, color = new Color(0.95f, 0.85f, 0.20f) },
            new ItemVisual { itemId = 5, color = new Color(0.75f, 0.30f, 0.95f) }
        };
        [SerializeField] private bool autoFitCamera = true;
        [SerializeField] private Transform boardRoot;
        [SerializeField] private GameBoardView boardView;
        [SerializeField] private BoardInputController inputController;
        [SerializeField] private FpsMonitor fpsMonitor;
        [SerializeField] private BoardDebugOverlay debugOverlay;
        [SerializeField] private Vector2Int debugSwapA = new Vector2Int(0, 0);
        [SerializeField] private Vector2Int debugSwapB = new Vector2Int(0, 1);

        [SerializeField] private float swapDuration = 0.12f;
        [SerializeField] private float clearDuration = 0.12f;
        [SerializeField] private float fallDuration = 0.12f;
        [SerializeField] private float spawnDuration = 0.12f;
        [SerializeField] private float spawnHeight = 6f;
        [SerializeField] private int maxPerFrame = 32;
        [SerializeField] private int maxChains = 10;
        [SerializeField] private bool enableValidation = true;
        [SerializeField] private bool useFixedSeed = true;
        [SerializeField] private int fixedSeed = 12345;

        public bool IsBusy => _busy;
        public IGameBoard<GameSlot> Board => _game?.Board;
        public GameBoardView BoardView => boardView;

        private void Awake()
        {
            _lifetimeToken = this.GetCancellationTokenOnDestroy();
        }

        private void Start()
        {
            StartGameAsync().Forget(Debug.LogException);
        }

        private async UniTask StartGameAsync()
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

            if (inputController == null)
            {
                inputController = GetComponent<BoardInputController>();
                if (inputController == null)
                {
                    inputController = gameObject.AddComponent<BoardInputController>();
                }
            }

            if (fpsMonitor == null)
            {
                fpsMonitor = GetComponent<FpsMonitor>();
                if (fpsMonitor == null)
                {
                    fpsMonitor = gameObject.AddComponent<FpsMonitor>();
                }
            }

            if (debugOverlay == null)
            {
                debugOverlay = GetComponent<BoardDebugOverlay>();
                if (debugOverlay == null)
                {
                    debugOverlay = gameObject.AddComponent<BoardDebugOverlay>();
                }
            }

            var solver = new GameBoardSolver<GameSlot>(new ISequenceDetector<GameSlot>[]
            {
                new HorizontalLineDetector<GameSlot>(),
                new VerticalLineDetector<GameSlot>()
            }, new ISpecialItemDetector<GameSlot>[0]);

            var itemSwapper = new MyItemSwapper(boardView, swapDuration);

            var config = new GameConfig<GameSlot>
            {
                GameBoardDataProvider = new MyGameBoardDataProvider(rows, cols),
                GameBoardSolver = solver,
                ItemSwapper = itemSwapper,
                LevelGoalsProvider = new MyLevelGoalsProvider(),
                SolvedSequencesConsumers = new ISolvedSequencesConsumer<GameSlot>[] { }
            };

            _game = new MyMatch3Game(config);
            
            _game.InitGameLevel(0);

            var itemsPool = new ArrayItemsPool(GetItemIds(), useFixedSeed ? fixedSeed : (int?)null);
            var fillStrategy = new GravityFillStrategy(itemsPool, boardView, solver, clearDuration, fallDuration, spawnDuration, spawnHeight, maxPerFrame, maxChains, enableValidation);
            _game.SetGameBoardFillStrategy(fillStrategy);

            boardView.Build(_game.Board, tileSize, itemVisuals, boardRoot, autoFitCamera);

            try
            {
                await _game.StartAsync(_lifetimeToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Debug.Log("Match3 Game Started!");

            if (enableValidation)
            {
                var mismatches = boardView.ValidateAndFix();
                if (mismatches > 0)
                {
                    Debug.LogWarning($"BoardView mismatches fixed: {mismatches}");
                }
            }
        }

        public void TrySwap(int r1, int c1, int r2, int c2)
        {
            TrySwapAsync(new GridPosition(r1, c1), new GridPosition(r2, c2)).Forget(Debug.LogException);
        }
        
        public void DebugSwap()
        {
            TrySwap(debugSwapA.x, debugSwapA.y, debugSwapB.x, debugSwapB.y);
        }

        private async UniTask TrySwapAsync(GridPosition a, GridPosition b)
        {
            if (_game == null || _busy)
            {
                return;
            }

            _busy = true;
            try
            {
                Match3DebugLog.Record($"Swap {a.RowIndex},{a.ColumnIndex} <-> {b.RowIndex},{b.ColumnIndex}");
                await _game.SwapItemsAsync(a, b, _lifetimeToken);
            }
            finally
            {
                _busy = false;
            }
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

            var solver = new GameBoardSolver<GameSlot>(new ISequenceDetector<GameSlot>[]
            {
                new HorizontalLineDetector<GameSlot>(),
                new VerticalLineDetector<GameSlot>()
            }, new ISpecialItemDetector<GameSlot>[0]);

            var itemSwapper = new MyItemSwapper(boardView, swapDuration);

            var config = new GameConfig<GameSlot>
            {
                GameBoardDataProvider = new MyGameBoardDataProvider(rows, cols),
                GameBoardSolver = solver,
                ItemSwapper = itemSwapper,
                LevelGoalsProvider = new MyLevelGoalsProvider(),
                SolvedSequencesConsumers = new ISolvedSequencesConsumer<GameSlot>[] { }
            };

            _game = new MyMatch3Game(config);
            _game.InitGameLevel(0);

            var itemsPool = new ArrayItemsPool(GetItemIds(), useFixedSeed ? fixedSeed : (int?)null);
            var fillStrategy = new GravityFillStrategy(itemsPool, boardView, solver, clearDuration, fallDuration, spawnDuration, spawnHeight, maxPerFrame, maxChains, enableValidation);
            _game.SetGameBoardFillStrategy(fillStrategy);

            boardView.Build(_game.Board, tileSize, itemVisuals, boardRoot, autoFitCamera);
            await _game.StartAsync(_lifetimeToken);
        }

        public void SetBusy(bool busy)
        {
            _busy = busy;
        }

        public bool HasItemAt(GridPosition pos)
        {
            if (_game == null)
            {
                return false;
            }

            if (_game.Board.IsPositionOnBoard(pos) == false)
            {
                return false;
            }

            return _game.Board[pos].HasItem;
        }

        public bool CanSwap(GridPosition a, GridPosition b)
        {
            if (_game == null)
            {
                return false;
            }

            if (_game.Board.IsPositionOnBoard(a) == false || _game.Board.IsPositionOnBoard(b) == false)
            {
                return false;
            }

            int manhattan = Mathf.Abs(a.RowIndex - b.RowIndex) + Mathf.Abs(a.ColumnIndex - b.ColumnIndex);
            if (manhattan != 1)
            {
                return false;
            }

            var sa = _game.Board[a];
            var sb = _game.Board[b];
            if (sa.HasItem == false || sb.HasItem == false)
            {
                return false;
            }

            if (sa.IsMovable == false || sb.IsMovable == false)
            {
                return false;
            }

            return true;
        }

        public UniTask SwapAsync(GridPosition a, GridPosition b, CancellationToken ct = default)
        {
            if (_game == null)
            {
                return UniTask.CompletedTask;
            }

            var token = ct.CanBeCanceled ? ct : _lifetimeToken;
            return _game.SwapItemsAsync(a, b, token);
        }

        private int[] GetItemIds()
        {
            if (itemVisuals == null || itemVisuals.Length == 0)
            {
                return new[] { 1 };
            }

            var ids = new int[itemVisuals.Length];
            for (int i = 0; i < itemVisuals.Length; i++)
            {
                ids[i] = itemVisuals[i].itemId;
            }
            return ids;
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
        private readonly GameBoardView _boardView;
        private readonly float _swapDuration;

        public MyItemSwapper(GameBoardView boardView, float swapDuration)
        {
            _boardView = boardView;
            _swapDuration = swapDuration;
        }

        public async UniTask SwapItemsAsync(GameSlot gridSlot1, GameSlot gridSlot2, CancellationToken cancellationToken = default)
        {
            if (_boardView != null)
            {
                await _boardView.AnimateSwapAsync(gridSlot1.GridPosition, gridSlot2.GridPosition, _swapDuration, cancellationToken);
            }

            _boardView?.BeginBatch();
            int tempId = gridSlot1.ItemId;
            bool tempHas = gridSlot1.HasItem;

            if (gridSlot2.HasItem) gridSlot1.SetItem(gridSlot2.ItemId);
            else gridSlot1.Clear();

            if (tempHas) gridSlot2.SetItem(tempId);
            else gridSlot2.Clear();

            _boardView?.EndBatch(refreshDirty: true);
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

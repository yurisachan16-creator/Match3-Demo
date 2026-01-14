using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.App.Interfaces;
using Match3.Core.Structs;
using Match3.Infrastructure;
using QFramework;
using UnityEngine;

namespace Match3.App.Demo
{
    public sealed class GravityFillStrategy : IBoardFillStrategy<GameSlot>
    {
        public string Name => "Gravity Fill Strategy";

        private readonly IItemsPool _itemsPool;
        private readonly GameBoardView _boardView;
        private readonly GameBoardSolver<GameSlot> _solver;

        private readonly float _clearDuration;
        private readonly float _fallDuration;
        private readonly float _spawnDuration;
        private readonly float _spawnHeight;
        private readonly int _maxPerFrame;
        private readonly int _maxChains;
        private readonly bool _enableCrossCheck;

        public GravityFillStrategy(
            IItemsPool itemsPool,
            GameBoardView boardView,
            GameBoardSolver<GameSlot> solver,
            float clearDuration,
            float fallDuration,
            float spawnDuration,
            float spawnHeight,
            int maxPerFrame,
            int maxChains,
            bool enableCrossCheck)
        {
            _itemsPool = itemsPool;
            _boardView = boardView;
            _solver = solver;
            _clearDuration = clearDuration;
            _fallDuration = fallDuration;
            _spawnDuration = spawnDuration;
            _spawnHeight = spawnHeight;
            _maxPerFrame = maxPerFrame;
            _maxChains = maxChains;
            _enableCrossCheck = enableCrossCheck;
        }

        public IEnumerable<IJob> GetFillJobs(IGameBoard<GameSlot> gameBoard)
        {
            yield return new FillJob(this, gameBoard);
        }

        public IEnumerable<IJob> GetSolveJobs(IGameBoard<GameSlot> gameBoard, SolvedData<GameSlot> solvedData)
        {
            yield return new SolveJob(this, gameBoard, solvedData);
        }

        private async UniTask FillAsync(IGameBoard<GameSlot> board, CancellationToken ct)
        {
            var spawnPositions = ListPool<GridPosition>.Get();
            var spawnIds = DictionaryPool<GridPosition, int>.Get();

            try
            {
                for (int r = 0; r < board.RowCount; r++)
                {
                    for (int c = 0; c < board.ColumnCount; c++)
                    {
                        var slot = board[r, c];
                        if (slot.CanContainItem == false || slot.IsMovable == false)
                        {
                            continue;
                        }

                        if (slot.HasItem)
                        {
                            continue;
                        }

                        var pos = new GridPosition(r, c);
                        int id = _itemsPool.GetRandomItemId();
                        spawnIds[pos] = id;
                        spawnPositions.Add(pos);
                    }
                }

                _boardView.BeginBatch();
                foreach (var pos in spawnPositions)
                {
                    ct.ThrowIfCancellationRequested();
                    board[pos].SetItem(spawnIds[pos]);
                }

                await _boardView.AnimateSpawnAsync(spawnPositions, p => spawnIds[p], _spawnDuration, _spawnHeight, _maxPerFrame, ct);
            }
            finally
            {
                _boardView.EndBatch(refreshDirty: true);
                spawnIds.Release2Pool();
                spawnPositions.Release2Pool();
            }

            var solved = SolveWholeBoard(board);
            if (solved.SolvedSequences.Count > 0)
            {
                await ResolveChainAsync(board, solved, ct);
            }
        }

        private async UniTask SolveAndRefillAsync(IGameBoard<GameSlot> board, SolvedData<GameSlot> solvedData, CancellationToken ct)
        {
            if (solvedData == null || solvedData.SolvedSequences.Count == 0)
            {
                return;
            }

            await ResolveChainAsync(board, solvedData, ct);
        }

        private async UniTask CollapseAndSpawnAsync(IGameBoard<GameSlot> board, CancellationToken ct)
        {
            var moves = ListPool<(GridPosition from, GridPosition to)>.Get();
            try
            {
                for (int c = 0; c < board.ColumnCount; c++)
                {
                    int writeRow = board.RowCount - 1;
                    for (int r = board.RowCount - 1; r >= 0; r--)
                    {
                        var slot = board[r, c];
                        if (slot.CanContainItem == false)
                        {
                            writeRow = r - 1;
                            continue;
                        }

                        if (slot.HasItem == false)
                        {
                            continue;
                        }

                        if (r != writeRow)
                        {
                            moves.Add((new GridPosition(r, c), new GridPosition(writeRow, c)));
                        }

                        writeRow--;
                    }
                }

                if (moves.Count > 0)
                {
                    _boardView.BeginBatch();

                    foreach (var move in moves)
                    {
                        ct.ThrowIfCancellationRequested();
                        var from = board[move.from];
                        var to = board[move.to];
                        int id = from.ItemId;
                        to.SetItem(id);
                        from.Clear();
                    }

                    await _boardView.AnimateMovesAsync(moves, _fallDuration, _maxPerFrame, ct);
                }
            }
            finally
            {
                _boardView.EndBatch(refreshDirty: true);
                moves.Release2Pool();
            }

            var spawnPositions = ListPool<GridPosition>.Get();
            var spawnIds = DictionaryPool<GridPosition, int>.Get();

            try
            {
                for (int r = 0; r < board.RowCount; r++)
                {
                    for (int c = 0; c < board.ColumnCount; c++)
                    {
                        var slot = board[r, c];
                        if (slot.CanContainItem == false || slot.IsMovable == false)
                        {
                            continue;
                        }

                        if (slot.HasItem)
                        {
                            continue;
                        }

                        var pos = new GridPosition(r, c);
                        int id = _itemsPool.GetRandomItemId();
                        spawnIds[pos] = id;
                        spawnPositions.Add(pos);
                    }
                }

                if (spawnPositions.Count == 0)
                {
                    return;
                }

                _boardView.BeginBatch();
                foreach (var pos in spawnPositions)
                {
                    ct.ThrowIfCancellationRequested();
                    board[pos].SetItem(spawnIds[pos]);
                }

                await _boardView.AnimateSpawnAsync(spawnPositions, p => spawnIds[p], _spawnDuration, _spawnHeight, _maxPerFrame, ct);
            }
            finally
            {
                _boardView.EndBatch(refreshDirty: true);
                spawnIds.Release2Pool();
                spawnPositions.Release2Pool();
            }
        }

        private SolvedData<GameSlot> SolveWholeBoard(IGameBoard<GameSlot> board)
        {
            var positions = ListPool<GridPosition>.Get();
            try
            {
                for (int r = 0; r < board.RowCount; r++)
                {
                    for (int c = 0; c < board.ColumnCount; c++)
                    {
                        positions.Add(new GridPosition(r, c));
                    }
                }

                var solved = _solver.Solve(board, positions.ToArray());

                if (_enableCrossCheck)
                {
                    var expected = new HashSet<GridPosition>();
                    SimpleMatchScanner.CollectMatchedPositions(board, expected);

                    var actual = new HashSet<GridPosition>();
                    foreach (var slot in solved.GetSolvedGridSlots(onlyMovable: true))
                    {
                        actual.Add(slot.GridPosition);
                    }

                    if (expected.SetEquals(actual) == false)
                    {
                        Debug.LogWarning($"Match mismatch. Expected={expected.Count} Actual={actual.Count}");
                    }
                }

                return solved;
            }
            finally
            {
                positions.Release2Pool();
            }
        }

        private async UniTask ResolveChainAsync(IGameBoard<GameSlot> board, SolvedData<GameSlot> solvedData, CancellationToken ct)
        {
            var current = solvedData;
            for (int chain = 0; chain < _maxChains; chain++)
            {
                ct.ThrowIfCancellationRequested();

                if (current == null || current.SolvedSequences.Count == 0)
                {
                    return;
                }

                await ClearSolvedAsync(board, current, ct);
                Match3DebugLog.Record($"Chain {chain + 1}: cleared");
                await CollapseAndSpawnAsync(board, ct);
                Match3DebugLog.Record($"Chain {chain + 1}: collapsed/spawned");
                await EnsureNoHolesAsync(board, ct);

                var mismatches = _boardView.ValidateAndFix();
                if (mismatches > 0)
                {
                    Debug.LogWarning($"BoardView mismatches fixed: {mismatches}");
                    Match3DebugLog.Record($"Chain {chain + 1}: mismatches fixed {mismatches}");
                }

                current = SolveWholeBoard(board);
            }

            if (current != null && current.SolvedSequences.Count > 0)
            {
                Debug.LogWarning($"Max chains reached: {_maxChains}");
            }
        }

        private async UniTask ClearSolvedAsync(IGameBoard<GameSlot> board, SolvedData<GameSlot> solvedData, CancellationToken ct)
        {
            var clearPositions = ListPool<GridPosition>.Get();
            try
            {
                foreach (var slot in solvedData.GetSolvedGridSlots(onlyMovable: true))
                {
                    clearPositions.Add(slot.GridPosition);
                }

                await _boardView.AnimateClearAsync(clearPositions, _clearDuration, _maxPerFrame, ct);

                _boardView.BeginBatch();
                foreach (var pos in clearPositions)
                {
                    ct.ThrowIfCancellationRequested();
                    board[pos].Clear();
                }
            }
            finally
            {
                _boardView.EndBatch(refreshDirty: true);
                clearPositions.Release2Pool();
            }
        }

        private async UniTask EnsureNoHolesAsync(IGameBoard<GameSlot> board, CancellationToken ct)
        {
            var spawnPositions = ListPool<GridPosition>.Get();
            var spawnIds = DictionaryPool<GridPosition, int>.Get();

            try
            {
                for (int r = 0; r < board.RowCount; r++)
                {
                    for (int c = 0; c < board.ColumnCount; c++)
                    {
                        var slot = board[r, c];
                        if (slot.CanContainItem == false || slot.IsMovable == false)
                        {
                            continue;
                        }

                        if (slot.HasItem)
                        {
                            continue;
                        }

                        var pos = new GridPosition(r, c);
                        int id = _itemsPool.GetRandomItemId();
                        spawnIds[pos] = id;
                        spawnPositions.Add(pos);
                    }
                }

                if (spawnPositions.Count == 0)
                {
                    return;
                }

                _boardView.BeginBatch();
                foreach (var pos in spawnPositions)
                {
                    ct.ThrowIfCancellationRequested();
                    board[pos].SetItem(spawnIds[pos]);
                }

                await _boardView.AnimateSpawnAsync(spawnPositions, p => spawnIds[p], _spawnDuration, _spawnHeight, _maxPerFrame, ct);
            }
            finally
            {
                _boardView.EndBatch(refreshDirty: true);
                spawnIds.Release2Pool();
                spawnPositions.Release2Pool();
            }
        }

        private sealed class FillJob : IJob
        {
            private readonly GravityFillStrategy _strategy;
            private readonly IGameBoard<GameSlot> _board;

            public FillJob(GravityFillStrategy strategy, IGameBoard<GameSlot> board)
            {
                _strategy = strategy;
                _board = board;
            }

            public int ExecutionOrder => 0;

            public UniTask ExecuteAsync(CancellationToken cancellationToken = default)
            {
                return _strategy.FillAsync(_board, cancellationToken);
            }
        }

        private sealed class SolveJob : IJob
        {
            private readonly GravityFillStrategy _strategy;
            private readonly IGameBoard<GameSlot> _board;
            private readonly SolvedData<GameSlot> _solvedData;

            public SolveJob(GravityFillStrategy strategy, IGameBoard<GameSlot> board, SolvedData<GameSlot> solvedData)
            {
                _strategy = strategy;
                _board = board;
                _solvedData = solvedData;
            }

            public int ExecutionOrder => 0;

            public UniTask ExecuteAsync(CancellationToken cancellationToken = default)
            {
                return _strategy.SolveAndRefillAsync(_board, _solvedData, cancellationToken);
            }
        }
    }
}

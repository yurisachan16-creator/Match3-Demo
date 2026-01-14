using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.Core.Structs;
using UnityEngine;

namespace Match3.App.Demo
{
    public class Match3StabilityTester : MonoBehaviour
    {
        [SerializeField] private GameController controller;
        [SerializeField] private bool runOnStart;
        [SerializeField] private int swaps = 500;
        [SerializeField] private int seed = 777;
        [SerializeField] private int validateEvery = 1;
        [SerializeField] private float delaySeconds = 0f;

        private CancellationTokenSource _cts;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<GameController>();
            }
        }

        private void OnEnable()
        {
            if (runOnStart)
            {
                Run();
            }
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public void Run()
        {
            RunAsync().Forget(Debug.LogException);
        }

        private async UniTask RunAsync()
        {
            if (controller == null || controller.Board == null)
            {
                Debug.LogWarning("StabilityTester: controller/board not ready.");
                return;
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            var rng = new System.Random(seed);
            int mismatchTotal = 0;

            for (int i = 0; i < swaps; i++)
            {
                ct.ThrowIfCancellationRequested();

                var board = controller.Board;
                var a = new GridPosition(rng.Next(0, board.RowCount), rng.Next(0, board.ColumnCount));
                var dir = rng.Next(0, 4);
                var b = dir switch
                {
                    0 => new GridPosition(a.RowIndex - 1, a.ColumnIndex),
                    1 => new GridPosition(a.RowIndex + 1, a.ColumnIndex),
                    2 => new GridPosition(a.RowIndex, a.ColumnIndex - 1),
                    _ => new GridPosition(a.RowIndex, a.ColumnIndex + 1)
                };

                if (controller.CanSwap(a, b))
                {
                    await controller.SwapAsync(a, b, ct);
                }

                if (validateEvery > 0 && (i % validateEvery == 0))
                {
                    mismatchTotal += controller.BoardView != null ? controller.BoardView.ValidateAndFix() : 0;
                }

                if (delaySeconds > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: ct);
                }
                else
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }

            Debug.Log($"StabilityTester finished. swaps={swaps} mismatchesFixed={mismatchTotal}");
        }
    }
}

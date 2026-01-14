using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.App.Interfaces;
using Match3.Core.Structs;
using QFramework;
using UnityEngine;

namespace Match3.App.Demo
{
    public class GameBoardView : MonoBehaviour
    {
        public sealed class DragGhost
        {
            private readonly GameObject _go;
            private readonly SpriteRenderer _renderer;

            internal DragGhost(GameObject go, SpriteRenderer renderer)
            {
                _go = go;
                _renderer = renderer;
            }

            public void SetWorldPosition(Vector3 world)
            {
                _go.transform.position = world;
            }

            internal void SetColor(Color c)
            {
                _renderer.color = c;
            }

            internal GameObject GameObject => _go;
        }

        private struct SlotView
        {
            public Vector3 AnchorWorld;
            public GameSlot Slot;
            public GameObject ItemGo;
            public SpriteRenderer ItemRenderer;
            public bool Highlighted;
        }

        private readonly Dictionary<GridPosition, SlotView> _slots = new Dictionary<GridPosition, SlotView>();
        private readonly List<GameSlot> _subscribedSlots = new List<GameSlot>();

        private Transform _root;
        private float _tileSize;
        private int _rows;
        private int _cols;
        private Color[] _palette;

        private Sprite _sprite;

        private SimpleObjectPool<GameObject> _itemPool;
        private SimpleObjectPool<GameObject> _ghostPool;

        private int _batchDepth;
        private readonly HashSet<GridPosition> _dirtyPositions = new HashSet<GridPosition>();

        public void Build(IGameBoard<GameSlot> board, float tileSize, Color[] palette, Transform root, bool autoFitCamera)
        {
            Clear();

            _root = root != null ? root : transform;
            _tileSize = tileSize <= 0 ? 1f : tileSize;
            _rows = board.RowCount;
            _cols = board.ColumnCount;
            _palette = palette;
            _sprite = CreateDefaultSprite();

            _itemPool ??= new SimpleObjectPool<GameObject>(CreateItemGo, ResetPooledGo, initCount: 0);
            _ghostPool ??= new SimpleObjectPool<GameObject>(CreateGhostGo, ResetPooledGo, initCount: 0);

            for (int r = 0; r < board.RowCount; r++)
            {
                for (int c = 0; c < board.ColumnCount; c++)
                {
                    var slot = board[r, c];
                    var pos = new GridPosition(r, c);
                    var anchorLocal = new Vector3(c * _tileSize, -r * _tileSize, 0f);
                    var anchorWorld = _root.TransformPoint(anchorLocal);

                    var view = new SlotView
                    {
                        AnchorWorld = anchorWorld,
                        Slot = slot,
                        ItemGo = null,
                        ItemRenderer = null,
                        Highlighted = false
                    };

                    _slots[pos] = view;

                    slot.OnItemChanged += OnSlotChanged;
                    _subscribedSlots.Add(slot);

                    SyncSlotVisual(pos, slot, animate: false);
                }
            }

            if (autoFitCamera)
            {
                FitCamera(board.RowCount, board.ColumnCount, _tileSize);
            }
        }

        public void Clear()
        {
            foreach (var slot in _subscribedSlots)
            {
                slot.OnItemChanged -= OnSlotChanged;
            }
            _subscribedSlots.Clear();

            foreach (var kv in _slots)
            {
                var view = kv.Value;
                if (view.ItemGo != null)
                {
                    _itemPool?.Recycle(view.ItemGo);
                }
            }
            _slots.Clear();
            _dirtyPositions.Clear();
            _batchDepth = 0;
        }

        private void OnDestroy()
        {
            Clear();
            _itemPool?.Clear(go => Destroy(go));
            _ghostPool?.Clear(go => Destroy(go));
        }

        public bool TryWorldToGrid(Vector3 world, out GridPosition pos)
        {
            pos = default;

            if (_root == null)
            {
                return false;
            }

            var local = _root.InverseTransformPoint(world);
            int col = Mathf.RoundToInt(local.x / _tileSize);
            int row = Mathf.RoundToInt(-local.y / _tileSize);

            if (row < 0 || row >= _rows || col < 0 || col >= _cols)
            {
                return false;
            }

            pos = new GridPosition(row, col);
            return true;
        }

        public void SetHighlight(GridPosition pos, bool highlight)
        {
            if (_slots.TryGetValue(pos, out var view) == false)
            {
                return;
            }

            if (view.Highlighted == highlight)
            {
                return;
            }

            view.Highlighted = highlight;
            if (view.ItemGo != null)
            {
                view.ItemGo.transform.localScale = highlight ? Vector3.one * 1.12f : Vector3.one;
                view.ItemRenderer.sortingOrder = highlight ? 10 : 1;
            }
            _slots[pos] = view;
        }

        public Color GetItemColorAt(GridPosition pos)
        {
            if (_slots.TryGetValue(pos, out var view) == false)
            {
                return Color.clear;
            }

            if (view.Slot == null || view.Slot.HasItem == false)
            {
                return Color.clear;
            }

            return ItemIdToColor(view.Slot.ItemId, 1f);
        }

        public DragGhost AllocateGhost(Color baseColor, float alpha)
        {
            var go = _ghostPool.Allocate();
            go.SetActive(true);
            go.transform.SetParent(_root, true);

            var renderer = go.GetComponent<SpriteRenderer>();
            var c = baseColor;
            c.a = Mathf.Clamp01(alpha);
            renderer.color = c;
            renderer.sortingOrder = 100;

            return new DragGhost(go, renderer);
        }

        public void RecycleGhost(DragGhost ghost)
        {
            if (ghost == null)
            {
                return;
            }

            _ghostPool.Recycle(ghost.GameObject);
        }

        public void BeginBatch()
        {
            _batchDepth++;
        }

        public void EndBatch(bool refreshDirty = true)
        {
            _batchDepth = Mathf.Max(0, _batchDepth - 1);
            if (_batchDepth == 0 && refreshDirty)
            {
                foreach (var pos in _dirtyPositions)
                {
                    if (_slots.TryGetValue(pos, out var view))
                    {
                        SyncSlotVisual(pos, view.Slot, animate: false);
                    }
                }
                _dirtyPositions.Clear();
            }
        }

        public async UniTask AnimateSwapAsync(GridPosition a, GridPosition b, float duration, CancellationToken ct = default)
        {
            if (_slots.TryGetValue(a, out var va) == false || _slots.TryGetValue(b, out var vb) == false)
            {
                return;
            }

            if (va.ItemGo == null || vb.ItemGo == null)
            {
                return;
            }

            var goA = va.ItemGo;
            var goB = vb.ItemGo;
            var startA = goA.transform.position;
            var startB = goB.transform.position;
            float t = 0f;
            while (t < duration)
            {
                ct.ThrowIfCancellationRequested();
                t += Time.deltaTime;
                float p = duration <= 0 ? 1f : Mathf.Clamp01(t / duration);
                goA.transform.position = Vector3.Lerp(startA, startB, p);
                goB.transform.position = Vector3.Lerp(startB, startA, p);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            goA.transform.position = startB;
            goB.transform.position = startA;

            var rendererA = va.ItemRenderer;
            var rendererB = vb.ItemRenderer;

            va.ItemGo = goB;
            va.ItemRenderer = rendererB;
            vb.ItemGo = goA;
            vb.ItemRenderer = rendererA;

            _slots[a] = va;
            _slots[b] = vb;
        }

        public async UniTask AnimateClearAsync(IReadOnlyList<GridPosition> positions, float duration, int maxPerFrame, CancellationToken ct = default)
        {
            int processed = 0;
            foreach (var pos in positions)
            {
                ct.ThrowIfCancellationRequested();
                if (_slots.TryGetValue(pos, out var view) == false || view.ItemGo == null)
                {
                    continue;
                }

                var go = view.ItemGo;
                var renderer = view.ItemRenderer;
                var startScale = go.transform.localScale;
                var startColor = renderer.color;
                float t = 0f;
                while (t < duration)
                {
                    ct.ThrowIfCancellationRequested();
                    t += Time.deltaTime;
                    float p = duration <= 0 ? 1f : Mathf.Clamp01(t / duration);
                    go.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, p);
                    var c = startColor;
                    c.a = Mathf.Lerp(startColor.a, 0f, p);
                    renderer.color = c;
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                view.ItemGo = null;
                view.ItemRenderer = null;
                _slots[pos] = view;

                _itemPool.Recycle(go);

                processed++;
                if (maxPerFrame > 0 && processed >= maxPerFrame)
                {
                    processed = 0;
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
        }

        public async UniTask AnimateMovesAsync(IReadOnlyList<(GridPosition from, GridPosition to)> moves, float duration, int maxPerFrame, CancellationToken ct = default)
        {
            int processed = 0;
            foreach (var move in moves)
            {
                ct.ThrowIfCancellationRequested();
                if (_slots.TryGetValue(move.from, out var fromView) == false ||
                    _slots.TryGetValue(move.to, out var toView) == false)
                {
                    continue;
                }

                if (fromView.ItemGo == null)
                {
                    continue;
                }

                var go = fromView.ItemGo;
                var renderer = fromView.ItemRenderer;

                var start = go.transform.position;
                var end = toView.AnchorWorld;

                float t = 0f;
                while (t < duration)
                {
                    ct.ThrowIfCancellationRequested();
                    t += Time.deltaTime;
                    float p = duration <= 0 ? 1f : Mathf.Clamp01(t / duration);
                    go.transform.position = Vector3.Lerp(start, end, p);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
                go.transform.position = end;

                fromView.ItemGo = null;
                fromView.ItemRenderer = null;
                toView.ItemGo = go;
                toView.ItemRenderer = renderer;
                _slots[move.from] = fromView;
                _slots[move.to] = toView;

                processed++;
                if (maxPerFrame > 0 && processed >= maxPerFrame)
                {
                    processed = 0;
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
        }

        public async UniTask AnimateSpawnAsync(IReadOnlyList<GridPosition> positions, Func<GridPosition, int> itemIdProvider, float duration, float spawnHeight, int maxPerFrame, CancellationToken ct = default)
        {
            int processed = 0;
            foreach (var pos in positions)
            {
                ct.ThrowIfCancellationRequested();
                if (_slots.TryGetValue(pos, out var view) == false)
                {
                    continue;
                }

                if (view.ItemGo != null)
                {
                    continue;
                }

                var go = _itemPool.Allocate();
                go.SetActive(true);
                go.transform.SetParent(_root, true);
                go.transform.localScale = Vector3.one;

                var renderer = go.GetComponent<SpriteRenderer>();
                int itemId = itemIdProvider(pos);
                renderer.color = ItemIdToColor(itemId, 1f);
                renderer.sortingOrder = 1;

                var start = view.AnchorWorld + Vector3.up * spawnHeight;
                var end = view.AnchorWorld;
                go.transform.position = start;

                float t = 0f;
                while (t < duration)
                {
                    ct.ThrowIfCancellationRequested();
                    t += Time.deltaTime;
                    float p = duration <= 0 ? 1f : Mathf.Clamp01(t / duration);
                    go.transform.position = Vector3.Lerp(start, end, p);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
                go.transform.position = end;

                view.ItemGo = go;
                view.ItemRenderer = renderer;
                _slots[pos] = view;

                processed++;
                if (maxPerFrame > 0 && processed >= maxPerFrame)
                {
                    processed = 0;
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
        }

        private void OnSlotChanged(GameSlot slot)
        {
            if (slot == null)
            {
                return;
            }

            var pos = slot.GridPosition;
            if (_batchDepth > 0)
            {
                _dirtyPositions.Add(pos);
                return;
            }

            SyncSlotVisual(pos, slot, animate: false);
        }

        private void SyncSlotVisual(GridPosition pos, GameSlot slot, bool animate)
        {
            if (_slots.TryGetValue(pos, out var view) == false)
            {
                return;
            }

            if (slot.HasItem)
            {
                if (view.ItemGo == null)
                {
                    var go = _itemPool.Allocate();
                    go.SetActive(true);
                    go.transform.SetParent(_root, true);
                    go.transform.position = view.AnchorWorld;
                    go.transform.localScale = view.Highlighted ? Vector3.one * 1.12f : Vector3.one;

                    var renderer = go.GetComponent<SpriteRenderer>();
                    renderer.sprite = _sprite;
                    renderer.color = ItemIdToColor(slot.ItemId, 1f);
                    renderer.sortingOrder = view.Highlighted ? 10 : 1;

                    view.ItemGo = go;
                    view.ItemRenderer = renderer;
                    _slots[pos] = view;
                }
                else
                {
                    view.ItemRenderer.color = ItemIdToColor(slot.ItemId, 1f);
                    _slots[pos] = view;
                }
            }
            else
            {
                if (view.ItemGo != null)
                {
                    _itemPool.Recycle(view.ItemGo);
                    view.ItemGo = null;
                    view.ItemRenderer = null;
                    _slots[pos] = view;
                }
            }
        }

        private Color ItemIdToColor(int itemId, float alpha)
        {
            if (itemId <= 0 || _palette == null || _palette.Length == 0)
            {
                return new Color(1f, 1f, 1f, alpha);
            }

            int idx = (itemId - 1) % _palette.Length;
            var c = _palette[idx];
            c.a = alpha;
            return c;
        }

        private GameObject CreateItemGo()
        {
            var go = new GameObject("Item");
            go.SetActive(false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite ?? CreateDefaultSprite();
            sr.sortingOrder = 1;
            return go;
        }

        private GameObject CreateGhostGo()
        {
            var go = new GameObject("DragGhost");
            go.SetActive(false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite ?? CreateDefaultSprite();
            sr.sortingOrder = 100;
            return go;
        }

        private void ResetPooledGo(GameObject go)
        {
            if (go == null)
            {
                return;
            }
            go.transform.localScale = Vector3.one;
            go.transform.rotation = Quaternion.identity;
            go.SetActive(false);
        }

        private static void FitCamera(int rows, int cols, float tileSize)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            cam.orthographic = true;

            float width = Mathf.Max(1, cols) * tileSize;
            float height = Mathf.Max(1, rows) * tileSize;
            float halfHeight = height * 0.5f;
            float halfWidth = width * 0.5f;

            float sizeByHeight = halfHeight + tileSize;
            float sizeByWidth = (halfWidth / Mathf.Max(0.0001f, cam.aspect)) + tileSize;
            cam.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth);

            cam.transform.position = new Vector3((width - tileSize) * 0.5f, (-height + tileSize) * 0.5f, cam.transform.position.z);
        }

        private static Sprite CreateDefaultSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}

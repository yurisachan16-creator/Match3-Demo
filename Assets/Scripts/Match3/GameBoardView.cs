using System.Collections.Generic;
using Match3.App.Interfaces;
using Match3.Core.Structs;
using UnityEngine;

namespace Match3.App.Demo
{
    public class GameBoardView : MonoBehaviour
    {
        private readonly Dictionary<GridPosition, SpriteRenderer> _tileRenderers = new Dictionary<GridPosition, SpriteRenderer>();
        private readonly List<GameSlot> _subscribedSlots = new List<GameSlot>();

        private Sprite _tileSprite;
        private Color[] _palette;
        private Transform _root;
        private float _tileSize;

        public void Build(IGameBoard<GameSlot> board, float tileSize, Color[] palette, Transform root, bool autoFitCamera)
        {
            Clear();

            _palette = palette;
            _root = root;
            _tileSize = tileSize <= 0 ? 1f : tileSize;

            if (_root == null)
            {
                _root = transform;
            }

            _tileSprite = CreateDefaultSprite();

            for (int r = 0; r < board.RowCount; r++)
            {
                for (int c = 0; c < board.ColumnCount; c++)
                {
                    var slot = board[r, c];
                    var pos = new GridPosition(r, c);

                    var tileGo = new GameObject($"Tile_{r}_{c}");
                    tileGo.transform.SetParent(_root, false);
                    tileGo.transform.localPosition = new Vector3(c * _tileSize, -r * _tileSize, 0f);
                    tileGo.transform.localScale = new Vector3(_tileSize, _tileSize, 1f);

                    var renderer = tileGo.AddComponent<SpriteRenderer>();
                    renderer.sprite = _tileSprite;
                    renderer.color = GetColor(slot);

                    _tileRenderers[pos] = renderer;

                    slot.OnItemChanged += OnSlotChanged;
                    _subscribedSlots.Add(slot);
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

            if (_root != null)
            {
                for (int i = _root.childCount - 1; i >= 0; i--)
                {
                    var child = _root.GetChild(i).gameObject;
                    if (Application.isPlaying)
                    {
                        Destroy(child);
                    }
                    else
                    {
                        DestroyImmediate(child);
                    }
                }
            }

            _tileRenderers.Clear();
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void OnSlotChanged(GameSlot slot)
        {
            if (_tileRenderers.TryGetValue(slot.GridPosition, out var renderer))
            {
                renderer.color = GetColor(slot);
            }
        }

        private Color GetColor(GameSlot slot)
        {
            if (slot == null || slot.HasItem == false)
            {
                return new Color(0f, 0f, 0f, 0f);
            }

            int index = slot.ItemId - 1;
            if (_palette == null || index < 0 || index >= _palette.Length)
            {
                return Color.white;
            }

            return _palette[index];
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

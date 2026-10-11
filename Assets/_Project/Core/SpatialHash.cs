using System;
using System.Collections.Generic;

namespace WormRush.Core
{
    /// <summary>
    /// Uniform grid over the arena. Rebuilt every step (Clear + Insert), queried per head.
    /// Stores an int payload per point; the caller decides what it means (food index,
    /// packed worm/segment). Points outside the grid land in the border cells, so a query
    /// never misses them.
    /// </summary>
    public sealed class SpatialHash
    {
        readonly float _invCell;
        readonly float _origin;
        readonly int _dim;
        readonly int[] _cellHead;
        int[] _next;
        int[] _payload;
        int _count;

        public SpatialHash(float cellSize, float halfExtent, int initialCapacity = 512)
        {
            _invCell = 1f / cellSize;
            _origin = -halfExtent;
            _dim = Math.Max(1, (int)Math.Ceiling(2f * halfExtent / cellSize));
            _cellHead = new int[_dim * _dim];
            int capacity = Math.Max(16, initialCapacity);
            _next = new int[capacity];
            _payload = new int[capacity];
            Clear();
        }

        public int Count
        {
            get { return _count; }
        }

        public void Clear()
        {
            Array.Fill(_cellHead, -1);
            _count = 0;
        }

        public void Insert(float x, float y, int payload)
        {
            if (_count == _payload.Length)
            {
                Array.Resize(ref _payload, _count * 2);
                Array.Resize(ref _next, _count * 2);
            }

            int cell = CellY(y) * _dim + CellX(x);
            _payload[_count] = payload;
            _next[_count] = _cellHead[cell];
            _cellHead[cell] = _count;
            _count++;
        }

        /// <summary>
        /// Replaces <paramref name="results"/> with the payloads of every point in the cells touched by
        /// the square [x-r, x+r] x [y-r, y+r]. A superset of the circle: the caller does the exact test.
        /// Order is deterministic.
        /// </summary>
        public void Query(float x, float y, float radius, List<int> results)
        {
            results.Clear();
            int x0 = CellX(x - radius);
            int x1 = CellX(x + radius);
            int y0 = CellY(y - radius);
            int y1 = CellY(y + radius);
            for (int cy = y0; cy <= y1; cy++)
            {
                int row = cy * _dim;
                for (int cx = x0; cx <= x1; cx++)
                {
                    for (int i = _cellHead[row + cx]; i >= 0; i = _next[i])
                        results.Add(_payload[i]);
                }
            }
        }

        int CellX(float x)
        {
            return Clamp((int)Math.Floor((x - _origin) * _invCell));
        }

        int CellY(float y)
        {
            return Clamp((int)Math.Floor((y - _origin) * _invCell));
        }

        int Clamp(int c)
        {
            return c < 0 ? 0 : (c >= _dim ? _dim - 1 : c);
        }
    }
}

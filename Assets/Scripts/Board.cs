using System.Collections.Generic;

namespace BlockBlast
{
    public struct ClearedCell
    {
        public int X, Y, ColorIndex;
    }

    /// <summary>Pure game-rules model: an 8x8 grid. 0 = empty, n>0 = colour index n-1.</summary>
    public class Board
    {
        public const int Size = 8;
        public readonly int[,] Cells = new int[Size, Size];

        public void Reset()
        {
            System.Array.Clear(Cells, 0, Cells.Length);
        }

        public bool CanPlace(Shape s, int ox, int oy)
        {
            foreach (var c in s.Cells)
            {
                int x = ox + c.x, y = oy + c.y;
                if (x < 0 || y < 0 || x >= Size || y >= Size) return false;
                if (Cells[x, y] != 0) return false;
            }
            return true;
        }

        public bool CanPlaceAnywhere(Shape s)
        {
            for (int y = 0; y <= Size - s.Height; y++)
                for (int x = 0; x <= Size - s.Width; x++)
                    if (CanPlace(s, x, y)) return true;
            return false;
        }

        public void Place(Shape s, int ox, int oy, int value)
        {
            foreach (var c in s.Cells) Cells[ox + c.x, oy + c.y] = value;
        }

        public bool IsEmpty()
        {
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    if (Cells[x, y] != 0) return false;
            return true;
        }

        bool RowFull(int y)
        {
            for (int x = 0; x < Size; x++) if (Cells[x, y] == 0) return false;
            return true;
        }

        bool ColFull(int x)
        {
            for (int y = 0; y < Size; y++) if (Cells[x, y] == 0) return false;
            return true;
        }

        public void FindFullLines(List<int> rows, List<int> cols)
        {
            rows.Clear();
            cols.Clear();
            for (int i = 0; i < Size; i++)
            {
                if (RowFull(i)) rows.Add(i);
                if (ColFull(i)) cols.Add(i);
            }
        }

        /// <summary>Which lines WOULD complete if the shape were dropped here (board is left untouched).</summary>
        public void PreviewLines(Shape s, int ox, int oy, List<int> rows, List<int> cols)
        {
            foreach (var c in s.Cells) Cells[ox + c.x, oy + c.y] = -1;
            FindFullLines(rows, cols);
            foreach (var c in s.Cells) Cells[ox + c.x, oy + c.y] = 0;
        }

        readonly List<int> rowsBuf = new List<int>();
        readonly List<int> colsBuf = new List<int>();

        /// <summary>Clears every full row and column at once. Returns the number of lines cleared.</summary>
        public int ClearFullLines(List<ClearedCell> cleared)
        {
            cleared.Clear();
            FindFullLines(rowsBuf, colsBuf);
            if (rowsBuf.Count == 0 && colsBuf.Count == 0) return 0;

            var mark = new bool[Size, Size];
            foreach (int y in rowsBuf) for (int x = 0; x < Size; x++) mark[x, y] = true;
            foreach (int x in colsBuf) for (int y = 0; y < Size; y++) mark[x, y] = true;

            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    if (mark[x, y])
                    {
                        cleared.Add(new ClearedCell { X = x, Y = y, ColorIndex = Cells[x, y] - 1 });
                        Cells[x, y] = 0;
                    }

            return rowsBuf.Count + colsBuf.Count;
        }
    }
}

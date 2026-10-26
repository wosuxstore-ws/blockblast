using System.Collections.Generic;
using UnityEngine;

namespace BlockBlast
{
    /// <summary>An immutable block shape. Cells are normalised so the min x / min y is 0.</summary>
    public class Shape
    {
        public readonly Vector2Int[] Cells;
        public readonly int Width;
        public readonly int Height;
        public readonly string Key;
        public float Weight;

        public Shape(IEnumerable<Vector2Int> source)
        {
            var list = new List<Vector2Int>(source);
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var c in list)
            {
                if (c.x < minX) minX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.x > maxX) maxX = c.x;
                if (c.y > maxY) maxY = c.y;
            }
            for (int i = 0; i < list.Count; i++)
                list[i] = new Vector2Int(list[i].x - minX, list[i].y - minY);
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            Cells = list.ToArray();
            Width = maxX - minX + 1;
            Height = maxY - minY + 1;

            var sb = new System.Text.StringBuilder();
            foreach (var c in Cells) sb.Append(c.x).Append(',').Append(c.y).Append(';');
            Key = sb.ToString();
        }
    }

    /// <summary>All pieces in the game. Rotations / mirrors are generated from ASCII patterns.</summary>
    public static class ShapeLibrary
    {
        public static readonly List<Shape> All = new List<Shape>();
        static readonly float totalWeight;

        static ShapeLibrary()
        {
            //  pattern (rows top->bottom, '/' separates rows), rotate, mirror, weight of the whole family
            Add("#", false, false, 1f);
            Add("##", true, false, 3f);
            Add("###", true, false, 4f);
            Add("####", true, false, 3f);
            Add("#####", true, false, 2f);
            Add("##/##", false, false, 5f);
            Add("###/###/###", false, false, 2f);
            Add("##/##/##", true, false, 3f);
            Add("#./##", true, false, 5f);
            Add("#./#./##", true, true, 5f);
            Add("#../#../###", true, false, 3f);
            Add("###/.#.", true, false, 4f);
            Add(".##/##.", true, true, 4f);

            foreach (var s in All) totalWeight += s.Weight;
        }

        static void Add(string pattern, bool rotate, bool mirror, float familyWeight)
        {
            var rows = pattern.Split('/');
            var cells = new List<Vector2Int>();
            for (int row = 0; row < rows.Length; row++)
                for (int col = 0; col < rows[row].Length; col++)
                    if (rows[row][col] == '#')
                        cells.Add(new Vector2Int(col, rows.Length - 1 - row));

            var seen = new HashSet<string>();
            var variants = new List<Shape>();
            for (int m = 0; m < (mirror ? 2 : 1); m++)
            {
                var cur = new List<Vector2Int>(cells);
                if (m == 1)
                    for (int i = 0; i < cur.Count; i++) cur[i] = new Vector2Int(-cur[i].x, cur[i].y);

                for (int rot = 0; rot < (rotate ? 4 : 1); rot++)
                {
                    var shape = new Shape(cur);
                    if (seen.Add(shape.Key)) variants.Add(shape);
                    for (int i = 0; i < cur.Count; i++) cur[i] = new Vector2Int(cur[i].y, -cur[i].x); // rotate 90
                }
            }

            foreach (var v in variants)
            {
                v.Weight = familyWeight / variants.Count;
                All.Add(v);
            }
        }

        public static Shape Next()
        {
            float r = Random.value * totalWeight;
            foreach (var s in All)
            {
                r -= s.Weight;
                if (r <= 0f) return s;
            }
            return All[All.Count - 1];
        }
    }
}

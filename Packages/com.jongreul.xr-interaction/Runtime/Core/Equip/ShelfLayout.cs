using System;
using System.Collections.Generic;
using System.Numerics;

namespace Jongreul.XrInteraction.Equip
{
    public readonly struct ShelfCell
    {
        public readonly EquipItem Item;
        public readonly int Column;
        public readonly int Row;

        /// <summary>진열대 기준 로컬 위치. 가로는 가운데 정렬, 세로는 위에서 아래로.</summary>
        public readonly Vector3 LocalPosition;

        public ShelfCell(EquipItem item, int column, int row, Vector3 localPosition)
        {
            Item = item;
            Column = column;
            Row = row;
            LocalPosition = localPosition;
        }
    }

    /// <summary>보유 아이템을 카테고리별 격자로 자동 진열한다. 칸이 모자라면 페이지로 넘긴다.</summary>
    public static class ShelfLayout
    {
        public static int PageCount(int itemCount, int columns, int rows)
        {
            Validate(columns, rows);
            int perPage = columns * rows;
            return Math.Max(1, (itemCount + perPage - 1) / perPage);
        }

        /// <param name="category">null이면 전체.</param>
        public static IReadOnlyList<ShelfCell> Arrange(IReadOnlyList<EquipItem> items, string category, int columns,
            int rows, float spacingX, float spacingY, int page = 0)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            Validate(columns, rows);

            var filtered = new List<EquipItem>();
            foreach (EquipItem item in items)
            {
                if (category == null || string.Equals(item.Category, category, StringComparison.Ordinal))
                    filtered.Add(item);
            }

            int perPage = columns * rows;
            int pageCount = PageCount(filtered.Count, columns, rows);
            page = Math.Clamp(page, 0, pageCount - 1);

            var cells = new List<ShelfCell>(perPage);
            int start = page * perPage;
            int end = Math.Min(filtered.Count, start + perPage);
            for (int i = start; i < end; i++)
            {
                int index = i - start;
                int column = index % columns;
                int row = index / columns;
                float x = (column - (columns - 1) * 0.5f) * spacingX;
                float y = -row * spacingY;
                cells.Add(new ShelfCell(filtered[i], column, row, new Vector3(x, y, 0f)));
            }

            return cells;
        }

        static void Validate(int columns, int rows)
        {
            if (columns <= 0)
                throw new ArgumentOutOfRangeException(nameof(columns));
            if (rows <= 0)
                throw new ArgumentOutOfRangeException(nameof(rows));
        }
    }
}

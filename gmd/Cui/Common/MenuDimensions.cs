namespace gmd.Cui.Common;

// Where a menu is drawn and how wide its parts are. Index math with no view, so it is
// testable without a Terminal.Gui driver, i.e. the screen size is passed in.
record MenuDimensions(int X, int Y, int Width, int Height, int TitleWidth, int ShortcutWidth, int SubMenuMarkerWidth)
{
    const int maxHeight = 30;

    // Calculates menu view dimensions based on screen size and number of items
    public static MenuDimensions Calculate(
        IReadOnlyList<MenuItem> items,
        string title,
        int xOrg,
        int yOrg,
        int altX,
        int screenWidth,
        int screenHeight
    )
    {
        // Calculate view height based on number of items, screen height and max height if very large screen
        var viewHeight = Math.Min(items.Count + 2, Math.Min(maxHeight, screenHeight));

        // Calculate items width based on longest item tex and shortcut, and if sub menu marker is needed and scrollbar is needed
        var shortcutWidth = items.Any() ? items.Max(i => i.Shortcut.Length + 1) : 0; // Include space before
        var subMenuMarkerWidth = items.Any(i => i is SubMenu) ? 2 : 0; // Include space before
        var scrollbarWidth = items.Count + 2 > viewHeight ? 1 : 0;

        var detailColumn = DetailColumn(items);
        var itemTextWidth = items.Any()
            ? items.Max(i => i.Detail == "" ? i.Text.Length : detailColumn + i.Detail.Length)
            : 5;

        // Calculate view width based on title, shortcut, sub menu marker and scrollbar
        var totalItemsWidth = itemTextWidth + shortcutWidth + subMenuMarkerWidth + scrollbarWidth + 2; // (2 for borders)

        var viewWidth = Math.Max(totalItemsWidth, title.Length + 5); // (4 for extra space around title)
        if (viewWidth > totalItemsWidth)
        { // Ensure shortcut and menus are to the right
            itemTextWidth += viewWidth - totalItemsWidth;
        }

        if (viewWidth > screenWidth)
        { // Too wide view, try to fit on screen (reduce title width)
            viewWidth = screenWidth;
            itemTextWidth = Math.Max(10, viewWidth - shortcutWidth - subMenuMarkerWidth - scrollbarWidth - 1);
        }

        // Calculate view x and y position to be centered if Menu.Center or based on original x and y
        var viewX = xOrg == Menu.Center ? screenWidth / 2 - viewWidth / 2 : xOrg; // Centered if x == Center
        var viewY = yOrg == Menu.Center ? screenHeight / 2 - viewHeight / 2 : yOrg; // Centered if y == Center

        if (viewX + viewWidth > screenWidth)
        { // Too far to the right, try to move menu left
            if (altX >= 0)
            { // Use alternative x position (left of parent menu)
                viewX = altX - viewWidth;
            }
            else
            { // Adjust original x position
                viewX -= viewWidth;
            }
        }
        viewX = Math.Max(0, viewX);

        if (viewY + viewHeight > screenHeight)
        { // Too far down, try to move up
            viewY = screenHeight - viewHeight;
        }
        viewY = Math.Max(0, viewY);

        return new MenuDimensions(
            viewX,
            viewY,
            viewWidth,
            viewHeight,
            itemTextWidth,
            shortcutWidth,
            subMenuMarkerWidth
        );
    }

    // The item on a row of the menu, counted from its top border and scrolled to firstIndex, or -1
    // for a border or a row below the last item. A click on the frame picks nothing: it used to run
    // the item next to it, or the one scrolled out of view beyond it, and in an empty menu it threw.
    public int ItemAt(int y, int firstIndex, int itemCount)
    {
        var row = y - 1;
        if (row < 0 || row >= Height - 2)
            return -1;

        var index = firstIndex + row;
        return index < itemCount ? index : -1;
    }

    // Where the details start: after the longest text of the items that have one, and a gap
    public static int DetailColumn(IReadOnlyList<MenuItem> items) =>
        items.Where(i => i.Detail != "").Select(i => i.Text.Length + 2).DefaultIfEmpty(0).Max();
}

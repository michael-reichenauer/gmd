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

        var itemTextWidth = items.Any() ? items.Max(i => i.Text.Length) : 5;

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
}

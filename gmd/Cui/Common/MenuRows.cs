namespace gmd.Cui.Common;

// Draws the menu items as the rows the menu view shows, i.e. the item text possibly truncated,
// the shortcut, the sub menu marker and the separator lines. No view, so it is testable
// without a Terminal.Gui driver.
static class MenuRows
{
    // Less room than this for a detail, and it is left out, see Detail
    const int minDetailWidth = 4;

    public static IReadOnlyList<Text> ToRows(IReadOnlyList<MenuItem> items, MenuDimensions dimensions)
    {
        var detailColumn = MenuDimensions.DetailColumn(items);
        var detailWidth = dimensions.TitleWidth - detailColumn;

        return items
            .Select(item =>
            {
                if (item is MenuSeparator ms)
                    return Text.BrightMagenta(ToSeparatorText(ms, items.Count, dimensions));

                // Color if disabled or not
                var titleColor = item.IsDisabled ? Color.Dark : item.TextColor ?? Color.White;

                // Title text might need to be truncated, and a detail is cut first, at its start,
                // since the end of a path is what tells it apart
                var text = new TextBuilder();
                if (item.Detail != "" && detailWidth >= minDetailWidth)
                {
                    var detail =
                        item.Detail.Length > detailWidth ? "…" + item.Detail[^(detailWidth - 1)..] : item.Detail;
                    text.Color(titleColor, item.Text.Max(detailColumn, true)).Dark(detail.Max(detailWidth, true));
                }
                else if (item.Text.Length > dimensions.TitleWidth)
                {
                    text.Color(titleColor, item.Text.Max(dimensions.TitleWidth - 1, true)).Dark("…");
                }
                else
                {
                    text.Color(titleColor, item.Text.Max(dimensions.TitleWidth, true));
                }

                // Shortcut
                if (!item.IsDisabled && item.Shortcut != "")
                    text.Black(new string(' ', dimensions.ShortcutWidth - item.Shortcut.Length)).Cyan(item.Shortcut);
                else if (item.Shortcut != "")
                    text.Black(new string(' ', dimensions.ShortcutWidth - item.Shortcut.Length)).Dark(item.Shortcut);
                else if (dimensions.ShortcutWidth > 0)
                    text.Black(new string(' ', dimensions.ShortcutWidth));

                // Submenu marker >
                if (!item.IsDisabled && item is SubMenu)
                    text.BrightMagenta(" >");
                if (item.IsDisabled && item is SubMenu)
                    text.Dark(" >");
                if (dimensions.SubMenuMarkerWidth > 0)
                    text.Black("  ");

                return text.ToText();
            })
            .ToList();
    }

    static string ToSeparatorText(MenuSeparator item, int itemsCount, MenuDimensions dimensions)
    {
        string text = item.Text;
        var width = dimensions.Width - 2;
        var scrollbarWidth = itemsCount + 2 > dimensions.Height ? 0 : 1;
        if (text == "")
        { // Just a line ----
            text = new string('─', dimensions.Width - 2 + scrollbarWidth);
        }
        else
        { // A line with text, e.g. '-- text ------
            text = text.Max(width - 5);
            string suffix = new string('─', Math.Max(0, width - text.Length - 5 + scrollbarWidth));
            text = $"╴{text} {suffix}──";
        }

        return text;
    }
}

using System.Collections;
using Terminal.Gui;

namespace gmd.Cui.Common;

class Menu
{
    ContextMenu menu = new ContextMenu();

    public Menu(int x, int y, IEnumerable<MenuItem> menuItems)
    {
        menu = new ContextMenu(x, y, new MenuBarItem(
            menuItems.Select(i => i.AsMenuItem()).ToArray()));
    }


    public void Show()
    {
        menu.Show();
    }

    public static MenuItems MenuItems() => new MenuItems();

    public static SubMenu SubMenu(string title, string shortcut, IEnumerable<MenuItem> children, Func<bool>? canExecute = null) =>
        new SubMenu(title, shortcut, children, null, canExecute);

    public static MenuItem Item(string title, string shortcut, Action action, Func<bool>? canExecute = null) =>
        new MenuItem(title, shortcut, action, canExecute);

    public static MenuItem Separator(string text = "")
    {
        const int maxDivider = 25;
        if (text == "")
        {
            return new MenuItem(new string('─', maxDivider), "", () => { }, () => false);
        }

        text = text.Max(maxDivider - 6);
        string suffix = new string('─', Math.Max(0, maxDivider - text.Length - 6));
        return new MenuItem($"── {text} {suffix}──", "", () => { }, () => false);
    }
}


class MenuItems : IEnumerable<MenuItem>
{
    List<MenuItem> items = new List<MenuItem>();

    public IEnumerator<MenuItem> GetEnumerator() => items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)items).GetEnumerator();

    public MenuItems Menu(string title, string shortcut, IEnumerable<MenuItem> children, Action? action = null, Func<bool>? canExecute = null)
    {
        items.Add(new SubMenu(title, shortcut, children, action, canExecute));
        return this;
    }

    public MenuItems Item(string title, string shortcut, Action action, Func<bool>? canExecute = null)
    {
        items.Add(new MenuItem(title, shortcut, action, canExecute));
        return this;
    }

    public MenuItems Separator(string text = "")
    {
        items.Add(Common.Menu.Separator(text));
        return this;
    }

    public MenuItems Add(params MenuItem[] items)
    {
        items.Add(items);
        return this;
    }
}


class SubMenu : MenuItem
{
    MenuBarItem menuBar;

    public SubMenu(string title, string shortcut, IEnumerable<MenuItem> children, Action? action = null, Func<bool>? canExecute = null)
    {
        shortcut = shortcut == "" ? "" : shortcut + " ";
        menuBar = new MenuBarItem(title, shortcut, action, canExecute)
        {
            Children = children.Select(c => c.AsMenuItem()).ToArray()
        };
    }

    public override Terminal.Gui.MenuItem AsMenuItem() => menuBar;
}


class MenuItem
{
    Terminal.Gui.MenuItem item;

    public MenuItem()
    {
        item = new Terminal.Gui.MenuItem();
    }

    public MenuItem(string title, string shortcut, Action action, Func<bool>? canExecute = null)
    {
        shortcut = shortcut == "" ? "" : shortcut + " ";
        item = new Terminal.Gui.MenuItem(title, shortcut, action, canExecute);
    }

    public string Title => item.Title.ToString() ?? "";

    public virtual Terminal.Gui.MenuItem AsMenuItem() => item;
}



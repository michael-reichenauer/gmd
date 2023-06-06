using NStack;
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
}

class SubMenu : MenuItem
{
    MenuBarItem menuBar;

    public SubMenu(IEnumerable<MenuItem> children)
    {
        menuBar = new MenuBarItem(children.Select(c => c.AsMenuItem()).ToArray());
    }

    public SubMenu(ustring title, ustring shortcut, IEnumerable<MenuItem> children, Action? action = null, Func<bool>? canExecute = null)
    {
        shortcut = shortcut == "" ? "" : shortcut + " ";
        menuBar = new MenuBarItem(title, shortcut, action, canExecute)
        {
            Children = children.Select(c => c.AsMenuItem()).ToArray()
        };
    }

    public MenuBarItem AsMenuBarItem() => menuBar;
    public override Terminal.Gui.MenuItem AsMenuItem() => menuBar;
}


class MenuItem
{
    Terminal.Gui.MenuItem item;

    public MenuItem()
    {
        item = new Terminal.Gui.MenuItem();
    }
    public MenuItem(ustring title, ustring shortcut, Action action, Func<bool>? canExecute = null)
    {
        shortcut = shortcut == "" ? "" : shortcut + " ";
        item = new Terminal.Gui.MenuItem(title, shortcut, action, canExecute);
    }

    public string Title => item.Title.ToString() ?? "";

    public virtual Terminal.Gui.MenuItem AsMenuItem() => item;
}



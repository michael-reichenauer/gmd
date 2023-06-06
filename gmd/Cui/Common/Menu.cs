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

    public SubMenu(ustring title, ustring help, IEnumerable<MenuItem> children, Action? action = null, Func<bool>? canExecute = null)
    {
        menuBar = new MenuBarItem(title, help, action, canExecute)
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

    public MenuItem(Key shortcut = Key.Null)
    {
        item = new Terminal.Gui.MenuItem(shortcut);
    }
    public MenuItem(ustring title, ustring help, Action action, Func<bool>? canExecute = null)
    {
        item = new Terminal.Gui.MenuItem(title, help, action, canExecute);
    }

    public string Title => item.Title.ToString() ?? "";

    public virtual Terminal.Gui.MenuItem AsMenuItem() => item;
}



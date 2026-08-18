namespace gmd.Cui.Common;

// A normal menu item and base class for SubMenu and MenuSeparator
record MenuItem(string Text, string Shortcut, Action Action, Func<bool>? CanExecute = null)
{
    public bool IsDisabled { get; init; }
}

// To create a sub menu
record SubMenu : MenuItem
{
    public SubMenu(string text, string shortcut, IEnumerable<MenuItem> children, Func<bool>? canExecute = null)
        : base(text, shortcut, () => { }, canExecute)
    {
        Children = children;
    }

    public IEnumerable<MenuItem> Children { get; init; }
}

// To create a menu separator line or header line
record MenuSeparator : MenuItem
{
    public MenuSeparator(string text = "")
        : base(text, "", () => { }, () => false) { }
}

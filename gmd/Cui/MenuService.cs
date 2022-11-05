using NStack;
using Terminal.Gui;

namespace gmd.Cui;

interface IMenuService
{
    void ShowShowBranchesMenu(Point point);
}

class MenuService : IMenuService
{
    public void ShowShowBranchesMenu(Point point)
    {
        var contextMenu = new ContextMenu(point.X, point.Y,
                 new MenuBarItem(new MenuItem[] {
                    new MenuItem ("_Configuration", "Show configuration", () => MessageBox.Query (50, 5, "Info", "This would open settings dialog", "Ok")),
                    new MenuBarItem ("More options", new MenuItem [] {
                        new MenuItem ("_Setup", "Change settings", () => MessageBox.Query (50, 5, "Info", "This would open setup dialog", "Ok")),
                        new MenuItem ("_Maintenance", "Maintenance mode", () => MessageBox.Query (50, 5, "Info", "This would open maintenance dialog", "Ok")),
                    }),


                    new MenuItem ("_Quit", "", () => Application.RequestStop ())
                 })
             )
        { ForceMinimumPosToZero = true, UseSubMenusSingleFrame = true };

        contextMenu.Show();
    }
}


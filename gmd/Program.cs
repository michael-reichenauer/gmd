using Terminal.Gui;
using NStack;
using gmd.Cui;
using gmd.Utils;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("gmdTest")]

namespace gmd;

class Program
{
    static void Main(string[] args)
    {
        Application.Init();
        Application.Top.AddKeyBinding(Key.Esc, Command.QuitToplevel);
        Application.Top.WantMousePositionReports = false;

        var mainView = new MainView();
        Application.Top.Add(mainView.View);

        mainView.SetDataAsync().RunInBackground();

        // Run blocks until the user quits the application
        Application.Run();
        Application.Shutdown();
    }
}


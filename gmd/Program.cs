using Terminal.Gui;
using gmd.Cui;
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

        // Run blocks until the user quits the application
        Application.Run(HandleUIMainLoopError);
        Application.Shutdown();
    }

    private static bool HandleUIMainLoopError(Exception e)
    {
        Log.Exception(e, "Error in UI main loop");
        return false; // End loop after error
    }
}


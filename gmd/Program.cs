using Terminal.Gui;
using NStack;

namespace gmd;

class Program
{
    static void Main(string[] args)
    {
        Application.Init();

        var mainView = new MainView();
        Application.Top.Add(mainView);
        Application.Top.AddKeyBinding(Key.Esc, Command.QuitToplevel);

        mainView.SetData().RunInBackground();
        // Run blocks until the user quits the application
        Application.Run();
        Application.Shutdown();
    }
}


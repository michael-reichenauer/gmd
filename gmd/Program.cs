using Terminal.Gui;
using gmd.Cui;
using System.Runtime.CompilerServices;
using gmd.Utils;

[assembly: InternalsVisibleTo("gmdTest")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace gmd;

class Program
{
    private static DependencyInjection? dependencyInjection;
    private readonly IMainView mainView;

    static void Main(string[] args)
    {
        dependencyInjection = new DependencyInjection();
        dependencyInjection.RegisterDependencyInjectionTypes();

        Program program = dependencyInjection.Resolve<Program>();
        program.Run();
    }

    internal Program(IMainView mainView)
    {
        this.mainView = mainView;
    }

    private void Run()
    {
        Application.Init();
        Application.Top.AddKeyBinding(Key.Esc, Command.QuitToplevel);
        Application.Top.WantMousePositionReports = false;

        Application.Top.Add(mainView.View);

        // Run blocks until the user quits the application
        Application.Run(HandleUIMainLoopError);
        Application.Shutdown();
    }


    private bool HandleUIMainLoopError(Exception e)
    {
        Log.Exception(e, "Error in UI main loop");
        return false; // End loop after error
    }
}


﻿using Terminal.Gui;
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
        var t = Timing.Start();
        Log.Info($"Starting gmd ...");
        ExceptionHandling.HandleUnhandledExceptions(UI.Shutdown);

        dependencyInjection = new DependencyInjection();
        dependencyInjection.RegisterDependencyInjectionTypes();

        Program program = dependencyInjection.Resolve<Program>();
        Log.Info($"Initialized {t}");
        program.Main();
        Log.Info($"Done, running for {t}");
        Log.CloseAsync().Wait();
    }

    internal Program(IMainView mainView)
    {
        this.mainView = mainView;
    }

    private void Main()
    {
        var t = Timing.Start();
        Application.Init();
        Application.Top.AddKeyBinding(Key.Esc, Command.QuitToplevel);
        Application.Top.WantMousePositionReports = false;
        Application.Driver.SetCursorVisibility(CursorVisibility.Invisible);

        Application.Top.Add(mainView.View);
        Log.Info($"Initialized UI {t}");

        // Run blocks until the user quits the application
        Application.Run(HandleUIMainLoopError);
        Application.Shutdown();
    }


    private bool HandleUIMainLoopError(Exception e)
    {
        Log.Exception(e, "Error in UI main loop");
        Log.CloseAsync().Wait();
        return false; // End loop after error
    }
}


using Terminal.Gui;
using gmd.Cui;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;

[assembly: InternalsVisibleTo("gmdTest")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]


namespace gmd;

class Program
{
    private static DependencyInjection? dependencyInjection;
    private readonly IMainView mainView;

    // static readonly string configPath = "/workspaces/gmd/config.json";

    static void Main(string[] args)
    {
        var t = Timing.Start;
        Log.Info($"Starting gmd ...");

        // IConfigurationRoot config = new ConfigurationBuilder()
        //     .AddJsonFile(configPath)
        //     .AddEnvironmentVariables()
        //     .AddCommandLine(args)
        //     .Build();
        //Log.Info($"{config["greetingds"]}");

        ExceptionHandling.HandleUnhandledExceptions(UI.Shutdown);

        dependencyInjection = new DependencyInjection();
        dependencyInjection.RegisterDependencyInjectionTypes();

        Program program = dependencyInjection.Resolve<Program>();
        Log.Info($"Initialized {t}");
        program.Main();
        Log.Info($"Done, running for {t}");
        ConfigLogger.CloseAsync().Wait();
    }

    internal Program(IMainView mainView)
    {
        this.mainView = mainView;
    }

    private void Main()
    {
        var t = Timing.Start;

        Log.Info($"Build {Util.GetBuildVersion()} {Util.GetBuildTime().ToString("yyyy-MM-dd HH:mm")}");

        Application.Init();
        Application.Top.AddKeyBinding(Key.Esc, Command.QuitToplevel);
        UI.HideCursor();

        Application.Top.Add(mainView.View);
        Log.Info($"Initialized UI {t}");


        // Run blocks until the user quits the application
        Application.Run(HandleUIMainLoopError);
        Application.Shutdown();
    }


    private bool HandleUIMainLoopError(Exception e)
    {
        Log.Exception(e, "Error in UI main loop");
        ConfigLogger.CloseAsync().Wait();
        return false; // End loop after error
    }
}


using Terminal.Gui;
using gmd.Cui;
using System.Runtime.CompilerServices;
using gmd.Cui.Common;
using gmd.Git;
using gmd.Installation;
// using Microsoft.Extensions.Configuration;

[assembly: InternalsVisibleTo("gmdTest")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]


namespace gmd;

class Program
{
    private static DependencyInjection? dependencyInjection;
    private readonly IMainView mainView;
    private readonly IGit git;
    private readonly IUpdater updater;

    // static readonly string configPath = "/workspaces/gmd/config.json";

    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--version")
        {
            Console.WriteLine($"{Utils.Util.BuildVersion()}");
            return;
        }

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
        Threading.AssertIsMainThread();
        program.Main();
        Log.Info($"Done, running for {t}");
        ConfigLogger.CloseAsync().Wait();
    }

    internal Program(IMainView mainView, IGit git, IUpdater updater)
    {
        this.mainView = mainView;
        this.git = git;
        this.updater = updater;
    }

    void Main()
    {
        var t = Timing.Start;
        StartAsync().RunInBackground();

        Application.Init();
        Application.Top.AddKeyBinding(Key.Esc, Command.QuitToplevel);
        UI.HideCursor();

        Application.Top.Add(mainView.View);

        // Run blocks until the user quits the application
        Application.Run(HandleUIMainLoopError);
        Application.Shutdown();
    }


    bool HandleUIMainLoopError(Exception e)
    {
        Log.Exception(e, "Error in UI main loop");
        ConfigLogger.CloseAsync().Wait();
        return false; // End loop after error
    }

    async Task StartAsync()
    {
        await LogInfoAsync();
        await updater.CheckUpdateAvailableAsync();
    }

    async Task LogInfoAsync()
    {
        Log.Info($"Version: {Util.BuildVersion()}");
        Log.Info($"Build    {Util.BuildTime().ToUniversalTime().Iso()}");
        if (!Try(out var gitVersion, out var e, await git.Version()))
        {
            Log.Error($"No git command detected, {e}");
        }
        Log.Info($"Git:     {gitVersion}");
        Log.Info($"Cmd:     {Environment.CommandLine}");
        Log.Info($"Dir:     {Environment.CurrentDirectory}");
        Log.Info($".NET:    {Environment.Version}");
        Log.Info($"OS:      {Environment.OSVersion}");
    }
}


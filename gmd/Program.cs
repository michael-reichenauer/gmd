using Terminal.Gui;
using gmd.Cui;
using gmd.Cui.Common;
using gmd.Git;
using gmd.Installation;
using gmd.Common;
using gmd.Server;

namespace gmd;

class Program
{
    // Current major.minor version
    public const int MajorVersion = 0;
    public const int MinorVersion = 80;

    private static DependencyInjection? dependencyInjection;
    private readonly IMainView mainView;
    private readonly IGit git;
    private readonly IUpdater updater;
    private readonly IServer server;
    private readonly IState state;

    static async Task<int> Main(string[] args)
    {
        var t = Timing.Start();
        ExceptionHandling.HandleUnhandledExceptions(UI.Shutdown);

        new Upgrader().UpgradeData();

        dependencyInjection = new DependencyInjection();
        dependencyInjection.RegisterDependencyInjectionTypes();

        if (args.Contains("-h") || args.Contains("--help") || args.Contains("-?"))
        {
            return ShowHelp();
        }
        if (args.Contains("--version"))
        {
            return ShowVersion();
        }
        if (args.Contains("--update"))
        {
            return await UpdateAsync(dependencyInjection.Resolve<IUpdater>());
        }
        if (args.Contains("--changelog"))
        {
            return ShowShangeLog(dependencyInjection.Resolve<IServer>());
        }
        if (args.Contains("--updatechangelog"))
        {
            return UpdateShangeLog(dependencyInjection.Resolve<IServer>());
        }

        Log.Info($"Starting gmd ...");
        Program program = dependencyInjection.Resolve<Program>();
        program.Main();

        Log.Info($"Done, running for {t}");
        ConfigLogger.CloseAsync().Wait();
        return 0;
    }

    internal Program(IMainView mainView, IGit git, IUpdater updater, IServer server, IState state)
    {
        this.mainView = mainView;
        this.git = git;
        this.updater = updater;
        this.server = server;
        this.state = state;
    }

    void Main()
    {
        var t = Timing.Start();
        StartAsync().RunInBackground();

        Application.Init();
        Application.Top.AddKeyBinding(Key.Esc, Command.QuitToplevel);
        UI.HideCursor();                       // Hide cursor to avoid flickering
        Application.Driver.Checked = '◙';      // Checked box characters '▣' '▢'
        Application.Driver.UnChecked = '□';

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
        updater.CheckUpdatesRegularly().RunInBackground();
    }

    async Task LogInfoAsync()
    {
        Log.Info($"Version: {Build.Version()}");
        Log.Info($"Build    {Build.Time().ToUniversalTime().Iso()}Z");
        Log.Info($"Sha:     {Build.Sha()}");
        if (!Try(out var gitVersion, out var e, await git.Version()))
        {
            Log.Error($"No git command detected, {e}");
        }
        Log.Info($"Git:     {gitVersion}");
        Log.Info($"Cmd:     {Environment.CommandLine}");
        Log.Info($"Process: {Environment.ProcessPath}");
        Log.Info($"Dir:     {Environment.CurrentDirectory}");
        Log.Info($".NET:    {Environment.Version}");
        Log.Info($"OS:      {Environment.OSVersion}");
        Log.Info($"Time:    {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss")}+00:00");

        state.Set(s => s.GitVersion = gitVersion ?? "");
    }

    static int ShowVersion()
    {
        Console.WriteLine($"{Build.Version()}");
        return 0;
    }

    static async Task<int> UpdateAsync(IUpdater updater)
    {
        var currentVersion = Build.Version();
        Console.WriteLine($"Trying to update current version {currentVersion} ...");

        if (!Try(out var available, out var e, await updater.IsUpdateAvailableAsync()))
        {
            Console.WriteLine($"Failed to check for updates: {e}");
            return -1;
        }
        if (!available.Item1)
        {
            Console.WriteLine($"{available.Item2} is already latest version.");
            return 0;
        }

        Console.WriteLine($"Downloading {available.Item2} ...");
        if (!Try(out var newVersion, out e, await updater.UpdateAsync()))
        {
            Console.WriteLine($"Failed to update: {e}");
            return -1;
        }

        Console.WriteLine($"Updated {currentVersion} -> {newVersion}");
        return 0;
    }

    static int ShowHelp()
    {
        var msg = $"""
        gmd ({Build.Version()})
        Usage gmd [options] [arguments]

        options:
          --version       Show current version
          --update        Update gmd to latest version (downloading from GitHub)
          -d <path>       Show repo for working folder specified by <path>
          --help|-h|-?    Show command line help.
          
        """;
        Console.WriteLine(msg);
        return 0;
    }


    static int ShowShangeLog(IServer server)
    {
        Task.Run(async () =>
        {
            Console.WriteLine($"# Change Log for Gmd\n--------------------");
            if (!Try(out var log, out var e, await server.GetChangeLogAsync()))
            {
                Log.Error($"Failed to get change log, {e}");
                Console.WriteLine($"Failed to get change log, {e}");
            }

            Console.WriteLine($"{log}");
        })
        .Wait();

        return 0;
    }

    static int UpdateShangeLog(IServer server)
    {
        Task.Run(async () =>
        {
            Console.WriteLine($"Generating change log ...");
            if (!Try(out var log, out var e, await server.GetChangeLogAsync()))
            {
                Log.Error($"Failed to get change log, {e}");
            }

            File.WriteAllText("CHANGELOG.md", $"# Change Log for Gmd\n--------------------\n{log}");
            Console.WriteLine($"Generated change log");
        })
        .Wait();
        return 0;
    }
}


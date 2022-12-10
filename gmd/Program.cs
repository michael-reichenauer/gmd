using Terminal.Gui;
using gmd.Cui;
using gmd.Cui.Common;
using gmd.Git;
using gmd.Installation;


namespace gmd;

class Program
{
    // Current major.minor version
    public const int MajorVersion = 0;
    public const int MinorVersion = 30;

    private static DependencyInjection? dependencyInjection;
    private readonly IMainView mainView;
    private readonly IGit git;
    private readonly IUpdater updater;


    static async Task<int> Main(string[] args)
    {
        if (args.Contains("--version"))
        {
            return ShowVersion();
        }
        if (args.Contains("--update"))
        {
            return await UpdateAsync();
        }

        var t = Timing.Start();
        Log.Info($"Starting gmd ...");

        ExceptionHandling.HandleUnhandledExceptions(UI.Shutdown);

        dependencyInjection = new DependencyInjection();
        dependencyInjection.RegisterDependencyInjectionTypes();

        Program program = dependencyInjection.Resolve<Program>();
        program.Main();

        Log.Info($"Done, running for {t}");
        ConfigLogger.CloseAsync().Wait();
        return 0;
    }


    internal Program(IMainView mainView, IGit git, IUpdater updater)
    {
        this.mainView = mainView;
        this.git = git;
        this.updater = updater;
    }

    void Main()
    {
        var t = Timing.Start();
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
    }

    static int ShowVersion()
    {
        Console.WriteLine($"{Build.Version()}");
        return 0;
    }

    static async Task<int> UpdateAsync()
    {
        var currentVersion = Build.Version();
        Console.WriteLine($"Trying to update current version {currentVersion} ...");
        var updater = new Updater();

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
}


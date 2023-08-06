using Terminal.Gui;
using gmd.Cui;
using gmd.Cui.Common;
using gmd.Git;
using gmd.Common;
using gmd.Server;

namespace gmd;

class Program
{
    // Current major.minor version
    public const int MajorVersion = 0;
    public const int MinorVersion = 90;

    static readonly DependencyInjection dependencyInjection = new DependencyInjection();
    readonly IMainView mainView;
    readonly IGit git;
    readonly IState state;


    static async Task<int> Main(string[] args)
    {
        var t = Timing.Start();
        ExceptionHandling.HandleUnhandledExceptions(UI.Shutdown);

        // Upgrade data if needed
        Upgrader.UpgradeData();

        // Setup dependency injection by registering all types in this assembly
        dependencyInjection.RegisterAllAssemblyTypes();

        // Handle commands like show version, upgrade, ...
        var programCommands = dependencyInjection.Resolve<IProgramCommands>();
        var commandResult = await programCommands.HandleCommands(args);
        if (commandResult.IsCommand)
        {
            return commandResult.ExitCode;
        }

        Log.Info($"Starting gmd ...");
        Program program = dependencyInjection.Resolve<Program>();
        program.Run();

        Log.Info($"Done, running for {t}");
        ConfigLogger.CloseAsync().Wait();
        return 0;
    }

    internal Program(IMainView mainView, IGit git, IState state)
    {
        this.mainView = mainView;
        this.git = git;
        this.state = state;
    }

    void Run()
    {
        LogProgramInfoAsync().RunInBackground();

        Application.Init();
        Application.Top.AddKeyBinding(Key.Esc, Command.QuitToplevel);
        UI.HideCursor();                       // Hide cursor to avoid flickering
        Application.Driver.Checked = '◙'; // '■'; // ▣';      // Checked box characters 
        Application.Driver.UnChecked = '□'; // '□'; //▢';
        Application.Driver.Stipple = ' ';  // The scrollbar background character

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


    async Task LogProgramInfoAsync()
    {
        Log.Info($"Version: {Build.Version()}");
        Log.Info($"Build    {Build.Time().IsoZone()}");
        Log.Info($"Sha:     {Build.Sha()}");
        Log.Info($"Cmd:     {Environment.CommandLine}");
        Log.Info($"Process: {Environment.ProcessPath}");
        Log.Info($"Dir:     {Environment.CurrentDirectory}");
        Log.Info($".NET:    {Environment.Version}");
        Log.Info($"OS:      {Environment.OSVersion}");
        Log.Info($"Time:    {DateTime.Now.IsoZone()}");

        if (!Try(out var gitVersion, out var e, await git.Version()))
        {
            Log.Error($"No git command detected, {e}");
        }
        Log.Info($"Git:     {gitVersion}");

        state.Set(s => s.GitVersion = gitVersion ?? "");
    }
}


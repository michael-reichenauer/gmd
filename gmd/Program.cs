using gmd.Common;
using gmd.Cui;
using gmd.Cui.Common;
using gmd.Git;
using Terminal.Gui;

namespace gmd;

class Program
{
    // Current major.minor version. CI raises MinorVersion by one for every release from main, in the
    // release commit it makes (see .github/workflows/build-and-release.yml), unless the version here
    // is already above the latest release, i.e. raised by hand, as to 1.0. It finds both with sed,
    // so keep them written as 'public const int MajorVersion = <n>;' and 'MinorVersion = <n>;'.
    public const int MajorVersion = 1;
    public const int MinorVersion = 3;

    static readonly DependencyInjection dependencyInjection = new DependencyInjection();
    readonly IMainView mainView;
    readonly IGit git;
    readonly Config config;

    static async Task<int> Main(string[] args)
    {
        // Started by ssh or git to ask the user something, see Askpass. First of all, since what
        // follows writes the log and the config, which belong to the gmd that started the git
        if (Askpass.IsAsked)
            return Askpass.Answer(args);

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

        if (ExceptionHandling.Failure is string failure)
        {
            Console.Error.WriteLine(CrashMessage(failure, ConfigLogger.FilePath));
            return 1;
        }
        return 0;
    }

    // What a crash says, once the terminal is given back, where gmd used to end without a word, as
    // if it had simply quit. The log is begun anew on every start, so it is to be copied first.
    internal static string CrashMessage(string failure, string logPath) =>
        $"""
            gmd stopped on an unexpected error, sorry: {failure}

            What happened is in the log, which the next start of gmd begins anew, so copy it first:
              {logPath}
            Please report the problem, with the log and the version (gmd --version), at:
              {Project.ProblemUrl}
            """;

    internal Program(IMainView mainView, IGit git, Config config)
    {
        this.mainView = mainView;
        this.git = git;
        this.config = config;
    }

    void Run()
    {
        LogProgramInfoAsync().RunInBackground();

        Application.Init();
        // Only reached when no view above handles Esc, e.g. before a repo is shown. The log view
        // takes Esc itself and asks before quitting (RepoViewInput.QuitAfterAsking).
        Application.Top.AddKeyBinding(Key.Esc, Command.QuitToplevel);
        UI.HideCursor(); // Hide cursor to avoid flickering
        Application.Driver.Checked = '◙'; // '■'; // ▣';      // Checked box characters
        Application.Driver.UnChecked = '□'; // '□'; //▢';
        Application.Driver.Stipple = ' '; // The scrollbar background character

        Application.Top.Add(mainView.View);

        // Run blocks until the user quits the application
        Application.Run(HandleUIMainLoopError);
        Application.Shutdown();
    }

    bool HandleUIMainLoopError(Exception e)
    {
        ExceptionHandling.OnMainLoopException(e);
        ConfigLogger.CloseAsync().Wait();
        return false; // End loop after error
    }

    async Task LogProgramInfoAsync()
    {
        Log.Info($"Version: {Build.Version()}");
        Log.Info($"Build    {Build.Time().ToLocalTime().IsoZone()}");
        Log.Info($"Sha:     {Build.Sha()}");
        Log.Info($"Cmd:     {Environment.CommandLine}");
        Log.Info($"Process: {Environment.ProcessPath}");
        Log.Info($"Dir:     {Environment.CurrentDirectory}");
        Log.Info($".NET:    {Environment.Version}");
        Log.Info($"OS:      {Environment.OSVersion}");
        Log.Info($"Time:    {DateTime.Now.IsoZone()}");

        var gitVersionResult = await git.Version();
        if (gitVersionResult is Error e)
            Log.Error($"No git command detected, {e}");
        var gitVersion = gitVersionResult is string version ? version : "";
        Log.Info($"Git:     {gitVersion}");

        config.Set(s => s.GitVersion = gitVersion);
    }
}

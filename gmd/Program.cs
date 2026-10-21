using gmd.Common;
using gmd.Cui;
using gmd.Cui.Common;
using gmd.Git;
using gmd.Installation;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;

namespace gmd;

class Program
{
    // The major.minor of the next release, which dev's previews carry too. The release workflow
    // raises MinorVersion on dev right after each release (see .github/workflows/release.yml), and
    // refuses a release not above the latest one; a new major is raised by hand, as to 2.0. It
    // finds both with sed, so keep them written as 'public const int MajorVersion = <n>;' and
    // 'MinorVersion = <n>;'.
    public const int MajorVersion = 1;
    public const int MinorVersion = 15;

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
        ExceptionHandling.HandleUnhandledExceptions(UI.ShutdownFromAnyThread);

        // Setup dependency injection by registering all types in this assembly
        dependencyInjection.RegisterAllAssemblyTypes();

        // Handle commands like show version, upgrade, ...
        var programCommands = dependencyInjection.Resolve<IProgramCommands>();
        if (await programCommands.HandleCommandsAsync(args) is int exitCode)
            return exitCode;

        Log.Info($"Starting gmd ...");
        Program program = dependencyInjection.Resolve<Program>();
        program.Run();

        Log.Info($"Done, running for {t}");
        LogFile.CloseAsync().Wait();

        if (ExceptionHandling.Failure is string failure)
        {
            Console.Error.WriteLine(CrashMessage(failure, LogFile.FilePath));
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
        LogProgramInfo();

        // No program is started before Terminal.Gui has started, git's version included. With 1.x it
        // was curses that set the locale as it started (setlocale), and on macOS a fork in another
        // thread meanwhile hung both, the fork waiting for setlocale's lock, and setlocale for malloc,
        // which the fork had locked: gmd hung so at two starts in three (macOS 27, 2026-10-14). 2.x
        // has no curses, but it asks the terminal things as it starts, which a git started meanwhile
        // has no part in either.
        using IApplication app = Application.Create().Init();
        UI.SetApp(app);
        UI.UsePalette(config.Colors);
        if (app.Driver is IDriver driver)
        {
            // The terminal says what its background is a moment after the start, which picks the
            // light palette or the dark one, as the setting is then
            driver.DefaultAttributeChanged += (_, _) => UI.Post(() => UI.UsePalette(config.Colors));
        }
        LogGitVersionAsync().RunInBackground();

        // Esc, Terminal.Gui's quit key, ends the view on top, which is gmd itself when no view above
        // takes it, e.g. before a repo is shown. The log view takes Esc itself and asks before
        // quitting (RepoViewInput.QuitAfterAsking).
        var view = mainView.View;

        // Run blocks until the user quits the application
        app.Run(view, HandleUIMainLoopError);
        view.Dispose();
    }

    bool HandleUIMainLoopError(Exception e)
    {
        ExceptionHandling.OnMainLoopException(e);
        return false; // End loop after error
    }

    // The package manager that updates gmd, which a user report may need to know
    static string InstalledBy(PackageManager? manager) =>
        manager == null ? "" : $", installed with {manager.Name}, which updates it";

    static void LogProgramInfo()
    {
        Log.Info($"Version: {Build.Version()}");
        Log.Info($"Build    {Build.Time().ToLocalTime().IsoZone()}");
        Log.Info($"Sha:     {Build.Sha()}");
        Log.Info($"Cmd:     {Environment.CommandLine}");
        Log.Info($"Process: {Environment.ProcessPath}");
        Log.Info($"Binary:  {RunningGmd.Current.Path}{InstalledBy(RunningGmd.Current.PackageManager)}");
        Log.Info($"Dir:     {Environment.CurrentDirectory}");
        Log.Info($".NET:    {Environment.Version}");
        Log.Info($"OS:      {Environment.OSVersion}");
        Log.Info($"Time:    {DateTime.Now.IsoZone()}");
    }

    async Task LogGitVersionAsync()
    {
        var gitVersionResult = await git.GetVersionAsync();
        if (gitVersionResult is Error e)
            Log.Error($"No git command detected, {e}");
        var gitVersion = gitVersionResult is string version ? version : "";
        Log.Info($"Git:     {gitVersion}");

        config.Set(s => s.GitVersion = gitVersion);
    }
}

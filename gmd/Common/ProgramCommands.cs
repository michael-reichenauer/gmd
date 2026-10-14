using gmd.Installation;
using gmd.Server;

namespace gmd.Common;

// Handles command line options commands instead of running the UI
interface IProgramCommands
{
    // The exit code of the command the options name, or null when they name none and the UI is to run
    Task<int?> HandleCommandsAsync(string[] args);
}

// cSpell:ignore updatechangelog
class ProgramCommands : IProgramCommands
{
    const string ChangeLogHeading = "# Change Log for Gmd\n--------------------";

    readonly IUpdater updater;
    readonly IServer server;

    public ProgramCommands(IUpdater updater, IServer server)
    {
        this.updater = updater;
        this.server = server;
    }

    public async Task<int?> HandleCommandsAsync(string[] args)
    {
        if (HasOptions(args, "--help", "-h", "-?"))
            return ShowHelp();
        if (HasOptions(args, "--version"))
            return ShowVersion();
        if (HasOptions(args, "--update", "-u"))
            return await UpdateAsync();
        if (HasOptions(args, "--changelog"))
            return await ShowChangeLogAsync();
        if (HasOptions(args, "--updatechangelog"))
            return await UpdateChangeLogAsync(OptionValue(args, "--updatechangelog"));

        return null;
    }

    static int ShowHelp()
    {
        var msg = $"""
            gmd ({Build.Version()})
            Usage gmd [options] [arguments]

            options:
              --version           Show current version
              --update|-u         Update gmd to latest version (downloading from GitHub)
              --changelog         Show change log
              --updatechangelog [<version>]
                                  Update change log file CHANGELOG.md, with the commits since the
                                  latest release headed <version> rather than Current
              -d <path>           Show repo for working folder specified by <path>
              -m                  Show the start page, even in a repository
              --help|-h|-?        Show command line help.

            The help on using gmd, which ? opens in gmd:
              {Project.HelpUrl()}

            """;
        Console.WriteLine(msg);
        return 0;
    }

    static int ShowVersion()
    {
        Console.WriteLine(Build.Version());
        return 0;
    }

    async Task<int> UpdateAsync()
    {
        var currentVersion = Build.Version();
        Console.WriteLine($"Trying to update current version {currentVersion} ...");

        var availableResult = await updater.IsUpdateAvailableAsync();
        if (availableResult is not UpdateAvailability available)
        {
            Console.WriteLine($"Failed to check for updates: {availableResult.Error}");
            return -1;
        }
        if (!available.IsAvailable)
        {
            Console.WriteLine($"{available.Version} is already latest version.");
            return 0;
        }
        if (updater.PackageManager is PackageManager manager)
        {
            Console.WriteLine($"{available.Version} is available, and gmd was installed with {manager.Name},");
            Console.WriteLine(
                $"so update it{(manager.IsClosedFirst ? ", once gmd is closed," : "")} with: {manager.UpdateCommand}"
            );
            return -1;
        }

        Console.WriteLine($"Downloading {available.Version} ...");
        var newVersionResult = await updater.UpdateAsync();
        if (newVersionResult is not Version newVersion)
        {
            Console.WriteLine($"Failed to update: {newVersionResult.Error}");
            return -1;
        }

        Console.WriteLine($"Updated {currentVersion} -> {newVersion}");
        return 0;
    }

    async Task<int> ShowChangeLogAsync()
    {
        Console.WriteLine(ChangeLogHeading);
        var logResult = await server.GetChangeLogAsync();
        if (logResult is not string log)
        {
            Log.Error($"Failed to get change log, {logResult.Error}");
            Console.WriteLine($"Failed to get change log, {logResult.Error}");
            return -1;
        }

        Console.WriteLine(log);
        return 0;
    }

    // Fails with a non-zero exit code, since CI runs it to make the release commit on main
    async Task<int> UpdateChangeLogAsync(string? newRelease)
    {
        Console.WriteLine("Generating change log ...");
        var logResult = await server.GetChangeLogAsync(newRelease);
        if (logResult is not string log)
        {
            Console.WriteLine($"Failed to get change log, {logResult.Error}");
            return -1;
        }

        var text = $"{ChangeLogHeading}\n{log}";
        if (Result.Catch(() => File.WriteAllText("CHANGELOG.md", text)) is Error e)
        {
            Console.WriteLine($"Failed to write change log, {e}");
            return -1;
        }
        Console.WriteLine("Generated change log");
        return 0;
    }

    static bool HasOptions(string[] args, params string[] options) => options.Any(args.Contains);

    // The argument after an option, unless it is missing or is another option
    internal static string? OptionValue(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith('-') ? args[index + 1] : null;
    }
}

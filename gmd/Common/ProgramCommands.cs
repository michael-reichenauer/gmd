using gmd.Installation;
using gmd.Server;

namespace gmd.Common;

record CommandResult(bool IsCommand, int ExitCode);

// Handles command line options commands instead of running the UI
interface IProgramCommands
{
    Task<CommandResult> HandleCommands(string[] args);
}

// cSpell:ignore updatechangelog
class ProgramCommands : IProgramCommands
{
    readonly IUpdater updater;
    readonly IServer server;

    public ProgramCommands(IUpdater updater, IServer server)
    {
        this.updater = updater;
        this.server = server;
    }

    public async Task<CommandResult> HandleCommands(string[] args)
    {
        if (HasOptions(args, "--help", "-h", "-?"))
        {
            return new CommandResult(true, ShowHelp());
        }
        if (HasOptions(args, "--version"))
        {
            return new CommandResult(true, ShowVersion());
        }
        if (HasOptions(args, "--update", "-u"))
        {
            return new CommandResult(true, await UpdateAsync());
        }
        if (HasOptions(args, "--changelog"))
        {
            return new CommandResult(true, ShowChangeLog());
        }
        if (HasOptions(args, "--updatechangelog"))
        {
            return new CommandResult(true, UpdateChangeLog());
        }

        return new CommandResult(false, 0);
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
              --updatechangelog   Update change log file CHANGELOG.md
              -d <path>           Show repo for working folder specified by <path>
              -m                  Show main menu even if in working folder
              --help|-h|-?        Show command line help.

            """;
        Console.WriteLine(msg);
        return 0;
    }

    static int ShowVersion()
    {
        Console.WriteLine($"{Build.Version()}");
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

    int ShowChangeLog()
    {
        Task.Run(async () =>
            {
                Console.WriteLine($"# Change Log for Gmd\n--------------------");
                var logResult = await server.GetChangeLogAsync();
                if (logResult is not string log)
                {
                    Log.Error($"Failed to get change log, {logResult.Error}");
                    Console.WriteLine($"Failed to get change log, {logResult.Error}");
                    return;
                }

                Console.WriteLine($"{log}");
            })
            .Wait();

        return 0;
    }

    int UpdateChangeLog()
    {
        Task.Run(async () =>
            {
                Console.WriteLine($"Generating change log ...");
                var logResult = await server.GetChangeLogAsync();
                if (logResult is not string log)
                {
                    Console.WriteLine($"Failed to get change log, {logResult.Error}");
                    return;
                }

                var text = $"# Change Log for Gmd\n--------------------\n{log}";
                if (R.Catch(() => File.WriteAllText("CHANGELOG.md", text)) is Error e)
                {
                    Console.WriteLine($"Failed to write change log, {e}");
                }
                Console.WriteLine($"Generated change log");
            })
            .Wait();
        return 0;
    }

    static bool HasOptions(string[] args, params string[] options)
    {
        return options.Any(x => args.Contains(x));
    }
}

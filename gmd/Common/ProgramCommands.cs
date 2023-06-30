using gmd.Installation;
using gmd.Server;

record CommandResult(bool IsCommand, int ExitCode);

// Handles command line options commands instead of running the UI
interface IProgramCommands
{
    Task<CommandResult> HandleCommands(string[] args);
}

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
        if (HasOptions(args, "--update"))
        {
            return new CommandResult(true, await UpdateAsync());
        }
        if (HasOptions(args, "--changelog"))
        {
            return new CommandResult(true, ShowShangeLog());
        }
        if (HasOptions(args, "--updatechangelog"))
        {
            return new CommandResult(true, UpdateShangeLog());
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
          --update            Update gmd to latest version (downloading from GitHub)
          --changelog         Show change log
          --updatechangelog   Update change log file CHANGELOG.md
          -d <path>           Show repo for working folder specified by <path>
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


    int ShowShangeLog()
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

    int UpdateShangeLog()
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


    static bool HasOptions(string[] args, params string[] options)
    {
        return options.Any(x => args.Contains(x));
    }
}



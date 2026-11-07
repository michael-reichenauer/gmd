using gmd.Cui.WebLog;
using gmd.Installation;

namespace gmd.Common;

// Handles command line options commands instead of running the UI
interface IProgramCommands
{
    // The exit code of the command the options name, or null when they name none and the UI is to run
    Task<int?> HandleCommandsAsync(string[] args);
}

class ProgramCommands : IProgramCommands
{
    readonly IUpdater updater;
    readonly IWebLogExport webLogExport;

    public ProgramCommands(IUpdater updater, IWebLogExport webLogExport)
    {
        this.updater = updater;
        this.webLogExport = webLogExport;
    }

    public async Task<int?> HandleCommandsAsync(string[] args)
    {
        if (HasOptions(args, "--help", "-h", "-?"))
            return ShowHelp();
        if (HasOptions(args, "--version"))
            return ShowVersion();
        if (HasOptions(args, "--update", "-u"))
            return await UpdateAsync();
        if (HasOptions(args, "--web-log"))
            return await WriteWebLogAsync(args);

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
              -d <path>           Show repo for working folder specified by <path>
              -m                  Show the start page, even in a repository
              --web-log <file>    Write the branch graph of all active branches as a web page,
                                  of the repo in the current folder or the one -d names
                --prs <file>      ... with the open pull requests 'gh pr list --json
                                  number,title,headRefName,url,isDraft' wrote to <file>
                --max <n>         ... with at most <n> commits (default 500)
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

    async Task<int> WriteWebLogAsync(string[] args)
    {
        if (OptionValue(args, "--web-log") is not string outPath)
        {
            Console.Error.WriteLine("--web-log needs the file to write");
            return -1;
        }
        var repoPath = OptionValue(args, "-d") ?? Environment.CurrentDirectory;
        var pullRequestsPath = OptionValue(args, "--prs") ?? "";
        var maxRows = int.TryParse(OptionValue(args, "--max"), out var max) && max > 0 ? max : 500;

        if (await webLogExport.WriteAsync(repoPath, outPath, pullRequestsPath, maxRows) is Error e)
        {
            Console.Error.WriteLine($"Failed to write the web log: {e.AllMessages()}");
            return -1;
        }
        Console.WriteLine($"Wrote {outPath}");
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

using System.Runtime.InteropServices;
using gmd.Common;
using gmd.Cui.Common;
using gmd.Installation;

namespace gmd.Cui.App;

enum IssueKind
{
    Bug,
    Suggestion,
}

interface IIssueReport
{
    // Opens the issue form of a bug report or a suggestion on GitHub, with what gmd knows of itself
    // and where it runs already filled in
    Task OpenAsync(IssueKind kind);
}

// A bug report or a suggestion, as the issue forms in .github/ISSUE_TEMPLATE ask for them, opened with
// the version, the system and terminal, and the git version filled in, which a user would otherwise
// have to look up, and which are what a problem is first narrowed down by. The log is not: it holds
// the paths of the user's repositories and the names of their branches, and a link is no place for
// it, so the status line says where it is, for adding it to the report.
class IssueReport : IIssueReport
{
    readonly Config config;
    readonly IWebPage webPage;
    readonly IStatusLine status;

    public IssueReport(Config config, IWebPage webPage, IStatusLine status)
    {
        this.config = config;
        this.webPage = webPage;
        this.status = status;
    }

    public async Task OpenAsync(IssueKind kind)
    {
        var running = RunningGmd.Current;
        var version = VersionText(Build.Version(), Build.Sha(), running.IsDevInstance, running.PackageManager);
        var system = SystemText(
            OsName(),
            RuntimeInformation.OSArchitecture.ToString(),
            Environment.GetEnvironmentVariable,
            File.Exists,
            ColorsText(Palette.Current.Name, config.Colors)
        );
        var url = Url(kind, version, system, config.GitVersion);

        var name = kind == IssueKind.Bug ? "a bug report" : "a suggestion";
        await webPage.OpenAsync(url, name, kind == IssueKind.Bug ? "Report a Bug" : "Suggest an Improvement", false);
        if (kind == IssueKind.Bug)
            status.Info($"Add the log to the report, {Files.ShortHomePath(LogFile.FilePath, Files.HomePath)}");
    }

    // GitHub fills a field of an issue form from the query parameter named as the field's id
    internal static string Url(IssueKind kind, string version, string system, string git)
    {
        var url = $"{Project.IssuesUrl}/new?template={Template(kind)}&version={Uri.EscapeDataString(version)}";
        return kind == IssueKind.Bug
            ? $"{url}&system={Uri.EscapeDataString(system)}&git={Uri.EscapeDataString(git)}"
            : url;
    }

    static string Template(IssueKind kind) =>
        kind switch
        {
            IssueKind.Bug => "bug_report.yml",
            IssueKind.Suggestion => "suggestion.yml",
            _ => throw Asserter.Fail($"Unknown issue kind {kind}"),
        };

    // The version and the commit it was built from, and how it got here, since a gmd a package
    // manager installed is updated by that, and a dev build is no release at all
    internal static string VersionText(Version version, string sha, bool isDevInstance, PackageManager? manager)
    {
        var how =
            isDevInstance ? ", a dev build"
            : manager != null ? $", installed with {manager.Name}"
            : "";
        return $"{version.Txt()} ({sha}){how}";
    }

    // The operating system, the terminal, and what is between them, which is where most problems of
    // drawing, keys and the clipboard come from: ssh, WSL, a container, tmux. The variables are the
    // ones terminals and those tools set; the files the ones a container has.
    internal static string SystemText(
        string os,
        string arch,
        Func<string, string?> env,
        Func<string, bool> fileExists,
        string colors
    )
    {
        List<string> parts = [$"{os} {arch}", TerminalText(env)];

        if (IsSet(env("SSH_CONNECTION")) || IsSet(env("SSH_TTY")))
            parts.Add("over ssh");
        if (IsSet(env("WSL_DISTRO_NAME")))
            parts.Add($"WSL {env("WSL_DISTRO_NAME")}");
        if (IsSet(env("CODESPACES")))
            parts.Add("in a Codespace");
        else if (IsSet(env("REMOTE_CONTAINERS")))
            parts.Add("in a dev container");
        else if (fileExists("/.dockerenv") || fileExists("/run/.containerenv"))
            parts.Add("in a container");
        if (IsSet(env("TMUX")))
            parts.Add("in tmux");
        else if (env("TERM")?.StartsWith("screen") == true && IsSet(env("STY")))
            parts.Add("in screen");

        parts.Add($"colors {colors}");
        return string.Join(", ", parts);
    }

    // The palette drawn in, and the setting it was picked by, when that was not the palette itself
    internal static string ColorsText(string palette, string setting) =>
        palette == setting ? palette : $"{palette} ({setting})";

    // The terminal by the name it gives itself, with its version where it says it, and $TERM, which
    // tells what it claims to understand
    static string TerminalText(Func<string, string?> env)
    {
        var program = env("TERM_PROGRAM") ?? "";
        var name =
            IsSet(program) ? $"{program} {env("TERM_PROGRAM_VERSION")}".Trim()
            : IsSet(env("WT_SESSION")) ? "Windows Terminal"
            : "";
        var term = env("TERM") ?? "";
        return (IsSet(name), IsSet(term)) switch
        {
            (true, true) => $"{name} (TERM={term})",
            (true, false) => name,
            (false, true) => $"TERM={term}",
            _ => "unknown terminal",
        };
    }

    // The name a user knows the system by rather than the kernel's: the distribution on Linux, and
    // the version of macOS or Windows, which .NET reports as it is, not as the kernel's
    static string OsName()
    {
        if (Build.IsWindows)
            return $"Windows {Environment.OSVersion.Version}";
        if (Build.IsMacOS)
            return $"macOS {Environment.OSVersion.Version}";
        return LinuxName(Result.Catch(() => File.ReadAllText("/etc/os-release")) is string text ? text : "")
            ?? RuntimeInformation.OSDescription;
    }

    // PRETTY_NAME of /etc/os-release, e.g. 'Ubuntu 24.04.1 LTS', null when it has none
    internal static string? LinuxName(string osRelease)
    {
        var line = osRelease.Split('\n').FirstOrDefault(l => l.StartsWith("PRETTY_NAME="));
        var name = line?["PRETTY_NAME=".Length..].Trim().Trim('"');
        return IsSet(name) ? name : null;
    }

    static bool IsSet(string? value) => !string.IsNullOrWhiteSpace(value);
}

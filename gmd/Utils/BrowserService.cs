using System.Runtime.InteropServices;

namespace gmd.Utils;

// cSpell:ignore wslview rundll32

interface IBrowserService
{
    // Opens a web page in the browser of the user, an error when there is none to open it in
    Task<Result> OpenAsync(string url);
}

// Opening a web page means asking the desktop to, and which way there is to ask differs per
// platform and per session, much as for the clipboard (see ClipboardService), so this tries the
// ways there are in order and takes the first that works:
//
//   - $BROWSER, the convention for naming the browser to use, which comes first since it is what
//     the user or the environment chose. VS Code sets it in its terminals over ssh and in a
//     container, to a helper that opens the page on the machine the user is sitting at, which is
//     then the only way there is.
//   - the platform's own opener: the shell on Windows, 'open' on macOS, xdg-open on a Linux
//     desktop, and the Windows side of WSL.
//
// Over ssh with no $BROWSER there is no way at all, which is expected rather than a failure, and
// the caller copies the link instead.
class BrowserService : IBrowserService
{
    readonly ICmd cmd;

    public BrowserService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public Task<Result> OpenAsync(string url) => OpenAsync(url, CurrentOs, Environment.GetEnvironmentVariable);

    // Internal and taking the platform and the environment rather than reading them, so the tests
    // can drive a platform they are not running on.
    internal async Task<Result> OpenAsync(string url, OSPlatform os, Func<string, string?> env)
    {
        var openers = OpenersFor(url, os, env);
        if (openers.Count == 0)
            return new Error("There is no browser to open it in here");

        List<string> failures = [];
        foreach (var (path, args) in openers)
        {
            if (await cmd.StartAsync(path, args) is not Error e)
            {
                Log.Info($"Opened {url} using {path}");
                return Result.Ok;
            }

            failures.Add($"  {path}: {e.AllMessages().Split('\n')[0].Trim()}");
        }

        Log.Warn($"Failed to open {url} in a browser:\n{failures.Join("\n")}");
        return new Error("The browser could not be opened");
    }

    // The ways to open the page on this platform, in the order they are tried
    internal static IReadOnlyList<(string Path, string Args)> OpenersFor(
        string url,
        OSPlatform os,
        Func<string, string?> env
    )
    {
        List<(string, string)> openers = [];

        // A list, like PATH, of commands that take the page last or where '%s' is, e.g. 'firefox'
        // or 'firefox --new-tab %s'. A command is split at its first space, so one with a space in
        // its path is not supported, which the convention itself leaves unsaid.
        var browsers = env("BROWSER") ?? "";
        foreach (var browser in browsers.Split(os == OSPlatform.Windows ? ';' : ':').Select(b => b.Trim()))
        {
            if (browser == "")
                continue;
            var command = browser.Contains("%s") ? browser.Replace("%s", url) : $"{browser} {url}";
            var space = command.IndexOf(' ');
            openers.Add((command[..space], command[(space + 1)..]));
        }

        if (os == OSPlatform.Windows)
        {
            // The shell's handler for a URL, which opens the default browser. Not 'cmd /c start',
            // which would read the '%' and '&' in a link as its own syntax.
            openers.Add(("rundll32.exe", $"url.dll,FileProtocolHandler {url}"));
        }
        else if (os == OSPlatform.OSX)
        {
            openers.Add(("open", url));
        }
        else
        {
            // WSL without a Linux desktop opens the page on the Windows side, with wslview when the
            // wslu tools are there and else as Windows itself does
            if (IsSet(env("WSL_DISTRO_NAME")))
            {
                openers.Add(("wslview", url));
                openers.Add(("rundll32.exe", $"url.dll,FileProtocolHandler {url}"));
            }

            // A desktop to open it on, which xdg-open would otherwise fail to find
            if (IsSet(env("DISPLAY")) || IsSet(env("WAYLAND_DISPLAY")))
                openers.Add(("xdg-open", url));
        }

        return openers;
    }

    static OSPlatform CurrentOs =>
        Build.IsWindows ? OSPlatform.Windows
        : Build.IsMacOS ? OSPlatform.OSX
        : OSPlatform.Linux;

    // An empty variable is as good as an unset one here, and tests and tmux set them that way
    static bool IsSet(string? value) => !string.IsNullOrEmpty(value);
}

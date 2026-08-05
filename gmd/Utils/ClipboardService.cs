using System.Runtime.InteropServices;

namespace gmd.Utils;

// cSpell:ignore xsel xclip pbcopy

interface IClipboardService
{
    // Puts the text on the clipboard of the machine the user is sitting at
    R Set(string text);
}

// A clipboard belongs to the desktop session rather than to a process, so setting it means
// handing the text to whatever owns it. Which owner can be reached differs per platform, per
// session and per machine, so this tries them in order and takes the first that works instead of
// assuming one:
//
//   - a helper binary of the platform (pbcopy, wl-copy, xclip, xsel, clip.exe), given the text on
//     its stdin,
//   - the Win32 clipboard directly on Windows, which needs no tool to be installed,
//   - and last the terminal itself, via OSC 52, which is the only one that reaches the user's own
//     clipboard when gmd runs over ssh or in a container.
//
// The order matters as much as the list. A tool for the wrong display server does not just fail,
// it can succeed into a clipboard that nothing reads — an X tool under a native Wayland session
// reaches XWayland and nothing else — so only the ones the session can actually use are offered
// at all.
class ClipboardService : IClipboardService
{
    readonly ICmd cmd;
    readonly ITerminalClipboard terminal;

    public ClipboardService(ICmd cmd, ITerminalClipboard terminal)
    {
        this.cmd = cmd;
        this.terminal = terminal;
    }

    public R Set(string text) => Set(text, CurrentOs, Environment.GetEnvironmentVariable);

    // Internal and taking the platform and the environment rather than reading them, so the tests
    // can drive a platform they are not running on.
    internal R Set(string text, OSPlatform os, Func<string, string?> env)
    {
        List<string> failures = [];

        foreach (var writer in WritersFor(os, env))
        {
            if (Try(out var e, writer.Set(text)))
            {
                Log.Info($"Copied {text.Length} chars to clipboard using {writer.Name}");
                return R.Ok;
            }

            failures.Add($"  {writer.Name}: {FirstLine(e.AllErrorMessages())}");
        }

        var message = $"Failed to copy to the clipboard.\n{failures.Join("\n")}\n{InstallHint(os)}";
        Log.Warn(message);
        return R.Error(message);
    }

    // The ways to set the clipboard on this platform, in the order they are tried
    internal IReadOnlyList<ClipboardWriter> WritersFor(OSPlatform os, Func<string, string?> env)
    {
        List<ClipboardWriter> writers = [];

        if (os == OSPlatform.Windows)
        {
            // The API rather than a tool, since it is the clipboard itself and always there, and
            // since it takes the text as UTF-16, which is what Windows stores, so no encoding can
            // lose anything. clip.exe is the fallback for when another program is holding the
            // clipboard open for longer than WindowsClipboard retries.
            writers.Add(new ClipboardWriter("Win32 SetClipboardData", WindowsClipboard.TrySetText));
            writers.Add(Tool("clip.exe", ""));

            // No OSC 52 here: the two above cannot both be missing on Windows, and the sequence
            // would have to go through stdout, which Terminal.Gui owns.
            return writers;
        }

        if (os == OSPlatform.OSX)
        {
            // Part of macOS itself, so it needs no probing and no install hint
            writers.Add(Tool("pbcopy", ""));
        }
        else
        {
            // Linux has no clipboard of its own — it belongs to the display server — so a tool is
            // only worth trying when the session it talks to is actually there.
            if (IsSet(env("WAYLAND_DISPLAY")))
                writers.Add(Tool("wl-copy", ""));

            if (IsSet(env("DISPLAY")))
            {
                writers.Add(Tool("xclip", "-selection clipboard"));
                writers.Add(Tool("xsel", "--input --clipboard"));
            }

            // WSL with WSLg has both of the above and shares its clipboard with Windows, so this
            // is for WSL without it, where the Windows clipboard is still one process call away
            if (IsSet(env("WSL_DISTRO_NAME")))
                writers.Add(Tool("clip.exe", ""));
        }

        // Last, because it is the only one that cannot report whether it worked: a terminal that
        // does not support OSC 52 ignores the sequence in silence. So it is the answer only once
        // everything that can be checked has been ruled out.
        writers.Add(new ClipboardWriter("OSC 52 (the terminal)", terminal.Set));
        return writers;
    }

    ClipboardWriter Tool(string path, string args) =>
        new ClipboardWriter($"{path} {args}".Trim(), text => cmd.CommandWithStdin(path, args, text));

    // What the user can do about it, which is the part the old "not supported on this platform"
    // message was missing
    static string InstallHint(OSPlatform os) =>
        os == OSPlatform.Linux
            ? "Install a clipboard tool (wl-clipboard, xclip or xsel), " + "or use a terminal with OSC 52 copy enabled."
            : "Use a terminal with OSC 52 copy enabled.";

    static OSPlatform CurrentOs =>
        Build.IsWindows ? OSPlatform.Windows
        : Build.IsMacOS ? OSPlatform.OSX
        : OSPlatform.Linux;

    // An empty variable is as good as an unset one here, and tests and tmux set them that way
    static bool IsSet(string? value) => !string.IsNullOrEmpty(value);

    static string FirstLine(string text) => text.Split('\n')[0].Trim();
}

// One way of putting text on the clipboard. The name is what the log and the error message call
// it, i.e. the command line of a tool.
record ClipboardWriter(string Name, Func<string, R> Set);

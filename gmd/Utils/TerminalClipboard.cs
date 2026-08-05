using System.Text;

namespace gmd.Utils;

interface ITerminalClipboard
{
    // Asks the terminal to put the text on its clipboard, using OSC 52
    R Set(string text);
}

// The terminal's own copy mechanism: the text is base64 encoded into an escape sequence, and the
// terminal that receives it puts it on the clipboard of the machine it is running on — which is
// the machine the user is sitting at, whether or not that is the machine gmd is running on.
//
// That is what makes it worth having. Over ssh, or from inside a container, no local helper binary
// can reach the user's clipboard: xclip on the remote host copies into the remote host's X
// session, which nobody is looking at. For a terminal git client that is a common way to be run,
// and OSC 52 is the only mechanism that covers it.
//
// The catch, and the reason Clipboard tries this last, is that there is no reply. A terminal that
// does not support the sequence, or that has it disabled, ignores it in silence, so all this can
// ever report is that the text was sent.
class TerminalClipboard : ITerminalClipboard
{
    // The controlling terminal rather than stdout: Terminal.Gui owns stdout, and stdout may also
    // have been redirected, while /dev/tty is the terminal either way.
    const string TtyPath = "/dev/tty";

    // Terminals cap what they accept — xterm and tmux both have a limit in this region — and a
    // sequence over the limit is truncated rather than refused, so a large copy would land as a
    // silently cut-off one. Better to say so and let the tools that have no such limit be tried.
    const int MaxEncodedLength = 100000;

    public R Set(string text)
    {
        if (Build.IsWindows)
            return R.Error("OSC 52 is only used on Unix, where the terminal can be written to directly");

        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        if (encoded.Length > MaxEncodedLength)
            return R.Error($"Too much text for the terminal to copy ({encoded.Length} > {MaxEncodedLength} bytes)");

        // ESC ] 52 ; c ; <base64> BEL, where 'c' selects the clipboard (as opposed to the primary
        // selection) and BEL terminates the string, which more terminals accept than ST does.
        var sequence = $"\u001b]52;c;{encoded}\u0007";

        if (!Try(out var e, () => Write(sequence)))
            return R.Error($"Failed to write to {TtyPath}", e);

        return R.Ok;
    }

    static void Write(string sequence)
    {
        // A character device is not seekable, so it is opened rather than appended to, and shared
        // because the terminal is of course already open for the drawing
        using var stream = new FileStream(TtyPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(sequence);
    }
}

using System.Runtime.InteropServices;
using gmd.Utils;

namespace gmdTest.Utils;

// The tool chain is the part of copying that can be tested without a clipboard: which tools are
// offered for a platform and a session, in which order, what is piped to them, and what happens
// when one of them is missing. Whether the text really ends up on the clipboard afterwards cannot
// be faked — that is what the tmux end-to-end test and the manual checks in MODERNIZATION.md are
// for.
[TestClass]
public class ClipboardServiceTest
{
    static readonly Func<string, string?> NoEnv = _ => null;

    [TestMethod]
    public void TestMacOsUsesPbcopy()
    {
        Assert.AreEqual("pbcopy, OSC 52 (the terminal)", Chain(OSPlatform.OSX, NoEnv));
    }

    [TestMethod]
    public void TestWindowsUsesTheApiBeforeClipExe()
    {
        // And no OSC 52: those two cannot both be missing on Windows
        Assert.AreEqual("Win32 SetClipboardData, clip.exe", Chain(OSPlatform.Windows, NoEnv));
    }

    [TestMethod]
    public void TestWaylandSessionUsesWlCopy()
    {
        var env = Env(("WAYLAND_DISPLAY", "wayland-0"));

        Assert.AreEqual("wl-copy, OSC 52 (the terminal)", Chain(OSPlatform.Linux, env));
    }

    [TestMethod]
    public void TestX11SessionUsesXclipThenXsel()
    {
        var env = Env(("DISPLAY", ":0"));

        Assert.AreEqual(
            "xclip -selection clipboard, xsel --input --clipboard, OSC 52 (the terminal)",
            Chain(OSPlatform.Linux, env)
        );
    }

    // A Wayland session usually has DISPLAY set too, for XWayland, and an X tool then reaches only
    // the XWayland clipboard — i.e. it succeeds and pastes nothing into a native application. So
    // wl-copy has to come first, which is the whole reason the order is asserted here.
    [TestMethod]
    public void TestWaylandComesBeforeX11WhenBothAreSet()
    {
        var env = Env(("WAYLAND_DISPLAY", "wayland-0"), ("DISPLAY", ":0"));

        Assert.AreEqual(
            "wl-copy, xclip -selection clipboard, xsel --input --clipboard, OSC 52 (the terminal)",
            Chain(OSPlatform.Linux, env)
        );
    }

    // ssh without X forwarding, and any container: no tool on this machine can reach the clipboard
    // of the machine the user is sitting at, so the terminal is the only way there
    [TestMethod]
    public void TestNoDisplayLeavesOnlyTheTerminal()
    {
        Assert.AreEqual("OSC 52 (the terminal)", Chain(OSPlatform.Linux, NoEnv));
    }

    [TestMethod]
    public void TestWslWithoutADisplayUsesClipExe()
    {
        var env = Env(("WSL_DISTRO_NAME", "Ubuntu"));

        Assert.AreEqual("clip.exe, OSC 52 (the terminal)", Chain(OSPlatform.Linux, env));
    }

    // An empty variable is what tmux and a login shell leave behind, and it means no session just
    // as much as an unset one does
    [TestMethod]
    public void TestEmptyDisplayCountsAsNoDisplay()
    {
        var env = Env(("DISPLAY", ""), ("WAYLAND_DISPLAY", ""));

        Assert.AreEqual("OSC 52 (the terminal)", Chain(OSPlatform.Linux, env));
    }

    [TestMethod]
    public void TestTextIsPipedToTheToolOnStdin()
    {
        var cmd = new FakeCmd("");
        var terminal = new FakeTerminalClipboard();
        var clipboard = new ClipboardService(cmd, terminal);

        Assert.IsTrue(Try(out var e, clipboard.Set("some text", OSPlatform.OSX, NoEnv)), $"{e}");

        Assert.AreEqual(1, cmd.Calls.Count);
        Assert.AreEqual("pbcopy", cmd.Calls[0].Path);
        Assert.AreEqual("some text", cmd.Calls[0].Stdin);
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            terminal.Texts,
            "The tool worked, so the terminal is not used"
        );
    }

    // A missing tool is the normal case rather than an error: which of them a machine has differs
    // per distribution, which is why there is a chain at all
    [TestMethod]
    public void TestNextToolIsUsedWhenTheFirstOneFails()
    {
        var cmd = new FakeCmd((path, _, _) => path == "xsel" ? FakeCmd.Ok("") : FakeCmd.Fail("xclip: not found", -1));
        var terminal = new FakeTerminalClipboard();
        var clipboard = new ClipboardService(cmd, terminal);

        Assert.IsTrue(Try(out var e, clipboard.Set("text", OSPlatform.Linux, Env(("DISPLAY", ":0")))), $"{e}");

        CollectionAssert.AreEqual(new[] { "xclip", "xsel" }, cmd.Calls.Select(c => c.Path).ToArray());
        CollectionAssert.AreEqual(Array.Empty<string>(), terminal.Texts);
    }

    [TestMethod]
    public void TestTerminalIsTheLastResortWhenNoToolWorks()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("Error: Can't open display"));
        var terminal = new FakeTerminalClipboard();
        var clipboard = new ClipboardService(cmd, terminal);

        Assert.IsTrue(Try(out var e, clipboard.Set("text", OSPlatform.Linux, Env(("DISPLAY", ":0")))), $"{e}");

        CollectionAssert.AreEqual(new[] { "text" }, terminal.Texts);
    }

    // What the user is shown when nothing worked. The old message said "not supported on this
    // platform", which was both wrong and impossible to act on.
    [TestMethod]
    public void TestFailureNamesEveryToolTriedAndWhatToInstall()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("Error: Can't open display"));
        var terminal = new FakeTerminalClipboard(R.Error("No /dev/tty"));
        var clipboard = new ClipboardService(cmd, terminal);

        Assert.IsFalse(Try(out var e, clipboard.Set("text", OSPlatform.Linux, Env(("DISPLAY", ":0")))));

        var message = e.AllErrorMessages();
        StringAssert.Contains(message, "xclip -selection clipboard: Error: Can't open display");
        StringAssert.Contains(message, "xsel --input --clipboard: Error: Can't open display");
        StringAssert.Contains(message, "OSC 52 (the terminal): No /dev/tty");
        StringAssert.Contains(message, "Install a clipboard tool (wl-clipboard, xclip or xsel)");
    }

    static string Chain(OSPlatform os, Func<string, string?> env) =>
        new ClipboardService(new FakeCmd(""), new FakeTerminalClipboard())
            .WritersFor(os, env)
            .Select(w => w.Name)
            .Join(", ");

    static Func<string, string?> Env(params (string Name, string Value)[] variables) =>
        name => variables.Where(v => v.Name == name).Select(v => v.Value).FirstOrDefault();
}

// A double for the OSC 52 writer, which cannot run in a test: it writes to the terminal, and
// there is none
class FakeTerminalClipboard : ITerminalClipboard
{
    readonly R result;

    public FakeTerminalClipboard(R? result = null) => this.result = result ?? R.Ok;

    // Every text it was asked to copy, in order
    public List<string> Texts { get; } = [];

    public R Set(string text)
    {
        Texts.Add(text);
        return result;
    }
}

using System.Text;
using IOPath = System.IO.Path;

namespace gmdTest.Fixtures;

// Drives the built gmd binary in a tmux pane and reads back the rendered screen.
//
// tmux parses the escape sequences and keeps a screen model, so 'capture-pane' hands back what
// the user sees as plain text, which is what makes it assertable. A raw pty would only give the
// redraw traffic. See CLAUDE.md, "Running the TUI from a non-interactive shell".
//
// Nothing about Terminal.Gui is named here, deliberately: these tests are meant to be as valid
// against a Terminal.Gui 2.x build as a 1.x one, so they can be the acceptance suite for that
// port.
//
// Use like e.g.:
//     using var repo = await E2eRepo.CreateAsync();
//     using var gmd = TmuxSession.StartGmd(repo);
//     ScreenText.AssertEqual("""...""", gmd.WaitFor("Initial"), repo.Path);
sealed class TmuxSession : IDisposable
{
    public const int DefaultWidth = 120;
    public const int DefaultHeight = 40;

    // How many identical captures in a row count as a settled screen. Not cosmetic: gmd swallows
    // every keystroke while a git command is running (Progress.Show calls UI.StopInput, which is
    // 'RootKeyEvent = _ => true', i.e. keys are dropped rather than queued), and the progress
    // marquee redraws over the application bar every 100 ms while one is. So a key must never be
    // sent into a screen that is still moving, and three identical captures is the evidence that
    // it is not.
    const int StableCount = 3;
    const int PollMs = 100;
    const int DefaultTimeoutMs = 30000;

    // Only the session's own private server is ever talked to, so a leaked kill cannot reach the
    // developer's tmux
    readonly string socket;
    readonly TempHome home;
    readonly string repoPath;
    readonly int width;
    readonly int height;

    // Set by the environment to leave the session up after a test, for looking at a screen a
    // snapshot disagrees with
    static bool IsKeepAlive => Environment.GetEnvironmentVariable("GMD_E2E_KEEP") == "1";

    TmuxSession(string socket, TempHome home, string repoPath, int width, int height)
    {
        this.socket = socket;
        this.home = home;
        this.repoPath = repoPath;
        this.width = width;
        this.height = height;
    }

    // The throwaway HOME the app ran under, so a test can assert what gmd wrote there
    public string Home => home.Path;

    public static TmuxSession StartGmd(TempRepo repo, int width = DefaultWidth, int height = DefaultHeight) =>
        StartGmd(repo.Path, width, height);

    public static TmuxSession StartGmd(
        string repoPath,
        int width = DefaultWidth,
        int height = DefaultHeight,
        params string[] extraArgs
    )
    {
        EnsureTmux();

        var session = new TmuxSession($"gmd-e2e-{Guid.NewGuid():N}", TempHome.Create(), repoPath, width, height);
        try
        {
            session.Start(extraArgs);
        }
        catch (Exception)
        {
            // The caller's 'using' never got the session, so nothing else would clean it up
            session.Dispose();
            throw;
        }

        return session;
    }

    // The rendered screen, as the user sees it. tmux already drops trailing blank cells.
    public string Capture() => Tmux("capture-pane", "-p", "-t", "gmd").Output;

    // The same with the colors kept as ANSI escapes. Unused by the tests so far, but it is the
    // only way to reach what is highlighted, and the highlight is how gmd draws the current row.
    public string CaptureColors() => Tmux("capture-pane", "-p", "-e", "-t", "gmd").Output;

    // Polls until the screen contains the text and has then stopped changing, and returns it.
    // Never sleeps a fixed time.
    public string WaitFor(string expected, int timeoutMs = DefaultTimeoutMs) =>
        Poll(screen => screen.Contains(expected), $"text '{expected}' to appear", timeoutMs);

    // Polls until the text is gone and the screen has stopped changing. Needed because a modal
    // dialog is drawn over the log view rather than replacing it, so the rows behind it still
    // match anything WaitFor would look for. This is what "the dialog closed" means.
    public string WaitUntilGone(string expected, int timeoutMs = DefaultTimeoutMs) =>
        Poll(screen => !screen.Contains(expected), $"text '{expected}' to disappear", timeoutMs);

    // Polls until the screen has simply stopped changing
    public string WaitForStable(int timeoutMs = DefaultTimeoutMs) => Poll(_ => true, "the screen to settle", timeoutMs);

    // Sends keys, e.g. "q", "Down", "Escape", "S-Right". The screen must have settled first, see
    // StableCount.
    public void Send(params string[] keys) => Tmux(["send-keys", "-t", "gmd", .. keys]);

    // Sends text literally, for typing into a dialog, so that e.g. "Down" is five characters
    // rather than a cursor key
    public void SendText(string text) => Tmux("send-keys", "-t", "gmd", "-l", text);

    // Whether gmd is still running. The pane is kept after it exits (remain-on-exit), so this is
    // about the process, not the session.
    //
    // Only whether it exited is available, not with what code: tmux leaves '#{pane_dead_status}'
    // empty for a binary it exec'd directly, and only fills it in when the pane command went
    // through a shell. gmd does exit 0 — checked by running it as 'sh -c "gmd …; echo $?"', which
    // reports 0 and makes tmux report 0 as well — but adding that shell just to read the code
    // would put a wrapper process between tmux and gmd, which CLAUDE.md warns against. A crash
    // shows up as a WaitFor timing out with the screen and the log in the message, which is
    // better evidence than an exit code anyway.
    public bool IsRunning => Display("#{pane_dead}") == "0";

    public void WaitForExit(int timeoutMs = DefaultTimeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (!IsRunning)
                return;
            Thread.Sleep(PollMs);
        }

        Assert.Fail($"gmd was still running after {timeoutMs} ms\n{Diagnostics(Capture())}");
    }

    public void Dispose()
    {
        if (IsKeepAlive)
        {
            Log.Info($"GMD_E2E_KEEP: attach with 'tmux -L {socket} attach', home is '{home.Path}'");
            return;
        }

        // The server is private to this session, so killing it cannot reach anything else, and
        // it is done before the home is deleted so gmd is gone before its state is, and so that
        // the socket inside the home is not pulled out from under a running server
        Tmux("kill-server");
        home.Dispose();
    }

    void Start(string[] extraArgs)
    {
        // The options have to be in a config file rather than 'set-option' calls: a tmux server
        // with no session exits immediately, so 'start-server' followed by 'set-option' would
        // silently configure a server that is already gone, and new-session would then start a
        // fresh one with the defaults. Passing -f applies them before the pane can run.
        //
        // remain-on-exit is the one that matters most. Without it a gmd that dies at startup
        // takes the session with it, and every capture fails with "can't find session" rather
        // than showing the screen and the exit code that say what happened.
        var conf = IOPath.Join(home.Path, "tmux.conf");
        File.WriteAllText(
            conf,
            """
            set-option -g remain-on-exit on
            set-option -g status off
            set-option -g default-terminal "screen-256color"
            """
        );

        string[] command = [BinaryPath(), "-d", repoPath, .. extraArgs];

        // A private server rather than a named session on the developer's: it cannot see their
        // ~/.tmux.conf (which could turn the status bar on, change default-terminal or enable
        // mouse reporting, all of which would change the capture), it cannot be disturbed by
        // them, and kill-server in Dispose is then total and safe.
        Tmux([
            "-f",
            conf,
            "new-session",
            "-d",
            "-s",
            "gmd",
            "-x",
            $"{width}",
            "-y",
            $"{height}",
            "-c",
            repoPath,
            .. EnvironmentVariables().SelectMany(e => new[] { "-e", e }),
            .. command,
        ]);

        // Detached sessions take their size from -x/-y, but that interacts with the window-size
        // and default-size options and has moved between tmux versions. Asserting it here turns
        // a future change into one clear failure rather than every snapshot failing at once.
        var size = Display("#{pane_width}x#{pane_height}");
        Assert.AreEqual($"{width}x{height}", size, "The tmux pane is not the size the test asked for");
    }

    // The environment gmd runs under. This is the whole hermeticity story, see TempHome.
    IReadOnlyList<string> EnvironmentVariables() =>
        [
            $"HOME={home.Path}",
            // HOME alone does not cover git, which also reads $XDG_CONFIG_HOME/git/config
            $"XDG_CONFIG_HOME={IOPath.Join(home.Path, ".config")}",
            $"TMPDIR={IOPath.Join(home.Path, "tmp")}",
            // The time column is the author time in LOCAL time (RepoWriter.WriteTime), so
            // without this the snapshots differ between a developer's machine and CI
            "TZ=UTC",
            // UTF-8 for the '● ┣ ┅ Ϙ' the UI is drawn with, and a pinned culture because
            // RepoWriter formats the time with the current culture and no CultureInfo, where
            // ':' is the culture's time separator rather than a literal
            "LANG=C.UTF-8",
            "LC_ALL=C.UTF-8",
            // HOME does not cover /etc/gitconfig, which a CI image may populate
            "GIT_CONFIG_NOSYSTEM=1",
            // Nothing may ever block on a credential prompt in a pane nobody is watching
            "GIT_TERMINAL_PROMPT=0",
        ];

    // Polls capture-pane until the condition holds and the screen has then been unchanged for
    // StableCount polls in a row
    string Poll(Func<string, bool> isDone, string what, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var screen = "";
        var previous = "";
        var stable = 0;

        while (DateTime.UtcNow < deadline)
        {
            screen = Capture();
            stable = screen == previous ? stable + 1 : 0;
            previous = screen;

            if (isDone(screen) && stable >= StableCount)
                return screen;

            Thread.Sleep(PollMs);
        }

        Assert.Fail(
            $"Timed out after {timeoutMs} ms waiting for {what}, "
                + $"the screen was unchanged for {stable} of {StableCount} polls.\n{Diagnostics(screen)}"
        );
        return screen;
    }

    // Everything worth knowing when a wait times out. The log is usually the whole answer, since
    // an unhandled exception goes there rather than to the screen.
    string Diagnostics(string screen)
    {
        var rule = new string('-', width);
        return new StringBuilder()
            .AppendLine($"Pane: dead={Display("#{pane_dead}")} status={Display("#{pane_dead_status}")}")
            .AppendLine($"Screen ({width}x{height}):")
            .AppendLine(rule)
            .AppendLine(screen)
            .AppendLine(rule)
            .AppendLine($"Tail of {home.Path}/gmd.log:")
            .AppendLine(home.LogTail())
            .ToString();
    }

    // A pane property, e.g. whether it is still running. The session outlives gmd
    // (remain-on-exit), so a failure here means the session itself is gone, which is a broken
    // harness rather than a finished app and must not be mistaken for one.
    string Display(string format)
    {
        var result = Tmux("display-message", "-p", "-t", "gmd", format);
        Assert.IsTrue(result.IsOk, $"The tmux session is gone, so '{format}' could not be read.\n{result}");
        return result.Output.Trim();
    }

    // Every tmux command goes to this session's own private server.
    //
    // TMUX_TMPDIR puts its socket inside the throwaway home rather than in the shared
    // /tmp/tmux-<uid>/, for two reasons: the developer's own server, which lives there under the
    // default name, is then unreachable from here by construction, and the socket is deleted
    // with the home. kill-server stops the server but leaves its socket file behind, so without
    // this a suite run leaves one dead socket per test lying around for good.
    ProcResult Tmux(params string[] args) =>
        Proc.Run("tmux", ["-L", socket, .. args], env: new Dictionary<string, string> { ["TMUX_TMPDIR"] = home.Path });

    // The binary the test project already builds. gmdTest references gmd, which is an Exe, so
    // MSBuild copies its apphost into the test output, which means it is always in step with the
    // test assembly and needs no path arithmetic up to the repository root.
    static string BinaryPath()
    {
        var name = OperatingSystem.IsWindows() ? "gmd.exe" : "gmd";
        var path = IOPath.Join(AppContext.BaseDirectory, name);
        Assert.IsTrue(File.Exists(path), $"'{path}' is missing, build the solution first (dotnet build gmd.sln)");
        return path;
    }

    // Absence of tmux is a clear failure rather than a silent skip, so that this tier cannot
    // quietly stop running in CI
    static void EnsureTmux()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("The end-to-end UI tests drive the app through tmux, which is Unix only");

        if (!Proc.CanRun("tmux", "-V"))
            Assert.Fail("tmux is not installed, run ./installtools (the end-to-end UI tests drive the TUI in it)");
    }
}

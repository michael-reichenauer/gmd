using gmdTest.Fixtures;

// End-to-end tests: the built gmd binary, real git, a real pty. tmux keeps a screen model, so
// what is asserted is the rendered screen — the drawing, the layout, the key dispatch and the
// dialogs, none of which any other test in this suite reaches. The tests are in Cui/, one class
// per area of the app, placed as the code they reach is in gmd/Cui/.
//
// They name no Terminal.Gui type, deliberately, so they are as valid against a 2.x build as a
// 1.x one, which is what makes them the acceptance suite for that port. They are
// characterization tests: they capture what gmd draws today, not what it ought to draw.
//
// Two rules for anything added here, both learned the hard way:
//   - Never send a key into a screen that has not settled. gmd drops keystrokes while a git
//     command is running rather than queueing them, so a key sent too early is silently lost.
//     Every Send is therefore preceded by a WaitFor.
//   - Escape in the log view asks "Quit gmd?" with Yes as the default, so a stray Escape leaves a
//     question up and the next Enter quits. Use WaitUntilGone to close a dialog and check it
//     really closed, rather than sending a second Escape for safety.
// CLAUDE.md has the rest, under TmuxSession.
//
// They need tmux, which ./installtools installs, and are in their own categories. Those are set
// here for the whole assembly rather than on each class, so that a new test class cannot be
// added without them and end up in the fast run:
//   ./test --filter "TestCategory!=Integration"   excludes these and the real git tests
//   ./test --filter "TestCategory=E2e"            runs only these
[assembly: TestCategory("Integration")]
[assembly: TestCategory("E2e")]

// The end-to-end tests run in parallel, one test per worker. They can: every test already owns
// everything it touches — its own temp repository, its own throwaway HOME and its own private tmux
// server (see TmuxSession) — and none of them changes process state such as the culture or an
// environment variable. It is what makes this tier fast: a test is nearly all waiting for a
// screen to settle, not CPU, so a run went from about four minutes to about half a minute. The
// worker count is fixed rather than the core count for the same reason, and 8 ran clean on 2, 4
// and 9 cores. What bounds the run now is its longest test, the thirty second worktree re-read.
//
// The unit tests in gmdTest cannot do the same, since several of them do change process state,
// and MSTest sets this per assembly, which is why these tests are a project of their own.
//
// A single test runs sequentially with '-- MSTest.Parallelize.Workers=1' after the dotnet test
// arguments, the settings on the command line override this attribute.
[assembly: Parallelize(Workers = 8, Scope = ExecutionScope.MethodLevel)]

namespace gmdE2eTest;

// Runs once for the whole test assembly, before any test. The same throwaway HOME as
// gmdTest/TestSetup.cs gives its test process, and for the same reason: the fixtures run git
// through gmd.Utils.Cmd, which logs, and the first log line of a process truncates ~/gmd.log.
//
// And more thread pool threads than the default, which parallel tests need. A running test holds
// a pool thread for all of its run, since TmuxSession polls with Thread.Sleep once the fixture's
// first await is behind it, and every Proc.Run it makes needs further pool threads for the
// callbacks that read the output, before WaitForExit can return. The pool starts with one thread
// per core and adds more only slowly, so with more workers than cores every tmux call waited for
// one: six workers on two cores took ten minutes instead of under one, and tests failed on
// their 30 s timeouts.
[TestClass]
public static class TestSetup
{
    const int MinWorkerThreads = 64;

    static TempHome? home;

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext _)
    {
        home = TempHome.Create();
        Environment.SetEnvironmentVariable("HOME", home.Path);

        ThreadPool.GetMinThreads(out var workerThreads, out var ioThreads);
        ThreadPool.SetMinThreads(Math.Max(workerThreads, MinWorkerThreads), ioThreads);
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup() => home?.Dispose();
}

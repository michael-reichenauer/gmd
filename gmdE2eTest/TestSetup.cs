using gmdTest.Fixtures;

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

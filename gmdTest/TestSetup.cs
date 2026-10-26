using gmdTest.Fixtures;

namespace gmdTest;

// Runs once for the whole test assembly, before any test, and gives the test process a throwaway
// $HOME of its own.
//
// Without it, running the suite filled the developer's runtime log. Any test that runs a git
// command goes through gmd.Utils.Cmd, which logs, into ~/gmd.log, which LogFile opened by
// truncating it in the gmds before 1.16. So 'scripts/test' wiped the log that 'scripts/log' exists
// to read, which is exactly when a developer is most likely to be reading it, and would now bury it
// under the suite's lines and rotate it away. The same redirect also covers ~/.gmdconfig, should a test ever reach the service
// that writes it.
//
// This is the same move TmuxSession makes for the gmd it starts, applied to the test process
// itself: gmd anchors all of its state on SpecialFolder.UserProfile with no override, and on
// Unix that resolves $HOME.
//
// Two details that are easy to get wrong here:
//   - The folder has to exist before HOME is set. GetFolderPath returns an empty string for a
//     HOME that does not exist, and Path.Join then yields the relative 'gmd.log', which lands in
//     the working directory instead.
//   - This has to happen before anything logs, since LogFile resolves the path once in its
//     static constructor. [AssemblyInitialize] is early enough; nothing in the suite logs from a
//     static initializer.
//
// Unix only, deliberately: on Windows the user profile folder does not come from HOME, so a test
// run there still writes into the developer's log. Left alone rather than guessed at, since
// Linux/macOS are the development platforms and a Windows session is the rare case of chasing a
// Windows-only bug. Worth knowing if you are in one — the suite's lines are in the log after
// a run. The fix would be a redirect seam in LogFile, i.e. product code changed for a test-only need on the platform that needs it least.
[TestClass]
public static class TestSetup
{
    static TempHome? home;

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext _)
    {
        home = TempHome.Create();
        Environment.SetEnvironmentVariable("HOME", home.Path);
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup() => home?.Dispose();
}

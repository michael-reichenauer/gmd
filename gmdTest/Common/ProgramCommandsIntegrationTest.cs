using System.Text.RegularExpressions;
using gmdTest.Fixtures;

namespace gmdTest.Common;

// The command line commands run with the built binary, as CI runs them: before any UI, so what
// they read through the server runs with no main loop
[TestClass]
[TestCategory("Integration")]
public class ProgramCommandsIntegrationTest
{
    // CI writes the change log of each release from main with 'gmd --updatechangelog v<version>',
    // which reads the repo as the UI does. With Terminal.Gui 2.x, the file monitor's timer asked
    // for the main loop there was not yet, gmd crashed, and the release from main failed.
    [TestMethod]
    public async Task TestUpdateChangeLogWritesTheNewReleaseBeforeAnyUi()
    {
        using var repo = await TempRepo.CreateAsync();
        var t = TempRepo.BaseTime;
        await repo.CommitFileAtAsync("a.txt", "a\n", "Initial", t);
        await repo.GitAsync("tag v1.0.0.0");
        await repo.GitAsync("checkout -q -b dev");
        await repo.CommitFileAtAsync("b.txt", "b\n", "Add b", t.AddMinutes(1));
        await repo.GitAsync("checkout -q main");
        repo.GitAt(["merge", "--no-ff", "-m", "Merge branch 'dev'\n\n- Show b", "dev"], t.AddMinutes(2));
        using var home = TempHome.Create();

        var result = Proc.Run(GmdPath(), ["--updatechangelog", "v1.1.0.0"], repo.Path, Env(home));

        Assert.IsTrue(result.IsOk, $"{result}\n{home.LogTail()}");
        var changeLog = File.ReadAllText(Path.Join(repo.Path, "CHANGELOG.md"));
        // The new release is dated today, which the test cannot pin
        Assert.AreEqual(
            """
            # Change Log for Gmd
            --------------------

            2 releases:

            ## [v1.1.0.0] - <today>
            - Show b

            """,
            Regex.Replace(changeLog, @"\[v1\.1\.0\.0\] - \d{4}-\d\d-\d\d", "[v1.1.0.0] - <today>")
        );
    }

    static Dictionary<string, string> Env(TempHome home) =>
        new()
        {
            ["HOME"] = home.Path,
            ["XDG_CONFIG_HOME"] = Path.Join(home.Path, ".config"),
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["TZ"] = "UTC",
        };

    // The apphost MSBuild copies into the test output, since gmdTest references gmd, an Exe
    static string GmdPath()
    {
        var path = Path.Join(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "gmd.exe" : "gmd");
        Assert.IsTrue(File.Exists(path), $"'{path}' is missing, build the solution first (dotnet build gmd.sln)");
        return path;
    }
}

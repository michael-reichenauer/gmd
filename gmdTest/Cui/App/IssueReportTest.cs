using System.Text.RegularExpressions;
using gmd.Cui.App;
using gmd.Installation;

namespace gmdTest.Cui.App;

// The links of Report a Bug and Suggest an Improvement, which fill the fields of the issue forms in
// .github/ISSUE_TEMPLATE by their ids, so a field renamed there must be renamed here as well
[TestClass]
public class IssueReportTest
{
    static readonly Dictionary<string, string?> NoEnv = [];

    [TestMethod]
    public void TestTheBugReportFillsTheVersionTheSystemAndGit()
    {
        var url = IssueReport.Url(IssueKind.Bug, "1.2.3.4 (abc)", "Ubuntu 24.04, over ssh", "git version 2.43.0");

        Assert.AreEqual(
            "https://github.com/michael-reichenauer/gmd/issues/new?template=bug_report.yml"
                + "&version=1.2.3.4%20%28abc%29"
                + "&system=Ubuntu%2024.04%2C%20over%20ssh"
                + "&git=git%20version%202.43.0",
            url
        );
    }

    [TestMethod]
    public void TestTheSuggestionFillsTheVersionOnly()
    {
        Assert.AreEqual(
            "https://github.com/michael-reichenauer/gmd/issues/new?template=suggestion.yml&version=1.2.3.4",
            IssueReport.Url(IssueKind.Suggestion, "1.2.3.4", "Ubuntu", "git")
        );
    }

    // Every template and field a link names is in the forms, by its id
    [TestMethod]
    public void TestTheFieldsFilledAreInTheForms()
    {
        foreach (var kind in Enum.GetValues<IssueKind>())
        {
            var url = IssueReport.Url(kind, "v", "s", "g");
            var template = Regex.Match(url, @"template=([^&]+)").Groups[1].Value;
            var form = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ISSUE_TEMPLATE", template));
            var ids = Regex.Matches(form, @"^\s*id: (\S+)", RegexOptions.Multiline).Select(m => m.Groups[1].Value);

            var fields = Regex.Matches(url, @"&(\w+)=").Select(m => m.Groups[1].Value).ToList();
            Assert.IsTrue(fields.Count > 0);
            CollectionAssert.IsSubsetOf(fields, ids.ToList(), $"Fields of {template}");
        }
    }

    [TestMethod]
    public void TestTheVersionOfABuildNotCisHasNoCommit()
    {
        Assert.AreEqual(
            "1.15 (1453.671), a dev build",
            IssueReport.VersionText(new Version(1, 15, 1453, 671), "", true, null)
        );
    }

    [TestMethod]
    public void TestWindows11IsNamedSo()
    {
        Assert.AreEqual("Windows 11 (10.0.22631.0)", IssueReport.WindowsName(new Version(10, 0, 22631, 0)));
        Assert.AreEqual("Windows 10.0.19045.0", IssueReport.WindowsName(new Version(10, 0, 19045, 0)));
    }

    [TestMethod]
    public void TestTheVersionSaysHowGmdWasInstalled()
    {
        var version = new Version(1, 15, 1453, 671);

        Assert.AreEqual("1.15 (1453.671) (abc1234)", IssueReport.VersionText(version, "abc1234", false, null));
        Assert.AreEqual(
            "1.15 (1453.671) (abc1234), installed with Homebrew",
            IssueReport.VersionText(version, "abc1234", false, PackageManager.Homebrew)
        );
        Assert.AreEqual(
            "1.15 (1453.671) (abc1234), a dev build",
            IssueReport.VersionText(version, "abc1234", true, null)
        );
    }

    [TestMethod]
    public void TestTheSystemSaysTheTerminalAndWhatIsBetween()
    {
        var env = new Dictionary<string, string?>
        {
            ["TERM_PROGRAM"] = "iTerm.app",
            ["TERM_PROGRAM_VERSION"] = "3.5.0",
            ["TERM"] = "xterm-256color",
            ["SSH_CONNECTION"] = "10.0.0.1 1234 10.0.0.2 22",
            ["TMUX"] = "/tmp/tmux-0/default,1,0",
        };

        Assert.AreEqual(
            "Ubuntu 24.04.1 LTS X64, iTerm.app 3.5.0 (TERM=xterm-256color), over ssh, in tmux, colors dark (auto)",
            System(env)
        );
    }

    [TestMethod]
    public void TestTheSystemOfWindowsTerminal()
    {
        var env = new Dictionary<string, string?> { ["WT_SESSION"] = "f00" };

        Assert.AreEqual("Ubuntu 24.04.1 LTS X64, Windows Terminal, colors dark (auto)", System(env));
    }

    [TestMethod]
    public void TestTheSystemInAContainer()
    {
        var env = new Dictionary<string, string?> { ["TERM"] = "xterm", ["REMOTE_CONTAINERS"] = "true" };
        Assert.AreEqual("Ubuntu 24.04.1 LTS X64, TERM=xterm, in a dev container, colors dark (auto)", System(env));

        var docker = IssueReport.SystemText(
            "Linux",
            "X64",
            NoEnv.GetValueOrDefault,
            p => p == "/.dockerenv",
            "16 (16)"
        );
        Assert.AreEqual("Linux X64, unknown terminal, in a container, colors 16 (16)", docker);
    }

    [TestMethod]
    public void TestTheSystemInWsl()
    {
        var env = new Dictionary<string, string?> { ["TERM"] = "xterm-256color", ["WSL_DISTRO_NAME"] = "Ubuntu" };

        Assert.AreEqual("Ubuntu 24.04.1 LTS X64, TERM=xterm-256color, WSL Ubuntu, colors dark (auto)", System(env));
    }

    [TestMethod]
    public void TestTheColorsSayTheSettingWhenItIsNotThePalette()
    {
        Assert.AreEqual("dark (auto)", IssueReport.ColorsText("dark", "auto"));
        Assert.AreEqual("light", IssueReport.ColorsText("light", "light"));
    }

    [TestMethod]
    public void TestTheLinuxNameIsTheDistributions()
    {
        var osRelease = """
            NAME="Ubuntu"
            VERSION_ID="24.04"
            PRETTY_NAME="Ubuntu 24.04.1 LTS"
            ID=ubuntu
            """;

        Assert.AreEqual("Ubuntu 24.04.1 LTS", IssueReport.LinuxName(osRelease));
        Assert.IsNull(IssueReport.LinuxName("NAME=Linux\n"));
        Assert.IsNull(IssueReport.LinuxName(""));
    }

    static string System(Dictionary<string, string?> env) =>
        IssueReport.SystemText("Ubuntu 24.04.1 LTS", "X64", env.GetValueOrDefault, _ => false, "dark (auto)");
}

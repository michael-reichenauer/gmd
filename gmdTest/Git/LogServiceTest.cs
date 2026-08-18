using System.Globalization;
using gmd.Git.Private;
using gmdTest.Utils;

namespace gmdTest.Git;

[TestClass]
public class LogServiceTest
{
    // Output of: git log --all --date-order -z --pretty="%H|%ai|%ci|%an|%P|%B"
    // Records are NUL separated, fields are '|' separated:
    //   %H id | %ai author time | %ci commit time | %an author | %P parents | %B message
    const string Id1 = "1111111111111111111111111111111111111111";
    const string Id2 = "2222222222222222222222222222222222222222";

    static string GitLogOutput(string message = "Subject line", string parents = Id2) =>
        $"{Id1}|2024-10-15 12:34:56 +0200|2024-10-15 12:35:00 +0200|Alice|{parents}|{message}\x00";

    static async Task<IReadOnlyList<gmd.Git.Commit>> GetLogAsync(string output)
    {
        var log = new LogService(new FakeCmd(output));
        var result = await log.GetLogAsync(100, "/wd");
        Assert.IsTrue(Try(out var commits, out var e, result), $"GetLogAsync failed: {e}");
        return commits;
    }

    // Git always emits dates in the same format regardless of the user's locale, so parsing
    // must be culture invariant. Cultures using a non-Gregorian calendar are the interesting
    // ones: "ar-SA" (Umm al-Qura) used to throw, and "th-TH" (Buddhist) and "fa-IR" (Persian)
    // used to silently parse the year hundreds of years off.
    [TestMethod]
    [DataRow("en-US")]
    [DataRow("sv-SE")]
    [DataRow("de-DE")]
    [DataRow("ar-SA")]
    [DataRow("th-TH")]
    [DataRow("fa-IR")]
    public async Task TestParseTimesIsCultureInvariant(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalDefault = CultureInfo.DefaultThreadCurrentCulture;
        try
        {
            var culture = new CultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            // Also set the default so the parsing done on a thread pool thread uses it
            CultureInfo.DefaultThreadCurrentCulture = culture;

            var commits = await GetLogAsync(GitLogOutput());

            Assert.AreEqual(1, commits.Count);
            // Compared as UTC so the assert does not depend on the machine's time zone
            Assert.AreEqual(
                new DateTime(2024, 10, 15, 10, 34, 56, DateTimeKind.Utc),
                commits[0].AuthorTime.ToUniversalTime(),
                $"AuthorTime wrong for culture '{cultureName}'"
            );
            Assert.AreEqual(
                new DateTime(2024, 10, 15, 10, 35, 0, DateTimeKind.Utc),
                commits[0].CommitTime.ToUniversalTime(),
                $"CommitTime wrong for culture '{cultureName}'"
            );
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.DefaultThreadCurrentCulture = originalDefault;
        }
    }

    [TestMethod]
    public async Task TestParseCommitFields()
    {
        var commits = await GetLogAsync(GitLogOutput());

        Assert.AreEqual(1, commits.Count);
        var c = commits[0];
        Assert.AreEqual(Id1, c.Id);
        Assert.AreEqual(Id1.Sid(), c.Sid);
        Assert.AreEqual("Alice", c.Author);
        Assert.AreEqual("Subject line", c.Subject);
        Assert.AreEqual("Subject line", c.Message);
        CollectionAssert.AreEqual(new[] { Id2 }, c.ParentIds);
    }

    // A commit message may contain '|', which is also the field separator, so the trailing
    // parts must be rejoined into the original message.
    [TestMethod]
    public async Task TestParseMessageContainingFieldSeparator()
    {
        var commits = await GetLogAsync(GitLogOutput("Fix a|b parsing|and more"));

        Assert.AreEqual("Fix a|b parsing|and more", commits[0].Message);
        Assert.AreEqual("Fix a|b parsing|and more", commits[0].Subject);
    }

    // The subject is the first line, the message keeps all lines
    [TestMethod]
    public async Task TestParseMultiLineMessage()
    {
        var commits = await GetLogAsync(GitLogOutput("Subject\n\nBody line 1\nBody line 2"));

        Assert.AreEqual("Subject", commits[0].Subject);
        Assert.AreEqual("Subject\n\nBody line 1\nBody line 2", commits[0].Message);
    }

    // Leading empty lines in a message are skipped, and the subject comes from the first
    // non-empty line
    [TestMethod]
    public async Task TestParseMessageWithLeadingEmptyLines()
    {
        var commits = await GetLogAsync(GitLogOutput("\n\n  \nSubject after empty lines"));

        Assert.AreEqual("Subject after empty lines", commits[0].Subject);
    }

    // The root commit has no parents, %P is then empty
    [TestMethod]
    public async Task TestParseRootCommitHasNoParents()
    {
        var commits = await GetLogAsync(GitLogOutput(parents: ""));

        Assert.AreEqual(0, commits[0].ParentIds.Length);
    }

    [TestMethod]
    public async Task TestParseMergeCommitHasTwoParents()
    {
        var commits = await GetLogAsync(GitLogOutput(parents: $"{Id2} {Id1}"));

        CollectionAssert.AreEqual(new[] { Id2, Id1 }, commits[0].ParentIds);
    }

    [TestMethod]
    public async Task TestParseEmptyLogIsNoCommits()
    {
        var commits = await GetLogAsync("");

        Assert.AreEqual(0, commits.Count);
    }

    // A row with too few fields is an error, not an exception
    [TestMethod]
    public async Task TestParseTooFewFieldsIsError()
    {
        var log = new LogService(new FakeCmd($"{Id1}|2024-10-15 12:34:56 +0200|Alice\x00"));

        var result = await log.GetLogAsync(100, "/wd");

        Assert.IsFalse(Try(out var _, out var _, result), "Expected a parse error");
    }

    [TestMethod]
    public async Task TestGitCommandFailureIsPropagated()
    {
        var log = new LogService(new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: not a git repository")));

        var result = await log.GetLogAsync(100, "/wd");

        Assert.IsFalse(Try(out var _, out var _, result), "Expected the git failure to propagate");
    }

    [TestMethod]
    public async Task TestGetLogPassesMaxCountAndWorkingDirectoryToGit()
    {
        var cmd = new FakeCmd(GitLogOutput());
        var log = new LogService(cmd);

        await log.GetLogAsync(42, "/some/wd");

        Assert.AreEqual(1, cmd.Calls.Count);
        Assert.AreEqual("git", cmd.Calls[0].Path);
        Assert.AreEqual("/some/wd", cmd.Calls[0].WorkingDirectory);
        StringAssert.Contains(cmd.Calls[0].Args, "--max-count=42");
    }
}

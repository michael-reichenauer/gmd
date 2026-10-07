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
        var commits = AssertOk(result);
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
        Assert.AreEqual("Subject after empty lines", commits[0].Message);
    }

    // A line of white space only is empty too, whatever the white space, and the first line that
    // is not keeps its indentation. What is skipped are whole lines only.
    [TestMethod]
    public async Task TestParseMessageSkipsLeadingWhiteSpaceLinesOnly()
    {
        var commits = await GetLogAsync(GitLogOutput("\n \t\n\r\n  Indented subject\n\nBody"));

        Assert.AreEqual("  Indented subject\n\nBody", commits[0].Message);
        Assert.AreEqual("  Indented subject", commits[0].Subject);
    }

    // Trailing white space is trimmed from the message, and from the subject line
    [TestMethod]
    public async Task TestParseMessageTrimsTrailingWhiteSpace()
    {
        var commits = await GetLogAsync(GitLogOutput("Subject \t\nBody  \n\n \n"));

        Assert.AreEqual("Subject \t\nBody", commits[0].Message);
        Assert.AreEqual("Subject", commits[0].Subject);
    }

    // A message of nothing, or of empty lines only, is an empty message and subject
    [TestMethod]
    [DataRow("")]
    [DataRow("\n")]
    [DataRow("\n \n\t")]
    public async Task TestParseEmptyMessage(string message)
    {
        var commits = await GetLogAsync(GitLogOutput(message));

        Assert.AreEqual("", commits[0].Message);
        Assert.AreEqual("", commits[0].Subject);
    }

    // Records are NUL separated, and a record of white space only, e.g. the newline after the last,
    // is no commit. A '|' in a message after its first line is part of the message as well.
    [TestMethod]
    public async Task TestParseSeveralRecords()
    {
        var output =
            $"{Id1}|2024-10-15 12:34:56 +0200|2024-10-15 12:35:00 +0200|Alice|{Id2}|First\n\nWith a | in it\n\x00"
            + "\n\x00"
            + $"{Id2}|2024-10-14 12:34:56 +0200|2024-10-14 12:35:00 +0200|Bob||Second|and more\n\x00\n";

        var commits = await GetLogAsync(output);

        Assert.AreEqual(2, commits.Count);
        Assert.AreEqual("First\n\nWith a | in it", commits[0].Message);
        Assert.AreEqual("First", commits[0].Subject);
        Assert.AreEqual(Id2, commits[1].Id);
        Assert.AreEqual("Bob", commits[1].Author);
        Assert.AreEqual(0, commits[1].ParentIds.Length);
        Assert.AreEqual("Second|and more", commits[1].Message);
        Assert.AreEqual("Second|and more", commits[1].Subject);
    }

    // The parents are space separated, and white space around them is not part of an id
    [TestMethod]
    public async Task TestParseParentsAroundWhiteSpace()
    {
        var commits = await GetLogAsync(GitLogOutput(parents: $" {Id2} {Id1} "));

        CollectionAssert.AreEqual(new[] { Id2, Id1 }, commits[0].ParentIds);
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

        var error = AssertError(result, "Expected a parse error");
        StringAssert.Contains(error.Message, $"{Id1}|2024-10-15 12:34:56 +0200|Alice");
    }

    [TestMethod]
    public async Task TestGitCommandFailureIsPropagated()
    {
        var log = new LogService(new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: not a git repository")));

        var result = await log.GetLogAsync(100, "/wd");

        AssertError(result, "Expected the git failure to propagate");
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

    // The path of a search is what the user typed: a '"' would end the argument, and a Windows
    // user's '\' is a '/' to git. The ids come back one per line.
    [TestMethod]
    public async Task TestTheFileSearchKeepsTheTypedPathInItsArgument()
    {
        var cmd = new FakeCmd($"{Id1}\n{Id2}\n");
        var log = new LogService(cmd);

        var ids = AssertOk(await log.GetIdsChangingFilesAsync("gmd\\Cui \"x", 50, "/wd"));

        CollectionAssert.AreEqual(new[] { Id1, Id2 }, ids.ToArray());
        StringAssert.EndsWith(cmd.Calls[0].Args, "--max-count=50 -- \":(icase)*gmd/Cui x*\"");
    }

    // The ids are given in chunks a Windows command line holds, each walked with everything kept
    // taken away, and a commit two chunks both reach is listed once
    [TestMethod]
    public async Task TestUnreachableCommitsAreReadInChunks()
    {
        var cmd = new FakeCmd(GitLogOutput());
        var ids = Enumerable.Range(0, 900).Select(i => i.ToString("x40")).ToList();

        var commits = AssertOk(await new LogService(cmd).GetUnreachableCommitsAsync(ids, ["5a5a"], "/wd"));

        Assert.AreEqual(3, cmd.Calls.Count);
        StringAssert.StartsWith(cmd.Calls[0].Args, "log --ignore-missing -z --date-order --pretty=");
        StringAssert.EndsWith(cmd.Calls[0].Args, $"{ids[399]} --not --all 5a5a");
        StringAssert.Contains(cmd.Calls[1].Args, $" {ids[400]} ");
        StringAssert.EndsWith(cmd.Calls[2].Args, $"{ids[899]} --not --all 5a5a");
        Assert.AreEqual(1, commits.Count);
    }

    [TestMethod]
    public async Task TestNoUnreachableCommitsWithoutIds()
    {
        var cmd = new FakeCmd(GitLogOutput());

        Assert.AreEqual(0, AssertOk(await new LogService(cmd).GetUnreachableCommitsAsync([], [], "/wd")).Count);
        Assert.AreEqual(0, cmd.Calls.Count);
    }

    // The ids git lists are the commits it still has; the others it passed over
    [TestMethod]
    public async Task TestExistingCommitIds()
    {
        var cmd = new FakeCmd($"{Id1}\n{Id2}\n");

        var existing = AssertOk(await new LogService(cmd).GetExistingCommitIdsAsync([Id1, Id2, "3333"], "/wd"));

        Assert.AreEqual($"rev-list --no-walk --ignore-missing {Id1} {Id2} 3333", cmd.Calls[0].Args);
        CollectionAssert.AreEquivalent(new[] { Id1, Id2 }, existing.ToList());
    }

    [TestMethod]
    public async Task TestNoExistingCommitIdsWithoutIds()
    {
        var cmd = new FakeCmd("");

        Assert.AreEqual(0, AssertOk(await new LogService(cmd).GetExistingCommitIdsAsync([], "/wd")).Count);
        Assert.AreEqual(0, cmd.Calls.Count);
    }
}

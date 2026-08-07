using gmd.Git.Private;
using gmdTest.Utils;

namespace gmdTest.Git;

[TestClass]
public class BlameServiceTest
{
    // Output of: git blame --porcelain <ref> -- <path>
    // Per blamed line: a '<sha> <original line nbr> <final line nbr> [<lines in group>]' header, a
    // key/value block that is emitted only the first time a commit is seen, and then the content
    // line, prefixed with a tab. The tab is written as '→' below and swapped in by Out(), since a
    // literal tab in the source would be at the mercy of the editor and the formatter.
    const string Id1 = "1111111111111111111111111111111111111111";
    const string Id2 = "2222222222222222222222222222222222222222";
    const string Id0 = "0000000000000000000000000000000000000000";

    static string Out(string output) => output.Replace('→', '\t');

    // Line 1 and 3 are from Id1, line 2 from Id2, so Id1 repeats on line 3 with no block at all.
    // Id1 is the root commit ('boundary', no 'previous'), Id2 renamed the file from old.txt.
    const string BlameOutput = """
        1111111111111111111111111111111111111111 1 1 1
        author Alice
        author-mail <alice@example.com>
        author-time 1700000000
        author-tz +0000
        committer Alice
        committer-mail <alice@example.com>
        committer-time 1700000000
        committer-tz +0000
        summary Initial commit
        boundary
        filename file.txt
        →first line
        2222222222222222222222222222222222222222 5 2 1
        author Bob
        author-mail <bob@example.com>
        author-time 1800000000
        author-tz +0000
        committer Bob
        committer-mail <bob@example.com>
        committer-time 1800000000
        committer-tz +0000
        summary Second commit
        previous 1111111111111111111111111111111111111111 old.txt
        filename file.txt
        →second line
        1111111111111111111111111111111111111111 2 3 1
        →third line
        """;

    static async Task<gmd.Git.Blame> GetBlameAsync(string output, string reference = "HEAD")
    {
        var blame = new BlameService(new FakeCmd(Out(output)));
        var result = await blame.GetBlameAsync("file.txt", reference, "/wd");
        Assert.IsTrue(Try(out var b, out var e, result), $"GetBlameAsync failed: {e}");
        return b;
    }

    [TestMethod]
    public async Task TestParseLines()
    {
        var blame = await GetBlameAsync(BlameOutput);

        Assert.AreEqual("file.txt", blame.Path);
        Assert.AreEqual("HEAD", blame.Reference);
        Assert.AreEqual(3, blame.Lines.Count);

        Assert.AreEqual(Id1, blame.Lines[0].CommitId);
        Assert.AreEqual(1, blame.Lines[0].LineNbr);
        Assert.AreEqual(1, blame.Lines[0].OriginalLineNbr);
        Assert.AreEqual("first line", blame.Lines[0].Text);

        Assert.AreEqual(Id2, blame.Lines[1].CommitId);
        Assert.AreEqual(2, blame.Lines[1].LineNbr);
        Assert.AreEqual(5, blame.Lines[1].OriginalLineNbr);
        Assert.AreEqual("second line", blame.Lines[1].Text);
    }

    // The header of a commit already seen has no key/value block, the commit is looked up instead
    [TestMethod]
    public async Task TestRepeatedCommitReusesTheFirstBlock()
    {
        var blame = await GetBlameAsync(BlameOutput);

        Assert.AreEqual(2, blame.CommitById.Count);
        Assert.AreEqual(Id1, blame.Lines[2].CommitId);
        Assert.AreEqual("third line", blame.Lines[2].Text);
        Assert.AreEqual("Alice", blame.CommitById[blame.Lines[2].CommitId].Author);
        Assert.AreEqual("Initial commit", blame.CommitById[blame.Lines[2].CommitId].Subject);
    }

    [TestMethod]
    public async Task TestParseCommitFields()
    {
        var blame = await GetBlameAsync(BlameOutput);

        var c = blame.CommitById[Id2];
        Assert.AreEqual(Id2, c.Id);
        Assert.AreEqual(Id2.Sid(), c.Sid);
        Assert.AreEqual("Bob", c.Author);
        Assert.AreEqual("bob@example.com", c.AuthorMail);
        Assert.AreEqual("Second commit", c.Subject);
        Assert.AreEqual("file.txt", c.Path);
        Assert.IsFalse(c.IsUncommitted);
    }

    // 'author-time' is unix seconds, so no locale or calendar is involved
    [TestMethod]
    public async Task TestParseAuthorTime()
    {
        var blame = await GetBlameAsync(BlameOutput);

        var expected = DateTimeOffset.FromUnixTimeSeconds(1700000000).LocalDateTime;
        Assert.AreEqual(expected, blame.CommitById[Id1].AuthorTime);
    }

    // The 'previous' line is what a drill down to the version before a commit needs, and it also
    // carries the name the file had before a rename
    [TestMethod]
    public async Task TestParsePreviousWithRenamedPath()
    {
        var blame = await GetBlameAsync(BlameOutput);

        Assert.AreEqual(Id1, blame.CommitById[Id2].PreviousId);
        Assert.AreEqual("old.txt", blame.CommitById[Id2].PreviousPath);
    }

    // The root commit is a boundary, it has no previous version to drill down to
    [TestMethod]
    public async Task TestParseBoundaryCommitHasNoPrevious()
    {
        var blame = await GetBlameAsync(BlameOutput);

        Assert.IsTrue(blame.CommitById[Id1].IsBoundary);
        Assert.AreEqual("", blame.CommitById[Id1].PreviousId);
    }

    // Lines not committed yet get the same all '0' sha as Server.Repo.UncommittedId
    [TestMethod]
    public async Task TestParseUncommittedLine()
    {
        var output = $"""
            {Id0} 5 1 1
            author Not Committed Yet
            author-mail <not.committed.yet>
            author-time 1800000000
            author-tz +0000
            committer Not Committed Yet
            committer-mail <not.committed.yet>
            committer-time 1800000000
            committer-tz +0000
            summary Version of file.txt from file.txt
            previous 1111111111111111111111111111111111111111 file.txt
            filename file.txt
            →not committed yet
            """;

        var blame = await GetBlameAsync(output, "");

        Assert.AreEqual(Id0, blame.Lines[0].CommitId);
        Assert.IsTrue(blame.CommitById[Id0].IsUncommitted);
        Assert.AreEqual("Not Committed Yet", blame.CommitById[Id0].Author);
    }

    // A blank line in the file is a bare tab. Tabs within a line are kept verbatim, so a copy of
    // the blamed lines yields the file's own text; expanding them is the view's job.
    [TestMethod]
    public async Task TestParseEmptyAndTabbedContentLines()
    {
        var output = $"""
            {Id1} 1 1 2
            author Alice
            author-time 1700000000
            summary Initial commit
            filename file.txt
            →
            {Id1} 2 2
            →→indented
            """;

        var blame = await GetBlameAsync(output);

        Assert.AreEqual(2, blame.Lines.Count);
        Assert.AreEqual("", blame.Lines[0].Text);
        Assert.AreEqual("\tindented", blame.Lines[1].Text);
    }

    // Cmd trims the end of the whole git output, so when the file's last line is empty its content
    // line is gone entirely. That must parse as an empty line, not as a failure.
    [TestMethod]
    public async Task TestLastContentLineTrimmedAwayIsParsedAsEmpty()
    {
        var output = $"""
            {Id1} 1 1 2
            author Alice
            author-time 1700000000
            summary Initial commit
            filename file.txt
            →first line
            {Id1} 2 2
            """;

        var blame = await GetBlameAsync(output);

        Assert.AreEqual(2, blame.Lines.Count);
        Assert.AreEqual("first line", blame.Lines[0].Text);
        Assert.AreEqual("", blame.Lines[1].Text);
    }

    [TestMethod]
    public async Task TestEmptyFileIsEmptyBlame()
    {
        var blame = await GetBlameAsync("");

        Assert.AreEqual(0, blame.Lines.Count);
        Assert.AreEqual(0, blame.CommitById.Count);
    }

    [TestMethod]
    public async Task TestParseInvalidHeaderIsError()
    {
        var blame = new BlameService(new FakeCmd(Out("not a blame header\n→some line")));
        var result = await blame.GetBlameAsync("file.txt", "HEAD", "/wd");
        Assert.IsFalse(Try(out var _, out var _, result), "Expected a parse error");
    }

    [TestMethod]
    public async Task TestGitCommandFailureIsPropagated()
    {
        var blame = new BlameService(new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: no such path")));
        var result = await blame.GetBlameAsync("file.txt", "HEAD", "/wd");
        Assert.IsFalse(Try(out var _, out var _, result), "Expected the git failure to propagate");
    }

    [TestMethod]
    public async Task TestGetBlamePassesReferenceAndPathToGit()
    {
        var cmd = new FakeCmd(Out(BlameOutput));
        var blame = new BlameService(cmd);

        await blame.GetBlameAsync("some dir/file.txt", "abc123", "/some/wd");

        Assert.AreEqual(1, cmd.Calls.Count);
        Assert.AreEqual("git", cmd.Calls[0].Path);
        Assert.AreEqual("/some/wd", cmd.Calls[0].WorkingDirectory);
        Assert.AreEqual("blame --porcelain abc123 -- \"some dir/file.txt\"", cmd.Calls[0].Args);
    }

    // A repo whose 'blame.ignoreRevsFile' names a file that is not there makes git blame fail
    // outright. Gmd honors that config, but retries once with it cleared rather than losing the
    // whole view over a missing text file.
    [TestMethod]
    public async Task TestMissingIgnoreRevsFileIsRetriedWithoutIt()
    {
        var cmd = new FakeCmd(
            (_, args, _) =>
                args.StartsWith("-c blame.ignoreRevsFile=")
                    ? FakeCmd.Ok(Out(BlameOutput))
                    : FakeCmd.Fail("fatal: could not open object name list: .git-blame-ignore-revs")
        );
        var blame = new BlameService(cmd);

        var result = await blame.GetBlameAsync("file.txt", "HEAD", "/wd");

        Assert.IsTrue(Try(out var b, out var e, result), $"Expected the retry to succeed: {e}");
        Assert.AreEqual(3, b.Lines.Count);
        Assert.AreEqual(2, cmd.Calls.Count);
        Assert.AreEqual("-c blame.ignoreRevsFile= blame --porcelain HEAD -- \"file.txt\"", cmd.Calls[1].Args);
    }

    // Any other failure is not retried, it is just propagated
    [TestMethod]
    public async Task TestOtherFailureIsNotRetried()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: no such path"));
        var blame = new BlameService(cmd);

        await blame.GetBlameAsync("file.txt", "HEAD", "/wd");

        Assert.AreEqual(1, cmd.Calls.Count);
    }

    // An empty reference blames the working tree, i.e. including the uncommitted lines
    [TestMethod]
    public async Task TestGetBlameWithoutReferenceOmitsIt()
    {
        var cmd = new FakeCmd(Out(BlameOutput));
        var blame = new BlameService(cmd);

        await blame.GetBlameAsync("file.txt", "", "/wd");

        Assert.AreEqual("blame --porcelain -- \"file.txt\"", cmd.Calls[0].Args);
    }
}

using gmd.Cui.Diff;
using gmd.Server;
using DiffOptions = gmd.Git.DiffOptions;

namespace gmdTest.Cui.Diff;

[TestClass]
public class DiffWhitespaceTest
{
    static readonly IReadOnlyList<SectionDiff> SomeLines =
    [
        new SectionDiff("1,1 +1,1", 1, 1, 1, 1, [new LineDiff(DiffMode.DiffAdded, "x")]),
    ];

    static FileDiff File(string path, bool hasLines = true) =>
        new(path, path, false, !hasLines, DiffMode.DiffModified, hasLines ? SomeLines : []);

    static CommitDiff Diff(string id, params FileDiff[] files) =>
        new(id, "Author", DateTime.MinValue, "Message", files);

    // Each file, and how many sections it has or that its changes are whitespace alone
    static string Files(CommitDiff diff) =>
        string.Join(
            ", ",
            diff.FileDiffs.Select(f =>
                f.IsWhitespaceOnly ? $"{f.PathAfter} whitespace" : $"{f.PathAfter} {f.SectionDiffs.Count}"
            )
        );

    // The files git left out are put back in their place with no lines, and a file that had none to
    // begin with, a binary file, is not taken for one of them
    [TestMethod]
    public void TestTheFilesOfWhitespaceAloneArePutBack()
    {
        var shown = Diff("", File("a.txt"), File("c.txt"));
        var all = Diff("", File("a.txt"), File("b.txt"), File("c.txt"), File("d.bin", hasLines: false));

        var diff = DiffWhitespace.WithWhitespaceOnlyFiles([shown], [all]).Single();

        Assert.AreEqual("a.txt 1, b.txt whitespace, c.txt 1, d.bin 0", Files(diff));
        Assert.AreSame(shown.FileDiffs[0], diff.FileDiffs[0], "As read with whitespace left out");
    }

    // A file history is a diff per commit, matched by id: a commit whose change was whitespace alone
    // has no file with whitespace left out, or is not there at all
    [TestMethod]
    public void TestTheCommitsOfAFileHistoryAreMatchedById()
    {
        CommitDiff[] shown = [Diff("c3", File("a.txt")), Diff("c2")];
        CommitDiff[] all = [Diff("c3", File("a.txt")), Diff("c2", File("a.txt")), Diff("c1", File("a.txt"))];

        var diffs = DiffWhitespace.WithWhitespaceOnlyFiles(shown, all);

        Assert.AreEqual(
            "c3: a.txt 1 | c2: a.txt whitespace | c1: a.txt whitespace",
            string.Join(" | ", diffs.Select(d => $"{d.Id}: {Files(d)}"))
        );
    }

    // With whitespace left out a diff is read twice, the second time with whitespace and no context,
    // for which files it has; any other diff is read once, as asked
    [TestMethod]
    public async Task TestADiffWithWhitespaceLeftOutIsReadAgainForItsFiles()
    {
        List<DiffOptions> reads = [];
        var source = new DiffSource(options =>
        {
            reads.Add(options);
            CommitDiff[] read = options.IsIgnoreWhitespace
                ? [Diff("", File("a.txt"))]
                : [Diff("", File("a.txt"), File("b.txt"))];
            return Task.FromResult<Result<CommitDiff[]>>(read);
        });

        var hidden = AssertOk(await source.ReadAsync(new DiffOptions(6, IsIgnoreWhitespace: true)));
        var shown = AssertOk(await source.ReadAsync(15));

        Assert.AreEqual("a.txt 1, b.txt whitespace", Files(hidden.Single()));
        Assert.AreEqual("a.txt 1, b.txt 1", Files(shown.Single()));
        CollectionAssert.AreEqual(new DiffOptions[] { new(6, true), new(0), new(15) }, reads);
    }
}

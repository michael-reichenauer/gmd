using gmd.Cui;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui;

// The rows of the lost commits dialog, as text, and what is said of the selected one
[TestClass]
public class LostWorkRowsTest
{
    static readonly DateTime Time = new(2024, 10, 15, 12, 3, 0);

    static LostWork Work(
        LostBy lostBy,
        string branch = "feature",
        bool isRewritten = false,
        int commits = 2,
        string subject = "Feature work"
    ) =>
        new LostWork(
            RepoBuilder.Sha("f2"),
            Enumerable.Range(0, commits).Select(i => RepoBuilder.Sha($"f{i}")).ToList(),
            RepoBuilder.Sha("f1"),
            Time,
            subject,
            "Alice",
            branch,
            lostBy,
            isRewritten
        );

    [TestMethod]
    public void TestHeaderAndRow()
    {
        Assert.AreEqual(
            "Time              Branch              Commits  Lost by   Subject             ",
            LostWorkRows.Header(LostWorkRows.MinWidth).ToString()
        );
        Assert.AreEqual(
            "2024-10-15 12:03  feature                   2  deleted   Feature work        ",
            LostWorkRows.Row(Work(LostBy.Deleted), LostWorkRows.MinWidth).ToString()
        );
    }

    // A line with no branch the reflog says it was made on, e.g. one on a detached HEAD
    [TestMethod]
    public void TestRowWithNoBranch()
    {
        Assert.AreEqual(
            "2024-10-15 12:03  -                         1  detached  Feature work        ",
            LostWorkRows.Row(Work(LostBy.Detached, branch: "", commits: 1), LostWorkRows.MinWidth).ToString()
        );
    }

    [TestMethod]
    [DataRow(LostBy.Deleted, false, "f20000 by Alice: made on 'feature', which was deleted")]
    [DataRow(LostBy.Detached, false, "f20000 by Alice: made on a detached HEAD, left when a branch was checked out")]
    [DataRow(LostBy.Reset, false, "f20000 by Alice: left behind on 'feature' by a reset")]
    [DataRow(LostBy.Rebase, false, "f20000 by Alice: replaced on 'feature' by a rebase")]
    [DataRow(LostBy.Amend, false, "f20000 by Alice: replaced on 'feature' by an amend")]
    [DataRow(LostBy.Other, false, "f20000 by Alice")]
    [DataRow(
        LostBy.Amend,
        true,
        "f20000 by Alice: an older version of commits still there, as an amend or a rebase leaves"
    )]
    public void TestReason(LostBy lostBy, bool isRewritten, string reason)
    {
        Assert.AreEqual(reason, LostWorkRows.Reason(Work(lostBy, isRewritten: isRewritten)));
    }
}

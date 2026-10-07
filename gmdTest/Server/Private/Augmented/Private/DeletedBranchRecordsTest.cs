using gmd.Common;
using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// The records of the branches gmd deleted: how a delete joins them, and which can still be restored
[TestClass]
public class DeletedBranchRecordsTest
{
    static readonly DateTime Time = new(2024, 10, 15, 12, 3, 0);

    static string Id(string name) => RepoBuilder.Sha(name);

    static RecordedDelete Local(string name, string tip, string remoteName = "", string subject = "Work") =>
        new()
        {
            Name = name,
            TipId = Id(tip),
            RemoteName = remoteName,
            Subject = subject,
            Time = Time,
        };

    static RecordedDelete Remote(string name, string tip, string subject = "Pushed work") =>
        new()
        {
            Name = name,
            RemoteName = $"origin/{name}",
            RemoteTipId = Id(tip),
            Subject = subject,
            Time = Time,
        };

    static string Of(RecordedDelete r) =>
        string.Join(
            ' ',
            new[] { r.Name, r.TipId.Sid(), r.RemoteName, r.RemoteTipId.Sid(), r.Subject }.Where(f => f != "")
        );

    // A branch deleted on both sides is recorded once per side, origin's first, and is one record
    [TestMethod]
    public void TestTheTwoSidesOfADeleteAreJoined()
    {
        List<RecordedDelete> records = [];

        DeletedBranchRecords.Add(records, Remote("dev", "b1"));
        DeletedBranchRecords.Add(records, Local("dev", "a2", "origin/dev"));

        Assert.AreEqual("dev a20000 origin/dev b10000 Work", string.Join("\n", records.Select(Of)));
    }

    // A local branch deleted and later its remote branch: restored as one, with the local tip's subject
    [TestMethod]
    public void TestALaterDeleteOfTheOtherSideIsJoined()
    {
        List<RecordedDelete> records = [];

        DeletedBranchRecords.Add(records, Local("dev", "a2", "origin/dev"));
        DeletedBranchRecords.Add(records, Remote("dev", "b1"));

        Assert.AreEqual("dev a20000 origin/dev b10000 Work", string.Join("\n", records.Select(Of)));
    }

    // A branch deleted again, after it was made again, is where it was the last time, and the newest
    [TestMethod]
    public void TestADeleteAgainReplacesTheOlderAndIsTheNewest()
    {
        List<RecordedDelete> records = [];

        DeletedBranchRecords.Add(records, Local("dev", "a1"));
        DeletedBranchRecords.Add(records, Local("fix", "f1"));
        DeletedBranchRecords.Add(records, Local("dev", "a2", subject: "Later work"));

        Assert.AreEqual(
            """
            dev a20000 Later work
            fix f10000 Work
            """,
            string.Join("\n", records.Select(Of))
        );
    }

    [TestMethod]
    public void TestOnlyTheNewestAreKept()
    {
        List<RecordedDelete> records = [];

        for (var i = 0; i < DeletedBranchRecords.MaxCount + 3; i++)
        {
            DeletedBranchRecords.Add(records, Local($"b{i}", "a1"));
        }

        Assert.AreEqual(DeletedBranchRecords.MaxCount, records.Count);
        Assert.AreEqual($"b{DeletedBranchRecords.MaxCount + 2}", records[0].Name);
        Assert.AreEqual("b3", records[^1].Name);
    }

    [TestMethod]
    public void TestTipIds()
    {
        List<RecordedDelete> records = [Local("dev", "a2"), Remote("fix", "b1")];
        records[0].RemoteTipId = Id("b1");

        CollectionAssert.AreEqual(new[] { Id("a2"), Id("b1") }, DeletedBranchRecords.TipIds(records).ToList());
    }

    // main with origin/main, and a local branch 'back' and a remote branch 'origin/pushed', which are
    // what a record collides with once a branch of its name is there again
    static Task<Repo> RepoAsync() =>
        new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .LocalBranch("back", "c1")
            .RemoteBranch("origin/pushed", "c1")
            .ViewRepoAsync();

    static readonly IReadOnlySet<string> AllExist = new HashSet<string> { Id("a2"), Id("b1") };

    [TestMethod]
    public async Task TestBothSidesAreRestorable()
    {
        var record = Local("dev", "a2", "origin/dev");
        record.RemoteTipId = Id("b1");

        var deleted = DeletedBranchRecords.Restorable(record, await RepoAsync(), AllExist);

        Assert.AreEqual(new DeletedBranch("dev", Id("a2"), "origin/dev", Id("b1"), "Work", Time), deleted);
    }

    // A side whose name a branch has again would collide with it, and a local branch of the name of a
    // remote branch, or the other way round, is no collision
    [TestMethod]
    public async Task TestASideWhoseNameIsBackIsNotRestorable()
    {
        var repo = await RepoAsync();
        var localBack = Local("back", "a2", "origin/back");
        localBack.RemoteTipId = Id("b1");
        var remoteBack = Local("pushed", "a2", "origin/pushed");
        remoteBack.RemoteTipId = Id("b1");

        Assert.AreEqual(
            new DeletedBranch("back", "", "origin/back", Id("b1"), "Work", Time),
            DeletedBranchRecords.Restorable(localBack, repo, AllExist)
        );
        Assert.AreEqual(
            new DeletedBranch("pushed", Id("a2"), "origin/pushed", "", "Work", Time),
            DeletedBranchRecords.Restorable(remoteBack, repo, AllExist)
        );
        Assert.IsNull(DeletedBranchRecords.Restorable(Local("back", "a2"), repo, AllExist));
    }

    // A tip git no longer has, which a gc pruned, cannot be restored
    [TestMethod]
    public async Task TestASideWhoseTipIsGoneIsNotRestorable()
    {
        var record = Local("dev", "a2", "origin/dev");
        record.RemoteTipId = Id("b1");
        var repo = await RepoAsync();

        Assert.AreEqual(
            new DeletedBranch("dev", "", "origin/dev", Id("b1"), "Work", Time),
            DeletedBranchRecords.Restorable(record, repo, new HashSet<string> { Id("b1") })
        );
        Assert.IsNull(DeletedBranchRecords.Restorable(record, repo, new HashSet<string>()));
    }
}

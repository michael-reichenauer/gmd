using gmd.Common;
using gmd.Server;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private.Augmented.Private;

// The records of the branches gmd deleted: how a delete joins them, which can still be restored, and
// forgetting the sides that are back
[TestClass]
public class DeletedBranchRecordsTest
{
    static readonly DateTime Time = new(2024, 10, 15, 12, 3, 0);

    static string Id(string name) => RepoBuilder.Sha(name);

    // A local branch deleted alone, which tracked remoteName, kept, or nothing
    static RecordedDelete Local(string name, string tip, string remoteName = "", string subject = "Work") =>
        new()
        {
            Name = name,
            TipId = Id(tip),
            RemoteName = remoteName,
            Subject = subject,
            Time = Time,
        };

    // 'origin/<name>' deleted alone, which the local branch localName tracked, kept, or none did
    static RecordedDelete Remote(string name, string tip, string localName = "") =>
        new()
        {
            Name = localName,
            RemoteName = $"origin/{name}",
            RemoteTipId = Id(tip),
            RemoteSubject = "Pushed work",
            Time = Time,
        };

    // Both sides deleted at once, as DeleteBranchAsync records it
    static RecordedDelete Both(string name, string tip, string remoteTip)
    {
        var both = Local(name, tip, $"origin/{name}");
        both.RemoteTipId = Id(remoteTip);
        both.RemoteSubject = "Pushed work";
        return both;
    }

    // The fields, '-' for an empty one: name, tip, subject, remote name, remote tip, remote subject
    static string Of(IEnumerable<RecordedDelete> records) =>
        string.Join(
            "\n",
            records.Select(r =>
                string.Join(
                    ' ',
                    new[]
                    {
                        r.Name,
                        r.TipId.Sid(),
                        r.Subject,
                        r.RemoteName,
                        r.RemoteTipId.Sid(),
                        r.RemoteSubject,
                    }.Select(f => f == "" ? "-" : f)
                )
            )
        );

    // A branch deleted on both sides is recorded as origin's is deleted, and then as the local one is,
    // which takes its place
    [TestMethod]
    public void TestTheTwoSidesOfADeleteAreOneRecord()
    {
        List<RecordedDelete> records = [];

        DeletedBranchRecords.Add(records, Remote("dev", "b1", "dev"));
        DeletedBranchRecords.Add(records, Both("dev", "a2", "b1"));

        Assert.AreEqual("dev a20000 Work origin/dev b10000 Pushed work", Of(records));
    }

    // A local branch deleted and later its remote branch, which no local branch tracks any more
    [TestMethod]
    public void TestALaterDeleteOfOriginsIsJoinedWithTheLocalBranchThatTrackedIt()
    {
        List<RecordedDelete> records = [];

        DeletedBranchRecords.Add(records, Local("dev", "a2", "origin/dev"));
        DeletedBranchRecords.Add(records, Remote("dev", "b1"));

        Assert.AreEqual("dev a20000 Work origin/dev b10000 Pushed work", Of(records));
    }

    // A remote branch deleted and later the local branch that tracked it, which then tracks nothing,
    // since git reports its remote branch as gone. A local branch of another name is joined as well.
    [TestMethod]
    public void TestALaterDeleteOfTheLocalBranchIsJoinedWithOriginsItTracked()
    {
        List<RecordedDelete> records = [];

        DeletedBranchRecords.Add(records, Remote("feature", "b1", "feat"));
        DeletedBranchRecords.Add(records, Local("feat", "a2"));

        Assert.AreEqual("feat a20000 Work origin/feature b10000 Pushed work", Of(records));
    }

    // Two deletes of a name that are not of one pair are two records: a local 'tmp' that tracked
    // nothing and an 'origin/tmp' no local branch tracked, and a local 'feat' that tracked
    // 'origin/feature' and an 'origin/feat'. Joined, a restore would push the one's commit as the other.
    [TestMethod]
    public void TestDeletesOfANameThatAreNotOfOnePairAreNotJoined()
    {
        List<RecordedDelete> records = [];

        DeletedBranchRecords.Add(records, Remote("tmp", "b1"));
        DeletedBranchRecords.Add(records, Local("tmp", "a2"));
        DeletedBranchRecords.Add(records, Remote("feat", "b2"));
        DeletedBranchRecords.Add(records, Local("feat", "a3", "origin/feature"));

        Assert.AreEqual(
            """
            feat a30000 Work origin/feature - -
            - - - origin/feat b20000 Pushed work
            tmp a20000 Work - - -
            - - - origin/tmp b10000 Pushed work
            """,
            Of(records)
        );
    }

    // A remote branch another local branch tracks now is not joined with the one that tracked it once
    [TestMethod]
    public void TestARemoteBranchTrackedByAnotherLocalBranchIsNotJoined()
    {
        List<RecordedDelete> records = [];

        DeletedBranchRecords.Add(records, Local("dev", "a2", "origin/dev"));
        DeletedBranchRecords.Add(records, Remote("dev", "b1", "dev2"));

        Assert.AreEqual(
            """
            dev2 - - origin/dev b10000 Pushed work
            dev a20000 Work origin/dev - -
            """,
            Of(records)
        );
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
            dev a20000 Later work - - -
            fix f10000 Work - - -
            """,
            Of(records)
        );
    }

    // A local branch that tracked a remote branch shows that branch is there: pushed again since gmd
    // deleted it, so the older record of it is nothing to restore
    [TestMethod]
    public void TestADeleteShowingARemoteBranchIsBackReplacesItsOlderSide()
    {
        List<RecordedDelete> records = [];

        DeletedBranchRecords.Add(records, Both("dev", "a1", "b1"));
        DeletedBranchRecords.Add(records, Remote("fix", "b2"));
        DeletedBranchRecords.Add(records, Local("fix", "a2", "origin/fix"));

        Assert.AreEqual(
            """
            fix a20000 Work origin/fix - -
            dev a10000 Work origin/dev b10000 Pushed work
            """,
            Of(records)
        );

        DeletedBranchRecords.Add(records, Remote("dev", "b3"));

        Assert.AreEqual(
            """
            dev a10000 Work origin/dev b30000 Pushed work
            fix a20000 Work origin/fix - -
            """,
            Of(records),
            "Origin's side replaced, and the local side joined with it, since nothing tracks it now"
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
        var deleted = DeletedBranchRecords.Restorable(Both("dev", "a2", "b1"), await RepoAsync(), AllExist);

        Assert.AreEqual(new DeletedBranch("dev", Id("a2"), "origin/dev", Id("b1"), "Work", Time), deleted);
    }

    // A remote branch no local branch tracked has the name its local branch would have, and the
    // subject of its own tip
    [TestMethod]
    public async Task TestARemoteBranchAloneIsNamedAsItsLocalBranchWouldBe()
    {
        var deleted = DeletedBranchRecords.Restorable(Remote("fix", "b1"), await RepoAsync(), AllExist);

        Assert.AreEqual(new DeletedBranch("fix", "", "origin/fix", Id("b1"), "Pushed work", Time), deleted);
    }

    // A side whose name a branch has again would collide with it, and a local branch of the name of a
    // remote branch, or the other way round, is no collision. With only origin's to restore, the row
    // has the subject of origin's tip.
    [TestMethod]
    public async Task TestASideWhoseNameIsBackIsNotRestorable()
    {
        var repo = await RepoAsync();

        Assert.AreEqual(
            new DeletedBranch("back", "", "origin/back", Id("b1"), "Pushed work", Time),
            DeletedBranchRecords.Restorable(Both("back", "a2", "b1"), repo, AllExist)
        );
        Assert.AreEqual(
            new DeletedBranch("pushed", Id("a2"), "origin/pushed", "", "Work", Time),
            DeletedBranchRecords.Restorable(Both("pushed", "a2", "b1"), repo, AllExist)
        );
        Assert.IsNull(DeletedBranchRecords.Restorable(Local("back", "a2"), repo, AllExist));
    }

    // A tip git no longer has, which a gc pruned, cannot be restored
    [TestMethod]
    public async Task TestASideWhoseTipIsGoneIsNotRestorable()
    {
        var record = Both("dev", "a2", "b1");
        var repo = await RepoAsync();

        Assert.AreEqual(
            new DeletedBranch("dev", "", "origin/dev", Id("b1"), "Pushed work", Time),
            DeletedBranchRecords.Restorable(record, repo, new HashSet<string> { Id("b1") })
        );
        Assert.IsNull(DeletedBranchRecords.Restorable(record, repo, new HashSet<string>()));
    }

    // The sides Restorable left out are the ones to forget
    [TestMethod]
    public async Task TestUnrestorable()
    {
        var repo = await RepoAsync();
        var record = Both("back", "a2", "b1");

        var unrestorable = DeletedBranchRecords.Unrestorable(
            record,
            DeletedBranchRecords.Restorable(record, repo, AllExist)
        );

        Assert.AreEqual("back a20000 - origin/back - -", Of([unrestorable!]));
        Assert.IsNull(
            DeletedBranchRecords.Unrestorable(record, new DeletedBranch("back", Id("a2"), "", Id("b1"), "", Time))
        );
        Assert.AreEqual("back a20000 - origin/back b10000 -", Of([DeletedBranchRecords.Unrestorable(record, null)!]));
    }

    // A side restored is forgotten, and the record with it once neither side is left, so a later
    // delete of the name is not joined with a side that is back. A record of another branch of the
    // name, or of the same branch at another tip, is left as it is.
    [TestMethod]
    public void TestRestoredSidesAreForgotten()
    {
        List<RecordedDelete> records = [Both("dev", "a2", "b1"), Local("fix", "a2"), Remote("dev2", "b1")];
        var deleted = new DeletedBranch("dev", Id("a2"), "origin/dev", Id("b1"), "Work", Time);

        DeletedBranchRecords.Forget(records, DeletedBranchRecords.Restored(deleted, true, false));

        Assert.AreEqual(
            """
            dev - - origin/dev b10000 Pushed work
            fix a20000 Work - - -
            - - - origin/dev2 b10000 Pushed work
            """,
            Of(records)
        );

        DeletedBranchRecords.Forget(records, DeletedBranchRecords.Restored(deleted, false, true));

        Assert.AreEqual(
            """
            fix a20000 Work - - -
            - - - origin/dev2 b10000 Pushed work
            """,
            Of(records)
        );
    }
}

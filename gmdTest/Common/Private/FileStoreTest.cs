using gmd.Common;
using gmd.Common.Private;

namespace gmdTest.Common.Private;

// The json files gmd keeps its config in, as several gmd instances share them. Each FileStore here
// stands for one running gmd, since the store is a single instance per process.
[TestClass]
public class FileStoreTest
{
    string root = "";
    string path = "";

    [TestInitialize]
    public void Init()
    {
        root = Path.Join(Path.GetTempPath(), $"gmdTest-filestore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        path = Path.Join(root, ".gmdconfig");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    // Two instances that have both read the file each change a part of it: the second write keeps
    // the first one's change, where it used to write its own stale copy back over it
    [TestMethod]
    public void TestTwoInstancesKeepEachOthersChanges()
    {
        var first = new FileStore();
        var second = new FileStore();
        first.Get<RepoConfig>(path);
        second.Get<RepoConfig>(path);

        first.Set<RepoConfig>(path, c => c.Branches = ["dev"]);
        second.Set<RepoConfig>(path, c => c.BranchColors["main"] = 3);

        var stored = new FileStore().Get<RepoConfig>(path);
        CollectionAssert.AreEqual(new[] { "dev" }, stored.Branches);
        Assert.AreEqual(3, stored.BranchColors["main"]);
        Assert.AreEqual(3, second.Get<RepoConfig>(path).BranchColors["main"], "The writer's cache is what it wrote");
    }

    // A file cut short, e.g. by a crash in the middle of a write, or emptied, used to stop gmd on
    // every start until it was deleted by hand. It is set aside as it was and started anew.
    [TestMethod]
    [DataRow("{\"Branches\": [\"dev\"")]
    [DataRow("")]
    [DataRow("null")]
    public void TestAnUnreadableFileIsSetAsideAndStartedAnew(string text)
    {
        File.WriteAllText(path, text);

        var config = new FileStore().Get<RepoConfig>(path);

        Assert.AreEqual(0, config.Branches.Count);
        Assert.AreEqual(text, File.ReadAllText(path + FileStore.UnreadableSuffix), "Kept, for the user to look at");
        Assert.AreEqual(0, new FileStore().Get<RepoConfig>(path).Branches.Count, "A file that reads again");
    }

    // A change to an unreadable file is a change to a new one, rather than lost or fatal
    [TestMethod]
    public void TestASetOnAnUnreadableFileStartsAnew()
    {
        File.WriteAllText(path, "{");

        new FileStore().Set<RepoConfig>(path, c => c.Branches = ["dev"]);

        CollectionAssert.AreEqual(new[] { "dev" }, new FileStore().Get<RepoConfig>(path).Branches);
    }

    // The file is written beside itself and renamed over, so nothing but the file is left behind
    [TestMethod]
    public void TestAWriteLeavesOnlyTheFile()
    {
        var store = new FileStore();

        store.Set<RepoConfig>(path, c => c.Branches = ["dev"]);
        store.Set<RepoConfig>(path, c => c.Branches = ["dev", "main"]);

        CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(root));
        CollectionAssert.AreEqual(new[] { "dev", "main" }, new FileStore().Get<RepoConfig>(path).Branches);
    }
}

using gmd.Common;
using gmd.Common.Private;

namespace gmdTest.Common;

// The single Config instance over the real file store, on a file of the test's own rather than
// '~/.gmdconfig'
[TestClass]
public class ConfigServiceTest
{
    string root = "";
    string path = "";

    [TestInitialize]
    public void Init()
    {
        root = Path.Join(Path.GetTempPath(), $"gmdTest-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        path = Path.Join(root, ".gmdconfig");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    // The values are there before anything sets one: they used to be loaded only when the UI was
    // ready, or when something was set, so 'gmd --update' read the defaults of what it checked first
    [TestMethod]
    public void TestConfigHoldsTheStoredValuesFromTheStart()
    {
        new FileStore().Set<Config>(
            path,
            c =>
            {
                c.CheckUpdates = false;
                c.AllowPreview = true;
                c.RecentFolders = ["/repo"];
            }
        );

        var config = new Config(new ConfigService(new RedirectedStore(path)));

        Assert.IsFalse(config.CheckUpdates);
        Assert.IsTrue(config.AllowPreview);
        CollectionAssert.AreEqual(new[] { "/repo" }, config.RecentFolders);
    }

    // A change is in the config at once and in the file, and the config also gets what another gmd
    // instance stored meanwhile, since a change is made to the file as it is now
    [TestMethod]
    public void TestSetChangesTheConfigAndTheFile()
    {
        var config = new Config(new ConfigService(new RedirectedStore(path)));
        new FileStore().Set<Config>(path, c => c.SpellWords = ["gmd"]); // Another instance

        config.Set(c => c.ShowKeyHints = false);

        Assert.IsFalse(config.ShowKeyHints);
        CollectionAssert.AreEqual(new[] { "gmd" }, config.SpellWords);
        var stored = new FileStore().Get<Config>(path);
        Assert.IsFalse(stored.ShowKeyHints);
        CollectionAssert.AreEqual(new[] { "gmd" }, stored.SpellWords);
    }

    // The real store, with every path made the test's file
    class RedirectedStore : IFileStore
    {
        readonly FileStore store = new();
        readonly string path;

        public RedirectedStore(string path) => this.path = path;

        public T Get<T>(string _) => store.Get<T>(path);

        public T Set<T>(string _, Action<T> set) => store.Set(path, set);
    }
}

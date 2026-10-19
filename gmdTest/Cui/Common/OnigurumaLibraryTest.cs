using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// The native library a release has embedded, written into a folder of the user's own to be loaded
[TestClass]
public class OnigurumaLibraryTest
{
    string folder = "";

    [TestInitialize]
    public void Init() => folder = Path.Join(Path.GetTempPath(), $"gmdTest-oniguruma-{Guid.NewGuid():N}", "gmd");

    [TestCleanup]
    public void Cleanup() => Directory.Delete(Path.GetDirectoryName(folder)!, true);

    // Named by the hash of what it is, so a gmd of another version with the same library uses the same
    // file, and written once
    [TestMethod]
    public void TestTheLibraryIsWrittenOnceByItsHash()
    {
        var path = OnigurumaLibrary.Write([1, 2, 3], folder, "libonigwrap.so")!;

        StringAssert.Matches(Path.GetFileName(path), new(@"^[0-9a-f]{16}-libonigwrap\.so$"));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
        var written = File.GetLastWriteTimeUtc(path);
        File.SetLastWriteTimeUtc(path, written.AddHours(-1));

        Assert.AreEqual(path, OnigurumaLibrary.Write([1, 2, 3], folder, "libonigwrap.so"));
        Assert.AreEqual(written.AddHours(-1), File.GetLastWriteTimeUtc(path), "Not written again");
        Assert.AreEqual(1, Directory.GetFiles(folder).Length, "Nothing left beside it");
    }

    // A file of that name that is not what it should be is written anew rather than loaded
    [TestMethod]
    public void TestAFileThatIsNotTheLibraryIsReplaced()
    {
        var path = OnigurumaLibrary.Write([1, 2, 3], folder, "libonigwrap.so")!;
        File.WriteAllBytes(path, [6, 6, 6]);

        Assert.AreEqual(path, OnigurumaLibrary.Write([1, 2, 3], folder, "libonigwrap.so"));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
    }

    // The folder is the user's alone, since a library put there by someone else would be run as the user
    [TestMethod]
    public void TestTheFolderIsTheUsersAlone()
    {
        // Returned from too, though Inconclusive throws, for the platform analyzer to see that what
        // follows is not run on Windows (CA1416)
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows keeps the user's own folders the user's alone");
            return;
        }

        OnigurumaLibrary.Write([1, 2, 3], folder, "libonigwrap.so");

        Assert.AreEqual(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(folder)
        );
    }

    // Where it cannot be written there is none, and code is not colored
    [TestMethod]
    public void TestNoneWhereItCannotBeWritten()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(folder)!);
        File.WriteAllText(folder, "a file where the folder should be");

        Assert.IsNull(OnigurumaLibrary.Write([1, 2, 3], folder, "libonigwrap.so"));
    }
}

namespace gmdTest.Utils;

[TestClass]
public class StringExtensionsTest
{
    // A sid is the short commit id gmd shows and logs, the first 6 characters of the sha
    [TestMethod]
    public void TestSid()
    {
        Assert.AreEqual("a1b2c3", "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2".Sid());
        Assert.AreEqual("a1b2c3", "a1b2c3".Sid());
        Assert.AreEqual("a1b2", "a1b2".Sid(), "A shorter id is left as it is");
        Assert.AreEqual("", "".Sid());
    }

    // Used to turn a remote branch name into its nice name, so the prefix is only trimmed when it
    // really is a prefix
    [TestMethod]
    public void TestTrimPrefix()
    {
        Assert.AreEqual("dev", "origin/dev".TrimPrefix("origin/"));
        Assert.AreEqual("dev", "dev".TrimPrefix("origin/"), "Not a prefix, so unchanged");
        Assert.AreEqual("dev/origin/x", "dev/origin/x".TrimPrefix("origin/"), "Only at the start");
        Assert.AreEqual("aa", "aaa".TrimPrefix("a"), "Only the first occurrence");
        Assert.AreEqual("", "origin/".TrimPrefix("origin/"));
        Assert.AreEqual("dev", "dev".TrimPrefix(""));
    }

    [TestMethod]
    public void TestTrimSuffix()
    {
        Assert.AreEqual("file", "file.txt".TrimSuffix(".txt"));
        Assert.AreEqual("file.txt", "file.txt".TrimSuffix(".md"), "Not a suffix, so unchanged");
        Assert.AreEqual("aa", "aaa".TrimSuffix("a"), "Only the last occurrence");
        Assert.AreEqual("", ".txt".TrimSuffix(".txt"));
        Assert.AreEqual("file", "file".TrimSuffix(""));
    }

    // Truncates a text to fit a column, optionally padding it so the column has a fixed width
    [TestMethod]
    public void TestMax()
    {
        Assert.AreEqual("abc", "abcdef".Max(3));
        Assert.AreEqual("abc", "abc".Max(3));
        Assert.AreEqual("ab", "ab".Max(3), "Not filled unless asked for");
        Assert.AreEqual("ab ", "ab".Max(3, true));
        Assert.AreEqual("abc", "abcdef".Max(3, true), "Filling does not affect a truncated text");
        Assert.AreEqual("   ", "".Max(3, true));
        Assert.AreEqual("", "abc".Max(0));
    }

    [TestMethod]
    public void TestToJson()
    {
        Assert.AreEqual("{\n  \"Name\": \"dev\"\n}", new { Name = "dev" }.ToJson().Replace("\r\n", "\n"));
        Assert.AreEqual("", ((object?)null).ToJson());
    }

    // The version text in the About dialog: 'major.minor' is the hand set version and the two in
    // parenthesis are derived from the build time
    [TestMethod]
    public void TestVersionTxt()
    {
        Assert.AreEqual("0.91 (1234.567)", new Version(0, 91, 1234, 567).Txt());
        Assert.AreEqual("", ((Version?)null).Txt());
        Assert.AreEqual("0.91 (-1.-1)", new Version(0, 91).Txt(), "Missing parts are -1");
    }

    // Note the integer division: a size is rounded down to a whole unit, so the '0.##' in the
    // format never has a fraction to show
    [TestMethod]
    public void TestFileSize()
    {
        Assert.AreEqual("0 B", 0L.FileSize());
        Assert.AreEqual("1023 B", 1023L.FileSize());
        Assert.AreEqual("1 KB", 1024L.FileSize());
        Assert.AreEqual("1 KB", 1536L.FileSize(), "Rounded down, not 1.5 KB");
        Assert.AreEqual("1 MB", (1024L * 1024).FileSize());
        Assert.AreEqual("5 GB", (5L * 1024 * 1024 * 1024).FileSize());
    }
}

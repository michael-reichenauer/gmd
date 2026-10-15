namespace gmdTest.Utils;

[TestClass]
public class StringExtensionsTest
{
    // A sid is the short commit id gmd shows and logs, the first 8 characters of the sha
    [TestMethod]
    public void TestSid()
    {
        Assert.AreEqual("a1b2c3d4", "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2".Sid());
        Assert.AreEqual("a1b2c3d4", "a1b2c3d4".Sid());
        Assert.AreEqual("a1b2c3", "a1b2c3".Sid(), "A shorter id is left as it is");
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

    // Compared character by character, not by the user's culture, which ignores characters that
    // are not drawn, as a soft hyphen (U+00AD) or a zero width joiner: 'origin/' was then a prefix
    // of 'orig\u00ADin/dev', and trimming it cut one character too few, leaving '/dev'. A branch
    // name may hold either.
    [TestMethod]
    public void TestTrimPrefixAndSuffixCompareCharacters()
    {
        Assert.AreEqual("orig\u00ADin/dev", "orig\u00ADin/dev".TrimPrefix("origin/"));
        Assert.AreEqual("origin\u200D/dev", "origin\u200D/dev".TrimPrefix("origin/"));
        Assert.AreEqual("repo.git\u00AD", "repo.git\u00AD".TrimSuffix(".git"));
        Assert.AreEqual("dev", "origin/dev".TrimPrefix("origin/"));
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

    // The version text in the About dialog: 'major.minor' is the hand set version and the two in
    // parenthesis are derived from the build time
    [TestMethod]
    public void TestVersionTxt()
    {
        Assert.AreEqual("0.91 (1234.567)", new Version(0, 91, 1234, 567).Txt());
        Assert.AreEqual("", ((Version?)null).Txt());
        Assert.AreEqual("0.91 (-1.-1)", new Version(0, 91).Txt(), "Missing parts are -1");
    }

    // Who made a branch's last commit, as the branch lists show it
    [TestMethod]
    public void TestInitials()
    {
        Assert.AreEqual("T U", "Test User".Initials());
        Assert.AreEqual("A", "  Anna  ".Initials());
        Assert.AreEqual("J R", "John Ronald Tolkien".Initials(), "Two at most");
        Assert.AreEqual("", "".Initials());
    }

    // A size is rounded down to a whole unit
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

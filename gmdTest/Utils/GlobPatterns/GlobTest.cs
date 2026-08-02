using gmd.Utils.GlobPatterns;

namespace gmdTest.Utils.GlobPatterns;

// Glob is vendored third-party code (based on github.com/kthompson/glob). gmd uses it in one
// place: FileMonitor turns the lines of .gitignore into patterns and ignores a file change when
// one of them matches, so a build or a test run does not make gmd reload the repo over and over.
// These tests document what we rely on rather than what the library set out to do.
[TestClass]
public class GlobTest
{
    [TestMethod]
    public void TestWildcardMatchesWithinOneSegment()
    {
        Assert.IsTrue(Glob.IsMatch("file.txt", "*.txt"));
        Assert.IsFalse(Glob.IsMatch("file.md", "*.txt"));
        Assert.IsTrue(Glob.IsMatch("file.txt", "file.*"));
        Assert.IsTrue(Glob.IsMatch("file.txt", "*"));
        Assert.IsTrue(Glob.IsMatch("file.txt", "f*e.txt"));
    }

    // A pattern of a single segment also matches the file name in any folder, which is what makes
    // a .gitignore line like '*.suo' or 'TODO.txt' work at any depth
    [TestMethod]
    public void TestSingleSegmentPatternAlsoMatchesTheFileNameAtAnyDepth()
    {
        Assert.IsTrue(Glob.IsMatch("src/sub/file.txt", "*.txt"));
        Assert.IsTrue(Glob.IsMatch("src/sub/TODO.txt", "TODO.txt"));
        Assert.IsFalse(Glob.IsMatch("src/sub/file.txt", "src/*.txt"), "But a rooted pattern does not");
    }

    [TestMethod]
    public void TestDirectoryWildcard()
    {
        Assert.IsTrue(Glob.IsMatch("bin/Debug/net8.0/gmd.dll", "bin/**/*"));
        Assert.IsTrue(Glob.IsMatch("bin/gmd.dll", "bin/**/*"), "Zero folders in between also matches");
        Assert.IsTrue(Glob.IsMatch("src/a/b/c.cs", "**/*.cs"));
        Assert.IsTrue(Glob.IsMatch("a.cs", "**/*.cs"));
    }

    // The reason FileMonitor rewrites a 'folder/' line to '**/folder/**/*': a pattern that starts
    // with the folder name only matches it at the root
    [TestMethod]
    public void TestPatternIsAnchoredAtTheStart()
    {
        Assert.IsTrue(Glob.IsMatch("obj/x.dll", "obj/**/*"));
        Assert.IsFalse(Glob.IsMatch("src/obj/x.dll", "obj/**/*"));
        Assert.IsTrue(Glob.IsMatch("src/obj/x.dll", "**/obj/**/*"));
        Assert.IsTrue(Glob.IsMatch("obj/x.dll", "**/obj/**/*"), "The leading '**' also matches nothing");
    }

    [TestMethod]
    public void TestCharacterWildcard()
    {
        Assert.IsTrue(Glob.IsMatch("file1.txt", "file?.txt"));
        Assert.IsFalse(Glob.IsMatch("file.txt", "file?.txt"), "One character, not none");
        Assert.IsFalse(Glob.IsMatch("file12.txt", "file?.txt"));
    }

    [TestMethod]
    public void TestCharacterSet()
    {
        Assert.IsTrue(Glob.IsMatch("file3.txt", "file[0-9].txt"));
        Assert.IsFalse(Glob.IsMatch("filex.txt", "file[0-9].txt"));
        Assert.IsTrue(Glob.IsMatch("filea.txt", "file[abc].txt"));
        Assert.IsTrue(Glob.IsMatch("filex.txt", "file[!0-9].txt"), "'!' inverts the set");
        Assert.IsFalse(Glob.IsMatch("file3.txt", "file[!0-9].txt"));
    }

    [TestMethod]
    public void TestLiteralSet()
    {
        Assert.IsTrue(Glob.IsMatch("a.cs", "*.{cs,md}"));
        Assert.IsTrue(Glob.IsMatch("a.md", "*.{cs,md}"));
        Assert.IsFalse(Glob.IsMatch("a.txt", "*.{cs,md}"));
    }

    // Both are folder separators and neither the pattern nor the input is case sensitive, so the
    // same .gitignore works on Windows and on Linux
    [TestMethod]
    public void TestSeparatorsAndCase()
    {
        Assert.IsTrue(Glob.IsMatch("src\\sub\\file.cs", "src/sub/*.cs"));
        Assert.IsTrue(Glob.IsMatch("src/sub/file.cs", "src\\sub\\*.cs"));
        Assert.IsTrue(Glob.IsMatch("SRC/File.CS", "src/*.cs"));
        Assert.IsTrue(Glob.IsMatch("src/file.cs", "SRC/*.CS"));
    }

    [TestMethod]
    public void TestExactPath()
    {
        Assert.IsTrue(Glob.IsMatch("a/b.txt", "a/b.txt"));
        Assert.IsFalse(Glob.IsMatch("x/a/b.txt", "a/b.txt"));
        Assert.IsFalse(Glob.IsMatch("a/b/c.txt", "a/b.txt"));
        Assert.IsFalse(Glob.IsMatch("a", "a/b.txt"), "A shorter path is not a match");
    }

    // The pattern is compiled once and reused, which is how FileMonitor uses it
    [TestMethod]
    public void TestCompiledPatternIsReusable()
    {
        var glob = new Glob("**/obj/**/*");

        Assert.AreEqual("**/obj/**/*", glob.Pattern);
        Assert.IsTrue(glob.IsMatch("src/obj/a.dll"));
        Assert.IsTrue(glob.IsMatch("obj/b.dll"));
        Assert.IsFalse(glob.IsMatch("src/a.cs"));
    }

    // FileMonitor catches the exception a bad line gives, so a .gitignore with anything unexpected
    // in it does not stop the rest of the patterns
    [TestMethod]
    public void TestAnUnterminatedSetThrows()
    {
        Assert.ThrowsExactly<Exception>(() =>
        {
            new Glob("file[0-9.txt");
        });
    }
}

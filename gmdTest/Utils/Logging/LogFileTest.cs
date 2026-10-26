using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace gmdTest.Utils.Logging;

[TestClass]
public class LogFileTest
{
    // A log line: the time, the level and the message, padded to a column, and then where it was
    // logged, as a path from the root of the repository
    [TestMethod]
    public void TestLogLine()
    {
        var line = LogFile.ToLogLine("INFO ", "Hello", "Member", ThisFile(), 12);

        var place = Regex.Escape($"{{Member() {Path.Join("gmdTest", "Utils", "Logging", "LogFileTest.cs")}:12}}");
        StringAssert.Matches(line, new Regex($@"^\d{{4}}-\d\d-\d\d \d\d:\d\d:\d\d\.\d{{3}} INFO :""Hello"" +{place}$"));
        Assert.AreEqual(101, line.IndexOf("{Member()"), "The message is padded to a column of 100");
    }

    // The time is ISO like whatever the user's culture, whose calendar would otherwise write the
    // year as 1448 under ar-SA and 2569 under th-TH, see TimeDateExtensionsTest
    [TestMethod]
    [DataRow("en-US")]
    [DataRow("ar-SA")]
    [DataRow("th-TH")]
    [DataRow("fa-IR")]
    public void TestLogLineTimeIsCultureInvariant(string cultureName)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            var year = DateTime.Now.Year;

            var line = LogFile.ToLogLine("INFO ", "Hello", "Member", ThisFile(), 12);

            StringAssert.StartsWith(line, $"{year}-", $"Wrong year for culture '{cultureName}'");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // Each run begins with a line that stands out, after a blank one, padded with '#' to 120
    [TestMethod]
    public void TestStartLine()
    {
        var line = LogFile.StartLine(new DateTime(2026, 10, 26, 9, 37, 48, 123), 4711);

        Assert.AreEqual("\n######## gmd started 2026-10-26 09:37:48.123, process 4711 " + new string('#', 61), line);
        Assert.AreEqual(121, line.Length);
        StringAssert.StartsWith(line[1..], LogFile.StartLinePrefix);
    }

    // Every start of gmd goes on with the log, so that what went wrong in the run before is still
    // there to report
    [TestMethod]
    public void TestOpenGoesOnWithTheLog()
    {
        using var folder = new TempFolder();
        var path = folder.File("gmd.log", "A line of the last run\n");

        AssertOk(LogFile.Open(path));

        Assert.AreEqual("A line of the last run\n", File.ReadAllText(path));
        Assert.IsTrue(LogFile.Open(folder.Path("new.log")) is Success, "A missing log is created");
        Assert.IsTrue(File.Exists(folder.Path("new.log")));
    }

    // A log grown past its 1 MB is rotated as gmd starts, to gmd.1.log, and gmd begins a new one
    [TestMethod]
    public void TestOpenRotatesATooLargeLog()
    {
        using var folder = new TempFolder();
        var large = new string('x', 1024 * 1024 + 1);
        var path = folder.File("gmd.log", large);

        AssertOk(LogFile.Open(path));

        Assert.AreEqual("", File.ReadAllText(path));
        Assert.AreEqual(large, File.ReadAllText(folder.Path("gmd.1.log")));
    }

    // Two rotated files are kept, the newest as gmd.1.log, and the oldest goes
    [TestMethod]
    public void TestRotateKeepsTwoRotatedFiles()
    {
        using var folder = new TempFolder();
        var path = folder.File("gmd.log", "Third");
        folder.File("gmd.1.log", "Second");
        folder.File("gmd.2.log", "First");

        LogFile.Rotate(path);

        Assert.IsFalse(File.Exists(path), "The log begins anew with its next line");
        Assert.AreEqual("Third", File.ReadAllText(folder.Path("gmd.1.log")));
        Assert.AreEqual("Second", File.ReadAllText(folder.Path("gmd.2.log")));
        CollectionAssert.AreEquivalent(
            new[] { "gmd.1.log", "gmd.2.log" },
            Directory.GetFiles(folder.Path("")).Select(Path.GetFileName).ToArray(),
            "'First' is gone"
        );
    }

    // The file an older gmd moved its large log to, gmd.log.2.log, which nothing reads any more
    [TestMethod]
    public void TestOpenRemovesTheRotatedFileOfAnOlderGmd()
    {
        using var folder = new TempFolder();
        var path = folder.File("gmd.log", "");
        folder.File("gmd.log.2.log", "Old");

        AssertOk(LogFile.Open(path));

        Assert.IsFalse(File.Exists(folder.Path("gmd.log.2.log")));
    }

    // A file gmd cannot write, as one that a 'sudo gmd' left owned by root, is rotated aside, which
    // takes only the right to write the folder, so its lines are kept. It used to stop gmd on a blank
    // screen.
    [TestMethod]
    public void TestOpenRotatesALogThatCannotBeWritten()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("A read-only file cannot be moved on Windows either");
        if (Environment.IsPrivilegedProcess)
            Assert.Inconclusive("Root writes a read-only file all the same");
        using var folder = new TempFolder();
        var path = folder.File("gmd.log", "A line of the last run\n");
        if (!OperatingSystem.IsWindows()) // Not run there, but the analyzer does not see that (CA1416)
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        AssertOk(LogFile.Open(path));

        Assert.AreEqual("", File.ReadAllText(path));
        Assert.AreEqual("A line of the last run\n", File.ReadAllText(folder.Path("gmd.1.log")));
        File.AppendAllText(path, "Written");
    }

    // Copy Log copies the last whole lines that fit, which may be all of them
    [TestMethod]
    public void TestTail()
    {
        string[] lines = ["one", "two", "three"];

        Assert.AreEqual(("one\ntwo\nthree", 3), LogFile.Tail(lines, 100));
        Assert.AreEqual(("two\nthree", 2), LogFile.Tail(lines, 10), "'two\\nthree\\n' is 10");
        Assert.AreEqual(("", 0), LogFile.Tail(lines, 5), "No part of a line");
        Assert.AreEqual(("", 0), LogFile.Tail([], 100));
    }

    // Where not even that works, the log is an error, which leaves gmd logging nowhere rather than
    // failing
    [TestMethod]
    public void TestOpenInAFolderThatIsNotThereFails()
    {
        using var folder = new TempFolder();

        AssertError(LogFile.Open(Path.Join(folder.Path("missing"), "gmd.log")));
    }

    sealed class TempFolder : IDisposable
    {
        readonly string root = System.IO.Path.Join(System.IO.Path.GetTempPath(), $"gmdTest-log-{Guid.NewGuid():N}");

        public TempFolder() => Directory.CreateDirectory(root);

        public string Path(string name) => System.IO.Path.Join(root, name);

        public string File(string name, string text)
        {
            var path = Path(name);
            System.IO.File.WriteAllText(path, text);
            return path;
        }

        public void Dispose() => Directory.Delete(root, true);
    }

    static string ThisFile([CallerFilePath] string path = "") => path;
}

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

    // Every start of gmd begins the log anew
    [TestMethod]
    public void TestOpenEmptiesTheLog()
    {
        using var folder = new TempFolder();
        var path = folder.File("gmd.log", "A line of the last run\n");

        AssertOk(LogFile.Open(path));

        Assert.AreEqual("", File.ReadAllText(path));
        Assert.IsTrue(LogFile.Open(folder.Path("new.log")) is Success, "A missing log is created");
    }

    // A file gmd cannot write, as one that a 'sudo gmd' left owned by root, is replaced, which takes
    // only the right to write the folder. It used to stop gmd on a blank screen.
    [TestMethod]
    public void TestOpenReplacesALogThatCannotBeWritten()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("A read-only file cannot be deleted on Windows either");
        if (Environment.IsPrivilegedProcess)
            Assert.Inconclusive("Root writes a read-only file all the same");
        using var folder = new TempFolder();
        var path = folder.File("gmd.log", "A line of the last run\n");
        if (!OperatingSystem.IsWindows()) // Not run there, but the analyzer does not see that (CA1416)
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        AssertOk(LogFile.Open(path));

        Assert.AreEqual("", File.ReadAllText(path));
        File.AppendAllText(path, "Written");
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

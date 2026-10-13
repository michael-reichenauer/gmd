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

    static string ThisFile([CallerFilePath] string path = "") => path;
}

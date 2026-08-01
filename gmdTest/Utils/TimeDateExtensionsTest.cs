using System.Globalization;
using System.Text.RegularExpressions;

namespace gmdTest.Utils;

[TestClass]
public class TimeDateExtensionsTest
{
    static readonly DateTime Time = new DateTime(2023, 2, 7, 4, 5, 6, 789, DateTimeKind.Utc);

    [TestMethod]
    public void TestIso()
    {
        Assert.AreEqual("2023-02-07 04:05:06", Time.Iso());
        Assert.AreEqual("2023-02-07 04:05:06.789", Time.IsoMs());
        Assert.AreEqual("2023-02-07", Time.IsoDate());
        Assert.AreEqual("2023-02-07 04:05:06 +00:00", Time.IsoZone(), "A UTC time has no offset");
    }

    // The zone is the offset of the value, so a local time is written with the machine's offset
    [TestMethod]
    public void TestIsoZoneOfALocalTime()
    {
        var local = new DateTimeOffset(2023, 2, 7, 4, 5, 6, TimeSpan.Zero).LocalDateTime;

        StringAssert.StartsWith(local.IsoZone(), $"{local.Iso()} ");
        StringAssert.Matches(local.IsoZone(), new Regex(@" [+-]\d\d:\d\d$"));
    }

    // These formats are ISO like, so they must not follow the user's culture. A culture with its
    // own calendar is the interesting one: 2023 is 1444 under ar-SA (Umm al-Qura), 2566 under
    // th-TH (Buddhist) and 1401 under fa-IR (Persian). Not only cosmetic: IsoDate is written into
    // the generated CHANGELOG.md and is what the log view filter matches a date against.
    [DataTestMethod]
    [DataRow("en-US")]
    [DataRow("sv-SE")]
    [DataRow("de-DE")]
    [DataRow("ar-SA")]
    [DataRow("th-TH")]
    [DataRow("fa-IR")]
    public void TestIsCultureInvariant(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalDefault = CultureInfo.DefaultThreadCurrentCulture;
        try
        {
            var culture = new CultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;

            Assert.AreEqual("2023-02-07 04:05:06", Time.Iso(), $"Iso wrong for culture '{cultureName}'");
            Assert.AreEqual("2023-02-07 04:05:06.789", Time.IsoMs(), $"IsoMs wrong for culture '{cultureName}'");
            Assert.AreEqual("2023-02-07", Time.IsoDate(), $"IsoDate wrong for culture '{cultureName}'");
            Assert.AreEqual("2023-02-07 04:05:06 +00:00", Time.IsoZone(), $"IsoZone wrong for culture '{cultureName}'");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.DefaultThreadCurrentCulture = originalDefault;
        }
    }
}

using gmd;

namespace gmdTest;

// gmd has no version file: the two last version numbers are derived from when the binary was
// built, counted from a fixed base time, and 'Built:' in the About dialog is that arithmetic run
// backwards from a version number. These pin the encoding, since a released version and the gmd
// that reads it are built from different commits and have to agree on it.
//
// The encoding is in UTC: the times are written as UTC ("…T00:00:00Z") and read as UTC, so the
// version a gmd computes for itself is the one CI tagged its release with, whatever the time zone
// of the machine it runs on. See MODERNIZATION.md.
[TestClass]
public class BuildTest
{
    // The base build time, i.e. the start of the project, is version 'x.y.0.0'
    static DateTime BaseBuildTime => Build.GetBuildTime(new Version(0, 0, 0, 0));

    [TestMethod]
    public void TestBaseBuildTime()
    {
        Assert.AreEqual(new DateTime(2022, 10, 30, 0, 0, 0, DateTimeKind.Utc), BaseBuildTime);
        Assert.AreEqual(DateTimeKind.Utc, BaseBuildTime.Kind);
    }

    // A preview built at 06:38:39Z is tagged 0.91.1429.398 by CI, which runs UTC. Read as local
    // time, the same binary called itself 0.91.1429.518 in UTC+2, which is later than the stable
    // release 0.91.1429.412 built fourteen minutes after it, so that release was never offered.
    // DateTime equality ignores the kind, and on a UTC machine local time is UTC, hence the kind
    // is asserted too: that is what fails on CI if the times are read as local again.
    [TestMethod]
    public void TestBuildTimeIsReadAsUtcWhateverTheTimeZone()
    {
        Assert.IsTrue(Build.TryParseDateTime("2026-09-28T06:38:39Z", out var buildTime));

        Assert.AreEqual(DateTimeKind.Utc, buildTime.Kind);
        Assert.AreEqual(new DateTime(2026, 9, 28, 6, 38, 39, DateTimeKind.Utc), buildTime);
        Assert.AreEqual((1429, 398), Build.GetTimeSinceBaseTime(buildTime));

        var releaseBuildTime = Build.GetBuildTime("0.91.1429.412");
        Assert.AreEqual(DateTimeKind.Utc, releaseBuildTime.Kind);
        Assert.AreEqual(new DateTime(2026, 9, 28, 6, 52, 0, DateTimeKind.Utc), releaseBuildTime);
    }

    [TestMethod]
    public void TestVersionIsTheProgramVersionAndTheTimeSinceTheBaseBuildTime()
    {
        var version = Build.Version();

        Assert.AreEqual(Program.MajorVersion, version.Major);
        Assert.AreEqual(Program.MinorVersion, version.Minor);
        Assert.AreEqual(Build.GetTimeSinceBaseTime(Build.Time()), (version.Build, version.Revision));
    }

    // The build time is unknown unless CI injected it or the assembly carries it, and it is then
    // 'default', i.e. long before the base build time. Both version numbers would be negative,
    // which Version() cannot express, so it reports the base time instead of throwing.
    [TestMethod]
    public void TestVersionOfAnUnknownBuildTimeIsTheBaseVersion()
    {
        Assert.AreEqual((0, 0), Build.GetTimeSinceBaseTime(default));
        Assert.AreEqual((0, 0), Build.GetTimeSinceBaseTime(BaseBuildTime.AddDays(-1)));

        // Which is what a test run gets, since the CI placeholder does not parse as a time and the
        // test host assembly has no build time either
        var version = Build.Version();
        Assert.IsTrue(version.Build >= 0 && version.Revision >= 0, $"Negative version numbers in {version}");
    }

    [TestMethod]
    public void TestDaysSinceTheBaseBuildTimeIsTheThirdVersionNumber()
    {
        Assert.AreEqual(0, Build.GetTimeSinceBaseTime(BaseBuildTime).Item1);
        Assert.AreEqual(0, Build.GetTimeSinceBaseTime(BaseBuildTime.AddHours(23)).Item1, "Not a whole day yet");
        Assert.AreEqual(1, Build.GetTimeSinceBaseTime(BaseBuildTime.AddDays(1)).Item1);
        Assert.AreEqual(200, Build.GetTimeSinceBaseTime(BaseBuildTime.AddDays(200).AddMinutes(555)).Item1);
    }

    // The fourth version number is the time of day, so it is always within the day. It used to be
    // counted from midnight UTC while the build time itself was local, which made it negative for
    // a build made between midnight and the time zone's offset, and Version() then threw. Both are
    // UTC now, see TestBuildTimeIsReadAsUtcWhateverTheTimeZone.
    [TestMethod]
    public void TestMinutesSinceMidnightIsTheFourthVersionNumber()
    {
        Assert.AreEqual(0, Build.GetTimeSinceBaseTime(new DateTime(2023, 2, 7, 0, 0, 0)).Item2);
        Assert.AreEqual(30, Build.GetTimeSinceBaseTime(new DateTime(2023, 2, 7, 0, 30, 0)).Item2);
        Assert.AreEqual(4 * 60 + 30, Build.GetTimeSinceBaseTime(new DateTime(2023, 2, 7, 4, 30, 0)).Item2);
        Assert.AreEqual(23 * 60 + 59, Build.GetTimeSinceBaseTime(new DateTime(2023, 2, 7, 23, 59, 59)).Item2);
    }

    // What the About dialog shows as 'Built:' for a version, i.e. the encoding run backwards
    [TestMethod]
    public void TestGetBuildTimeIsTheBaseTimePlusTheDaysAndMinutes()
    {
        Assert.AreEqual(BaseBuildTime.AddDays(100).AddMinutes(30), Build.GetBuildTime(new Version(0, 91, 100, 30)));
        Assert.AreEqual(BaseBuildTime.AddDays(100).AddMinutes(30), Build.GetBuildTime("0.91.100.30"));
    }

    [TestMethod]
    public void TestBuildTimeOfAVersionRoundTrips()
    {
        var buildTime = BaseBuildTime.AddDays(200).AddMinutes(555);

        var (days, minutes) = Build.GetTimeSinceBaseTime(buildTime);

        Assert.AreEqual(200, days);
        Assert.AreEqual(buildTime.TimeOfDay, TimeSpan.FromMinutes(minutes), "The minutes are the time of day");
        Assert.AreEqual(
            BaseBuildTime.AddDays(days).AddMinutes(minutes),
            Build.GetBuildTime(new Version(Program.MajorVersion, Program.MinorVersion, days, minutes))
        );
    }

    // Updater passes the version text of the latest GitHub release, so an unexpected tag name
    // must not throw
    [TestMethod]
    public void TestGetBuildTimeOfAnUnparsableVersionIsMinValue()
    {
        Assert.AreEqual(DateTime.MinValue, Build.GetBuildTime("not a version"));
        Assert.AreEqual(DateTime.MinValue, Build.GetBuildTime(""));
        Assert.AreEqual(DateTime.MinValue, Build.GetBuildTime("v0.91.100.30"));
    }

    // A version with less than four parts has -1 for the missing ones, so its build time lands
    // just before the base time rather than on it
    [TestMethod]
    public void TestGetBuildTimeOfAPartialVersion()
    {
        Assert.AreEqual(BaseBuildTime.AddDays(3).AddMinutes(-1), Build.GetBuildTime("0.91.3"));
        Assert.AreEqual(BaseBuildTime.AddDays(-1).AddMinutes(-1), Build.GetBuildTime("0.91"));
    }

    // Sha is the sid of a literal that CI replaces with the commit sha (see Build.cs), so it is
    // six characters both before and after that replacement
    [TestMethod]
    public void TestShaIsASid()
    {
        Assert.AreEqual(6, Build.Sha().Length);
    }
}

using gmd;

namespace gmdTest;

// gmd has no version file: the two last version numbers are derived from when the binary was
// built, counted from a fixed base time, and 'Built:' in the About dialog is that arithmetic run
// backwards from a version number. These pin the encoding, since a released version and the gmd
// that reads it are built from different commits and have to agree on it.
//
// Note that the base build time is written as UTC ("…T00:00:00Z") but parsed into local time, so
// the encoding is anchored at midnight only on a machine running UTC — which CI does, and CI is
// what builds the released versions. The tests below are therefore written to hold in any time
// zone. See MODERNIZATION.md.
[TestClass]
public class BuildTest
{
    // The base build time, i.e. the start of the project, is version 'x.y.0.0'
    static DateTime BaseBuildTime => Build.GetBuildTime(new Version(0, 0, 0, 0));

    [TestMethod]
    public void TestBaseBuildTime()
    {
        Assert.AreEqual(new DateTimeOffset(2022, 10, 30, 0, 0, 0, TimeSpan.Zero).LocalDateTime, BaseBuildTime);
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
    // a build made between midnight and the time zone's offset, and Version() then threw.
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

using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace gmd.Utils;

// Build contains build time and version information (do not move file)
static class Build
{
    // Do not change or move these values, they are used by CI/CD (see .github/workflows/build-and-release.yml)
    static readonly string CiCdBuildTimeText = "BUILD_TIME";
    static readonly string CiCdBuildShaText = "BUILD_SHA";

    // Base build time (currently start of project)
    const string BaseBuildTimeText = "2022-10-30T00:00:00Z";
    const string DateFormat = "yyyy-MM-ddTHH:mm:ssZ";

    public static bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public static bool IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static Version Version()
    {
        // The version is always increasing using the base build time for last 2 version numbers
        (int daysSinceBase, int minutesSinceMidnight) = GetTimeSinceBaseTime(Time());

        // Return version based on major version and time diff between first and latest build
        return new Version(Program.MajorVersion, Program.MinorVersion, daysSinceBase, minutesSinceMidnight);
    }

    public static DateTime GetBuildTime(string versionText)
    {
        if (!System.Version.TryParse(versionText, out var version))
            return DateTime.MinValue;
        return GetBuildTime(version);
    }

    public static DateTime GetBuildTime(Version version)
    {
        if (!TryParseDateTime(BaseBuildTimeText, out var baseBuildTime))
            return DateTime.MinValue;

        return baseBuildTime.AddDays(version.Build).AddMinutes(version.Revision);
    }

    public static DateTime Time()
    {
        if (TryParseDateTime(CiCdBuildTimeText, out var ciCdBuildTime))
        { // The CI/DI build script injected the build time, lets use that
            return ciCdBuildTime;
        }

        var assemblyBuildTimeText = AssemblyVersionBuildTime();
        if (TryParseDateTime(assemblyBuildTimeText, out var assemblyBuildTime))
        { // The build time form the assembly (SourceRevisionId field in .csproj file)
            return assemblyBuildTime;
        }

        return default;
    }

    public static string Sha() => CiCdBuildShaText.Sid();

    public static bool IsDevInstance() => Environment.CommandLine.Contains("gmd.dll") || IsDotNet();

    static bool IsDotNet()
    {
        var thisPath = Environment.ProcessPath ?? "gmd";
        return Path.GetFileNameWithoutExtension(thisPath) == "dotnet";
    }

    // The two last version numbers, i.e. the days since the base build time and the minutes since
    // midnight of the build day. Takes the build time so it can be tested with a time of its own.
    internal static (int, int) GetTimeSinceBaseTime(DateTime cbt)
    {
        if (!TryParseDateTime(BaseBuildTimeText, out var baseBuildTime))
            return (0, 0);

        // A build time that is not known is 'default', i.e. long before the base time. Both version
        // numbers would then be negative, which Version() cannot express, so report it as the base
        // time, i.e. version 'x.y.0.0'.
        if (cbt < baseBuildTime)
            return (0, 0);

        // Calculate days since first build and current build
        var timeSinceBase = cbt - baseBuildTime;
        var daysSinceBase = (int)timeSinceBase.TotalDays;

        // Calculate in minutes for build time after midnight of the build date
        var minutesSinceMidnight = (int)(cbt - cbt.Date).TotalMinutes;

        return (daysSinceBase, minutesSinceMidnight);
    }

    static bool TryParseDateTime(string text, out DateTime dateTime) =>
        DateTime.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime);

    static string AssemblyVersionBuildTime()
    {
        const string BuildVersionMetadataPrefix = "+build";

        var attribute = Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (attribute?.InformationalVersion != null)
        {
            var value = attribute.InformationalVersion;
            var index = value.IndexOf(BuildVersionMetadataPrefix);
            if (index > 0)
            {
                value = value[(index + BuildVersionMetadataPrefix.Length)..];
                return value;
            }
        }

        return "";
    }
}

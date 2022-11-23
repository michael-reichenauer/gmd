using System.Globalization;
using System.Reflection;

namespace gmd.Utils;

static class Build
{
    static readonly string CiCdBuildTimeText = "BUILD_TIME";
    static readonly string CiCdBuildShaText = "BUILD_SHA";

    // Base build time (currently start of project)
    const string BaseBuildTimeText = "2022-10-30T00:00:00Z";
    const string DateFormat = "yyyy-MM-ddTHH:mm:ssZ";


    internal static Version Version()
    {
        Log.Info($"{CiCdBuildTimeText}, {CiCdBuildShaText}");

        // The versin is always increasing using the base build time for last 2 version numbers
        (int daysSinceBase, int minutesSinceMidnight) = GetTimeSinceBaseTime();

        // Return version based on major version and time diff betweend first and lates build
        return new Version(Program.MajorVersion, Program.MinorVersion, daysSinceBase, minutesSinceMidnight);
    }

    internal static DateTime Time()
    {
        if (TryParseDateTime(CiCdBuildTimeText, out var ciCdBuildTime))
        {   // The CI/DI build script injected the build time, lets use that
            return ciCdBuildTime;
        }

        var assemblyBuildTimeText = AssemblyVersionBuildTime();
        if (TryParseDateTime(assemblyBuildTimeText, out var assemblyBuildTime))
        {   // The build time form the assembly (SourceRevisionId field in .csproj file)
            return assemblyBuildTime;
        }

        return default;
    }

    internal static string Sha() => CiCdBuildShaText.Substring(0, 6);


    static (int, int) GetTimeSinceBaseTime()
    {
        if (!TryParseDateTime(BaseBuildTimeText, out var baseBuildTime)) return (0, 0);

        // Get the current build time
        var cbt = Build.Time();

        // Calculate days since first build and current build
        var timeSinceBase = cbt - baseBuildTime;
        var daysSinceBase = (int)timeSinceBase.TotalDays;

        // Calculate in minutes for build time after midnight of the build date
        var buildMidnightText = $"{cbt.Year:0000}-{cbt.Month:00}-{cbt.Day:00}T00:00:00Z";
        if (!TryParseDateTime(buildMidnightText, out var buildMidnight)) return (0, 0);
        var minutesSinceMidnight = (int)(cbt - buildMidnight).TotalMinutes;

        return (daysSinceBase, minutesSinceMidnight);
    }

    static bool TryParseDateTime(string text, out DateTime dateTime) =>
        DateTime.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime);


    static string AssemblyVersionBuildTime()
    {
        const string BuildVersionMetadataPrefix = "+build";

        var attribute = Assembly.GetEntryAssembly()!
          .GetCustomAttribute<AssemblyInformationalVersionAttribute>();


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

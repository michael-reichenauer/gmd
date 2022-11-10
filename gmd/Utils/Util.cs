using System.Globalization;
using System.Reflection;

namespace gmd.Utils;

static class Util
{
    static readonly DateTime firstBuildTime = new DateTime(2022, 10, 30);

    internal static Version GetBuildVersion(int major = 0)
    {
        var buildTime = GetBuildTime();
        var timeSinceFirst = buildTime - firstBuildTime;

        var daysSinceFirst = (int)timeSinceFirst.TotalDays;
        var buildMidnight = new DateTime(buildTime.Year, buildTime.Month, buildTime.Day);
        var minutesSinceBuildMidnight = (int)(buildTime - buildMidnight).TotalMinutes;

        return new Version(major, daysSinceFirst, minutesSinceBuildMidnight);
    }

    internal static DateTime GetBuildTime()
    {
        const string BuildVersionMetadataPrefix = "+build";
        const string dateFormat = "yyyy-MM-ddTHH:mm:ss:fffZ";

        var attribute = Assembly.GetEntryAssembly()!
          .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (attribute?.InformationalVersion != null)
        {
            var value = attribute.InformationalVersion;
            var index = value.IndexOf(BuildVersionMetadataPrefix);
            if (index > 0)
            {
                value = value[(index + BuildVersionMetadataPrefix.Length)..];

                return DateTime.ParseExact(value, dateFormat, CultureInfo.InvariantCulture);
            }
        }

        return default;
    }
}
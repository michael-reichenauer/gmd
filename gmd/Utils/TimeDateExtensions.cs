using System.Globalization;

namespace System;

// Some useful DateTime extensions that are missing in .NET
//
// The formats are ISO like, so they must not depend on the user's culture. A culture with its own
// calendar renders 'yyyy' in that calendar (2023 becomes 1444 under ar-SA, 2566 under th-TH and
// 1401 under fa-IR), and these dates are not only shown: IsoDate is written into the generated
// CHANGELOG.md and is matched against the filter text in the log view.
public static class TimeDateExtensions
{
    public static string Iso(this DateTime source) =>
        source.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static string IsoMs(this DateTime source) =>
        source.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

    public static string IsoZone(this DateTime source) =>
        source.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);

    public static string IsoDate(this DateTime source) => source.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}




namespace System;

public static class TimeDateExtensions
{
    public static string Iso(this DateTime source)
    {
        return $"{source:yyyy-MM-dd HH:mm:ss.fff}";
    }
    public static string IsoUTC(this DateTime source)
    {
        return $"{source:yyyy-MM-dd HH:mm:ss.fffZ}";
    }
}






namespace System;

public static class TimeDateExtensions
{
    public static string Iso(this DateTime source)
    {
        return $"{source:yyyy-MM-dd HH:mm:ss.fff}";
    }
}



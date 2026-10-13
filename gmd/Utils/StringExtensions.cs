namespace gmd.Utils;

// Some useful string extensions that are missing in .NET
public static class StringExtensions
{
    const int SidLength = 6;

    // Method that limits the length of text to a defined length and can fill the rest with spaces
    public static string Max(this string source, int maxLength, bool isFill = false)
    {
        var text = source;
        if (isFill && text.Length < maxLength)
        {
            text += new string(' ', maxLength - text.Length);
        }

        if (text.Length <= maxLength)
        {
            return text;
        }

        return text.Substring(0, maxLength);
    }

    // Trims the prefix, compared character by character: a culture's comparison ignores characters
    // that are not drawn, e.g. a soft hyphen, and the length cut off would then be wrong
    public static string TrimPrefix(this string source, string prefix)
    {
        if (!source.StartsWith(prefix, StringComparison.Ordinal))
        {
            return source;
        }

        return source.Substring(prefix.Length);
    }

    public static string TrimSuffix(this string source, string suffix)
    {
        if (!source.EndsWith(suffix, StringComparison.Ordinal))
        {
            return source;
        }

        return source.Substring(0, source.Length - suffix.Length);
    }

    public static string Sid(this string source)
    {
        if (source.Length <= SidLength)
        {
            return source;
        }

        return source.Substring(0, SidLength);
    }

    public static string Txt(this Version? source)
    {
        if (source == null)
            return "";
        return $"{source.Major}.{source.Minor} ({source.Build}.{source.Revision})";
    }

    // A size in whole units, rounded down: 1536 bytes is '1 KB'
    public static string FileSize(this long source)
    {
        if (source < 1024)
            return $"{source} B";
        if (source < 1024 * 1024)
            return $"{source / 1024} KB";
        if (source < 1024 * 1024 * 1024)
            return $"{source / 1024 / 1024} MB";
        return $"{source / 1024 / 1024 / 1024} GB";
    }
}

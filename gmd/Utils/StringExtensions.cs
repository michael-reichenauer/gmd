namespace System;

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

    public static string TrimPrefix(this string source, string prefix)
    {
        if (!source.StartsWith(prefix))
        {
            return source;
        }

        return source.Substring(prefix.Length);
    }

    public static string TrimSuffix(this string source, string suffix)
    {
        if (!source.EndsWith(suffix))
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
}


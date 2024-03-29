using System.Text.Json;

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

    public static string ToJson(this object? source)
    {
        if (source == null) return "";

        if (!Try(out var json, out var e, () => JsonSerializer.Serialize(source, new JsonSerializerOptions { WriteIndented = true })))
        {
            return $"<Error: {e}>";
        }

        return json;
    }

    public static string Txt(this Version? source)
    {
        if (source == null) return "";
        return $"{source.Major}.{source.Minor} ({source.Build}.{source.Revision})";
    }

    public static string FileSize(this long source)
    {
        if (source < 1024) return $"{source} B";
        if (source < 1024 * 1024) return $"{source / 1024:0.##} KB";
        if (source < 1024 * 1024 * 1024) return $"{source / 1024 / 1024:0.##} MB";
        return $"{source / 1024 / 1024 / 1024:0.##} GB";
    }
}


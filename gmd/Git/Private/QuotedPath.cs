using System.Text;

namespace gmd.Git.Private;

// A path as git writes it in its output: as it is, or, when it has a character that needs it, in
// double quotes with the escapes of a C string. Those characters are '"', '\' and the control
// characters, and by default (core.quotePath) every one that is not ASCII, written as the octal
// escapes of its UTF-8 bytes, so 'ä' is '\303\244'. A path with a space is quoted by 'git status'
// and 'git clean', but not in a diff.
//
// The quotes used to be stripped and the escapes kept, so a file with an 'ä' in its name could
// not be committed by name, discarded or blamed, and a diff of it threw.
static class QuotedPath
{
    public static string Unquote(string text)
    {
        if (!IsQuoted(text))
            return text;

        var bytes = new List<byte>(text.Length);
        var end = text.Length - 1;
        for (var i = 1; i < end; )
        {
            if (text[i] != '\\')
            {
                // A run of characters as they are, which are not ASCII only when core.quotePath is off
                var start = i;
                while (i < end && text[i] != '\\')
                    i++;
                bytes.AddRange(Encoding.UTF8.GetBytes(text[start..i]));
                continue;
            }

            i++;
            if (i + 2 < end && IsOctal(text[i]) && IsOctal(text[i + 1]) && IsOctal(text[i + 2]))
            {
                bytes.Add((byte)((text[i] - '0') * 64 + (text[i + 1] - '0') * 8 + (text[i + 2] - '0')));
                i += 3;
                continue;
            }

            if (i < end)
            {
                bytes.Add(Escaped(text[i]));
                i++;
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    // Where a quoted path that starts the text ends, i.e. the index after its closing quote, or the
    // length of the text when it has none
    public static int EndOfQuoted(string text)
    {
        for (var i = 1; i < text.Length; i++)
        {
            if (text[i] == '\\')
                i++;
            else if (text[i] == '"')
                return i + 1;
        }

        return text.Length;
    }

    static bool IsQuoted(string text) => text.Length >= 2 && text[0] == '"' && text[^1] == '"';

    static bool IsOctal(char c) => c is >= '0' and <= '7';

    static byte Escaped(char c) =>
        c switch
        {
            'a' => 7,
            'b' => 8,
            't' => 9,
            'n' => 10,
            'v' => 11,
            'f' => 12,
            'r' => 13,
            _ => (byte)c, // '"' and '\'
        };
}

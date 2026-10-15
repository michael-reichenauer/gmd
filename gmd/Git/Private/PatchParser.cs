using System.Text;
using System.Text.RegularExpressions;

namespace gmd.Git.Private;

// Reads the patch of one file, as 'git diff' writes it, into a FilePatch and writes it back.
//
// Pure, as ConflictParser is, and held to the same test: ToText(Parse(x)) is x, byte for byte, for
// every shape a patch can have: a new or a deleted file, a file with '\r\n' line ends or with no
// newline at its end, a byte order mark. A patch of some of the lines is that patch changed and
// written by ToText, so if the identity holds, what git is given is what was read, less the lines
// left out.
//
// A hunk is read by its counts, as git applies it, rather than up to the next line that looks like
// a hunk's start: a line of the file can start with '@@' too.
static class PatchParser
{
    // '@@ -<old start>,<old count> +<new start>,<new count> @@<section>', a count of 1 left out
    static readonly Regex HunkStart = new Regex(
        @"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@(.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline
    );

    public static Result<FilePatch> Parse(string text)
    {
        if (text == "")
            return new Error("The patch is empty");
        if (!text.EndsWith('\n'))
            return new Error("The patch does not end with a newline");

        var lines = text[..^1].Split('\n');
        if (!lines[0].StartsWith("diff --git "))
            return new Error($"The patch does not start with 'diff --git': {lines[0]}");

        var i = 1;
        while (i < lines.Length && !lines[i].StartsWith("@@ "))
        {
            if (lines[i].StartsWith("diff --git "))
                return new Error("The patch is of more than one file");
            i++;
        }
        var header = lines[..i];

        List<PatchHunk> hunks = [];
        while (i < lines.Length)
        {
            if (ParseHunk(lines, ref i) is not PatchHunk hunk)
                return new Error($"The patch has a hunk that cannot be read, at line {i + 1}: {At(lines, i)}");
            hunks.Add(hunk);
        }

        return new FilePatch(header, hunks);
    }

    public static string ToText(FilePatch patch)
    {
        var text = new StringBuilder();
        foreach (var line in patch.Header)
            text.Append(line).Append('\n');

        foreach (var hunk in patch.Hunks)
        {
            text.Append($"@@ -{Range(hunk.OldStart, hunk.OldCount)} +{Range(hunk.NewStart, hunk.NewCount)} @@");
            text.Append(hunk.Section).Append('\n');
            foreach (var line in hunk.Lines)
            {
                text.Append(Prefix(line.Kind)).Append(line.Text).Append('\n');
                if (line.NoNewline != "")
                    text.Append(line.NoNewline).Append('\n');
            }
        }

        return text.ToString();
    }

    // The hunk starting at line i, which is then the line after it, or null when it is not one
    static PatchHunk? ParseHunk(string[] lines, ref int i)
    {
        var start = HunkStart.Match(lines[i]);
        if (!start.Success)
            return null;

        var (oldStart, oldCount) = (int.Parse(start.Groups[1].Value), Count(start.Groups[2]));
        var (newStart, newCount) = (int.Parse(start.Groups[3].Value), Count(start.Groups[4]));
        var (oldNbr, newNbr) = (oldStart, newStart);
        var (oldLeft, newLeft) = (oldCount, newCount);
        i++;

        List<PatchLine> hunkLines = [];
        while (oldLeft > 0 || newLeft > 0)
        {
            if (i >= lines.Length || lines[i] == "")
                return null;

            var text = lines[i][1..];
            PatchLine line;
            switch (lines[i][0])
            {
                case ' ':
                    line = new PatchLine(PatchLineKind.Context, text, oldNbr++, newNbr++);
                    (oldLeft, newLeft) = (oldLeft - 1, newLeft - 1);
                    break;
                case '-':
                    line = new PatchLine(PatchLineKind.Removed, text, oldNbr++, 0);
                    oldLeft--;
                    break;
                case '+':
                    line = new PatchLine(PatchLineKind.Added, text, 0, newNbr++);
                    newLeft--;
                    break;
                default:
                    return null;
            }
            if (oldLeft < 0 || newLeft < 0)
                return null;
            i++;

            // '\ No newline at end of file', after the line it is of
            if (i < lines.Length && lines[i].StartsWith('\\'))
                line = line with { NoNewline = lines[i++] };
            hunkLines.Add(line);
        }

        return new PatchHunk(oldStart, oldCount, newStart, newCount, start.Groups[5].Value, hunkLines);
    }

    // A count git leaves out is 1, and git leaves out every count of 1
    static int Count(Group group) => group.Success ? int.Parse(group.Value) : 1;

    static string Range(int start, int count) => count == 1 ? $"{start}" : $"{start},{count}";

    static char Prefix(PatchLineKind kind) =>
        kind switch
        {
            PatchLineKind.Context => ' ',
            PatchLineKind.Removed => '-',
            _ => '+',
        };

    static string At(string[] lines, int i) => i < lines.Length ? lines[i] : "(the end)";
}

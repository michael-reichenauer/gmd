using System.Text.RegularExpressions;

namespace gmdTest.Fixtures;

// Normalizes a screen captured from a tmux pane, so it can be asserted as a picture of what the
// user sees. GraphText does the same job for the graph column alone; this does it for the whole
// terminal.
//
// Most of the screen is already deterministic, because the fixture pins it rather than this
// masking it afterwards: E2eRepo pins the commit dates and identity (so the time column, and the
// commit ids derived from them, are the same on every machine), TmuxSession pins TZ and the
// locale, and the pane size is fixed so the column widths and the application bar's space filler
// cannot move. That is deliberate — an assertion on a real sid is worth more than one on a
// placeholder. What genuinely cannot be pinned is the temp repository path, which is a new guid
// every run, so that is what gets replaced.
//
// Use like e.g.:
//     ScreenText.AssertEqual(
//         """
//          Gmd {repo}, ●main                                            (main) [Ϙ Search] ? X
//         ────────────────────────────────────────────────────────────────────────────────────
//         ┣ ● Initial                                          0a02af Test User  24-10-15 12:00
//         """,
//         gmd.WaitFor("Initial"),
//         repo.Path);
static class ScreenText
{
    // What a replaced repository path is written as
    public const string RepoPlaceholder = "{repo}";

    // ApplicationBar.maxRepoLength, i.e. how much of the path the application bar keeps
    const int MaxRepoLength = 30;

    // 'yy-MM-dd HH:mm', as RepoWriter.WriteTime writes it
    static readonly Regex TimeRegex = new(@"\d\d-\d\d-\d\d \d\d:\d\d");

    // The whole screen: the repository path replaced, every line right trimmed and the trailing
    // blank lines dropped
    public static string Of(string capture, string repoPath = "") => Join(Lines(capture, repoPath));

    // A window of rows, for the screens where only part of it is the subject of the test
    public static string Rows(string capture, string repoPath, int first, int count) =>
        Join(Lines(capture, repoPath).Skip(first).Take(count));

    // Replaces the times in place, i.e. with a placeholder of the same width so the columns stay
    // aligned. For the screens that show a time which cannot be pinned, i.e. the uncommitted row,
    // whose time is DateTime.Now.
    public static string MaskTimes(string screen) => TimeRegex.Replace(screen, "NN-NN-NN NN:NN");

    // Asserts the screen matches. On a mismatch the actual screen is printed as a block that can
    // be read, checked and pasted straight back into the test, which is how these snapshots are
    // written and updated in the first place.
    public static void AssertEqual(string expected, string capture, string repoPath = "")
    {
        var actual = Of(capture, repoPath);
        if (actual == expected)
            return;

        var rule = new string('-', 100);
        Assert.Fail(
            $"The screen is not what was expected.\n\n"
                + $"Expected:\n{rule}\n{expected}\n{rule}\n\n"
                + $"Actual:\n{rule}\n{actual}\n{rule}\n"
        );
    }

    static IEnumerable<string> Lines(string capture, string repoPath)
    {
        var lines = Replace(capture, repoPath).Split('\n').Select(l => l.TrimEnd()).ToList();

        // A 40 row pane over a handful of commits is mostly empty, and dropping those rows is
        // what makes the expected value reviewable. Blank rows between content are kept.
        while (lines.Count > 0 && lines[^1] == "")
            lines.RemoveAt(lines.Count - 1);

        return lines;
    }

    static string Replace(string capture, string repoPath)
    {
        if (repoPath == "")
            return capture;

        // The application bar shows only the last 30 characters of the path, after a '┅'
        // (ApplicationBar.GetRepoPath). A temp repo path is longer than that, so the part that
        // says which fixture it is has been truncated away and nothing but the guid is left.
        if (repoPath.Length > MaxRepoLength)
            capture = capture.Replace($"┅{repoPath[^MaxRepoLength..]}", RepoPlaceholder);

        // The full path, as the commit details pane and the open repo menu show it
        return capture.Replace(repoPath, RepoPlaceholder);
    }

    static string Join(IEnumerable<string> lines) => string.Join("\n", lines);
}

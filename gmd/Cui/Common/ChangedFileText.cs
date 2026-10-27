namespace gmd.Cui.Common;

// How a changed file is written wherever files are listed, the commit details and the diff: by the
// letter the commit dialog uses, 'M' modified, 'A' new, 'D' deleted, 'R' renamed and 'C' conflicted,
// in the red the application bar says conflicts in, and the lines added and deleted
static class ChangedFileText
{
    public static Text Row(char kind, string path) => Letter(kind).White($" {path}");

    // The uncommitted files, by path, each as Row writes it: what Discard All Changes lists
    public static IReadOnlyList<Text> Rows(Server.Status status) =>
        [
            .. status
                .ModifiedFiles.Select(p => (Kind: 'M', Path: p))
                .Concat(status.AddedFiles.Select(p => (Kind: 'A', Path: p)))
                .Concat(status.DeletedFiles.Select(p => (Kind: 'D', Path: p)))
                .Concat(status.ConflictsFiles.Select(p => (Kind: 'C', Path: p)))
                .Concat(
                    status.RenamedSourceFiles.Zip(status.RenamedTargetFiles, (s, t) => (Kind: 'R', Path: $"{s} → {t}"))
                )
                .OrderBy(f => f.Path, StringComparer.Ordinal)
                .Select(f => Row(f.Kind, f.Path)),
        ];

    public static TextBuilder Letter(char kind) => Text.Color(ColorOf(kind), $"{kind}");

    public static Color ColorOf(char kind) =>
        kind switch
        {
            'A' => Color.Green,
            'D' => Color.Red,
            'C' => Color.BrightRed,
            _ => Color.Yellow,
        };

    // The lines added and deleted, each only when there are any, in the diff's green and red
    public static void AddCounts(TextBuilder text, int added, int deleted)
    {
        if (added > 0)
            text.Dark("  ").Green($"+{added}");
        if (deleted > 0)
            text.Dark(added > 0 ? " " : "  ").Red($"-{deleted}");
    }
}

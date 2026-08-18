using gmd.Common;
using gmd.Cui;
using gmd.Cui.Common;
using gmd.Server;

namespace gmdTest.Fixtures;

// Renders the branch graph of a view repo as plain text, i.e. what the UI draws in the graph
// column. Text.ToString() flattens the styled output, so the drawn graph can be asserted as a
// picture without a Terminal.Gui driver.
//
// Use like e.g.:
//     Assert.AreEqual(
//         """
//         ┣  Second
//         ┗  Initial
//         """,
//         GraphText.WithSubjects(repo));
static class GraphText
{
    // The graph column alone, one row per commit in view
    public static string Of(Repo repo, IRepoConfig? repoConfig = null) => Join(Rows(repo, repoConfig));

    // The graph column followed by the commit subject, which makes the picture self describing
    public static string WithSubjects(Repo repo, IRepoConfig? repoConfig = null)
    {
        var rows = Rows(repo, repoConfig);
        var width = rows.Any() ? rows.Max(r => r.Length) : 0;
        return Join(rows.Select((row, i) => $"{row.PadRight(width)}  {repo.ViewCommits[i].Subject}"));
    }

    // The color of every rune in the graph as one letter each, e.g. 'M' for the magenta main
    // branch, aligned below the runes of Of(). Branch colors are stable, so this shows which
    // branch each rune was drawn for.
    public static string ColorsOf(Repo repo, IRepoConfig? repoConfig = null) =>
        Join(RowTexts(repo, repoConfig).Select(TextColors.Of));

    static IReadOnlyList<string> Rows(Repo repo, IRepoConfig? repoConfig) =>
        RowTexts(repo, repoConfig).Select(t => t.ToString().TrimEnd()).ToList();

    // The drawn graph, one Text per commit in view, as the repo view writes them
    static IReadOnlyList<Text> RowTexts(Repo repo, IRepoConfig? repoConfig)
    {
        var branchColorService = new BranchColorService(repoConfig ?? new FakeRepoConfig());
        var graph = new GraphCreater(branchColorService).Create(repo);
        var writer = new GraphWriter();

        // Full width and no highlighted branch, i.e. nothing is truncated or given a background
        return repo.ViewCommits.Select((_, i) => writer.ToText(graph, i, graph.Width, "", false)).ToList();
    }

    static string Join(IEnumerable<string> rows) => string.Join("\n", rows);
}

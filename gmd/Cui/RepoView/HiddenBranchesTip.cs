namespace gmd.Cui.RepoView;

// What gmd says on the status line the first time it shows a repository with hidden branches: which
// branches are shown, how many are hidden, and how to show one. A repository opens showing main and
// the current branch, and every other branch is a dark ╮ or ╯ beside a commit, which someone new can
// take for branches gone missing (docs/USABILITY.md, the product review).
//
// Once per user rather than per repository: the idea is learned once, and after that the marks, the
// key hints and ✦ say the rest, where a tip on every first open would cover the hints each time.
static class HiddenBranchesTip
{
    // The tip, or null when nothing is hidden, which leaves it to be told in a repository that has
    public static string? Of(Server.Repo repo)
    {
        var hidden = repo.AllBranches.Count(b => b.IsPrimary && b.IsGitBranch && !b.IsInView);
        if (hidden == 0)
            return null;

        var shown = repo.ViewBranches.Where(b => b.IsPrimary).Select(b => b.NiceName).Distinct().ToList();
        var showing = shown.Count switch
        {
            1 => shown[0],
            2 or 3 => $"{string.Join(", ", shown[..^1])} and {shown[^1]}",
            _ => $"{shown.Count} branches",
        };
        var others = hidden == 1 ? "1 other branch is hidden" : $"{hidden} other branches are hidden";

        return $"Showing {showing}; {others}: ⇧→ shows one, as does a click on a dark ╮ or ╯";
    }
}

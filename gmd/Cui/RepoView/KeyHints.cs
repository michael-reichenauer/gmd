using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;

// The log view's key-hint line, see KeyHintLine: the few keys that do something useful where the
// cursor is. What is useful depends on what is under the cursor (a commit, the uncommitted changes, a
// highlighted branch or several selected rows) and on the repo, e.g. 'p push' only while there is
// something to push, and 'c continue' rather than 'c commit' during a rebase. The line starts with
// what the keys act on ('dev:' for a highlighted branch, 'commit on dev:' for the row), so it is the
// last to go, then 'm menu', since every command is in a menu.
//
// This is the decision, with no view, so it is tested without a terminal.
static class KeyHints
{
    static readonly KeyHint Menu = KeyHintLine.Menu;

    public static IReadOnlyList<KeyHint> For(IViewRepo repo, Hoover hoover, Selection selection, bool isDetailsShown)
    {
        if (selection.IsRows)
            return ForSelectedRows();

        return hoover.IsBranch
            ? ForBranch(repo, hoover, selection)
            : ForCommit(repo, hoover, selection, isDetailsShown);
    }

    // While the filter dialog is up, which has the keyboard, with the log view showing its results
    public static IReadOnlyList<KeyHint> ForFilter() =>
        [
            new("↑↓", "select"),
            new("Enter", "show in the log"),
            new("Esc", "back"),
            new("file:", "search changed files"),
            new("change:", "search changes"),
        ];

    // The ranges the menu and 'd' work with: diff, squash and cherry-pick of the rows, and copying
    // them
    static IReadOnlyList<KeyHint> ForSelectedRows() => [Menu, new("d", "diff"), new("Ctrl-C", "copy")];

    static IReadOnlyList<KeyHint> ForCommit(IViewRepo repo, Hoover hoover, Selection selection, bool isDetailsShown)
    {
        var status = repo.Status;
        List<KeyHint> hints = [new LabelHint(RowName(repo).White(":")), Menu];

        if (!status.IsOk)
            hints.Add(CommitHint(status));
        if (status.IsMerging)
            hints.Add(AbortHint);
        Add(hints, "d", LogKeys.Diff(repo, hoover, selection));
        hints.Add(new("Enter", isDetailsShown ? "hide details" : "details"));
        Add(hints, "p", LogKeys.Push(repo, hoover));
        Add(hints, "u", LogKeys.Pull(repo, hoover));
        if (repo.Graph.GetRowBranches(repo.CurrentIndex).Any())
            hints.Add(new("←→", "branch"));
        hints.Add(new("⇧→", "show branch"));
        if (repo.SearchMatches.IsActive)
            hints.Add(new("n", "next match"));
        hints.Add(new("f", "search"));
        Add(hints, "b", LogKeys.NewBranch(repo, hoover));

        return hints;
    }

    // A branch hoovered with ← → or the mouse, named first, since it is what the keys act on and
    // the mouse can move the hoover without a key being pressed
    static IReadOnlyList<KeyHint> ForBranch(IViewRepo repo, Hoover hoover, Selection selection)
    {
        var status = repo.Status;
        var primary = repo.Repo.BranchByName[hoover.BranchPrimaryName];
        var branch = LogKeys.HooveredLocal(repo, hoover);

        // The menu and Enter act on the hoovered branch only where it crosses the current row, see
        // RepoViewInput.OnMenu and OnKeyEnter; the mouse can hoover one anywhere
        var isOnRow = repo
            .Graph.GetRowBranches(repo.CurrentIndex)
            .Any(b => b.B.PrimaryName == hoover.BranchPrimaryName);
        var color = repo.Graph.BranchByName(hoover.BranchName).Color;
        List<KeyHint> hints = [new LabelHint(Text.Color(color, primary.NiceNameUnique).White(":"))];
        if (isOnRow)
            hints.Add(Menu);

        // Another branch is switched to and merged, the current one merged from and to others
        Add(hints, "s", LogKeys.Switch(repo, hoover));
        Add(hints, "e", LogKeys.Merge(repo, hoover));
        Add(hints, "⇧e", LogKeys.MergeInto(repo, hoover));

        // Enter shows or hides the branches meeting at the commit, the branch itself too at its tip
        // (RepoViewInput.TryShowHideCommitBranch). The main branch is always shown, so where it is
        // the only one, Enter does nothing to offer.
        if (isOnRow && repo.GetCommitBranches(true).Any(b => !b.IsMainBranch))
            hints.Add(new("Enter", "show/hide"));
        if (!branch.IsMainBranch)
            hints.Add(new("h", "hide"));
        Add(hints, "d", LogKeys.Diff(repo, hoover, selection));
        Add(hints, "p", LogKeys.Push(repo, hoover));
        Add(hints, "u", LogKeys.Pull(repo, hoover));
        if (!status.IsOk)
            hints.Add(CommitHint(status));
        if (status.IsMerging)
            hints.Add(AbortHint);
        Add(hints, "b", LogKeys.NewBranch(repo, hoover));

        return hints;
    }

    // A key whose decision is LogKeys', hinted when it acts and says so
    static void Add(List<KeyHint> hints, string key, KeyAction action)
    {
        if (action.IsHinted)
            hints.Add(new(key, action.Hint));
    }

    // The way out of an operation git stopped part way through, in the repo menu, which 'M' opens
    static readonly KeyHint AbortHint = new("⇧m", "abort…");

    // 'c' finishes a merge, or a cherry pick or revert made in gmd, by committing, but anything
    // else git stopped part way through has to be continued instead, and 'c' offers that
    static KeyHint CommitHint(Status status) =>
        new("c", status.IsMerging && !status.IsFinishedByCommit ? "continue" : "commit");

    // What the keys act on with no branch highlighted: the commit on the row, or on the uncommitted
    // row the changes, with the branch it is on, in the color of the branch's line in the graph.
    // The application bar names it instead when the key-hint line is turned off.
    public static TextBuilder RowName(IViewRepo repo)
    {
        var commit = repo.RowCommit;
        var branch = repo.Graph.BranchByName(commit.BranchName);
        return Text.White(commit.IsUncommitted ? "changes on " : "commit on ")
            .Color(branch.Color, branch.B.NiceNameUnique);
    }
}

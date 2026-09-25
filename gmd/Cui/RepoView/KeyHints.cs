using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;

record KeyHint(string Key, string Text);

// The key-hint line at the bottom of the log view: the few keys that do something useful where
// the cursor is, so that the keys are learned by using gmd rather than by reading the help first.
// What is useful depends on what is under the cursor (a commit, the uncommitted changes, a
// highlighted branch or several selected rows) and on the repo, e.g. 'p push' only while there is
// something to push, and 'c continue' rather than 'c commit' during a rebase.
//
// A key is written the way it is typed, and a shifted one with '⇧', so '⇧p' is Shift-P, which is a
// different command from 'p'.
// The hints are listed most useful first, which is also the order they are dropped in, from the
// end, when the line is too narrow for all of them. 'm menu' leads, since every command is in a
// menu, and '? help' is always kept, at the right.
//
// The line is drawn as the log's bottom border, in the color of the line under the application bar,
// with the hints set into it, so that it reads as the frame of the log and not as one more row of
// it, which it did when it was the hints on their own.
//
// This is the decision and the layout, with no view, so it is tested without a terminal; the
// line itself is drawn by KeyHintBar.
static class KeyHints
{
    const string Gap = "  ";
    static readonly KeyHint Help = new("?", "help");
    static readonly KeyHint Menu = new("m", "menu");

    // The border runs into the line for this long before the hints, and after the help
    const string Lead = "──";
    const string Tail = "──";

    public static IReadOnlyList<KeyHint> For(IViewRepo repo, Hoover hoover, Selection selection, bool isDetailsShown)
    {
        if (selection.I2 > selection.I1)
            return ForSelectedRows();

        return hoover.IsBranch ? ForBranch(repo, hoover) : ForCommit(repo, isDetailsShown);
    }

    // While the filter dialog is up, which has the keyboard, with the log view showing its results
    public static IReadOnlyList<KeyHint> ForFilter() =>
        [
            new("↑↓", "select"),
            new("Enter", "show in the log"),
            new("Esc", "back"),
            new("file:", "search changed files"),
        ];

    // The hints on one line of the given width, set into the border: as many as fit, from the left,
    // and help at the right, with the border between them
    //
    //   ── m menu  d diff  f search ──────────────── ? help ──
    public static Text ToText(IReadOnlyList<KeyHint> hints, int width)
    {
        // The border and its spaces around the hints, the help and the border around it
        var frame = Lead.Length + 1 + 1 + 1 + 1 + Length(Help) + 1 + Tail.Length;
        var shown = hints.ToList();
        while (shown.Count > 0 && frame + Length(shown) > width)
            shown.RemoveAt(shown.Count - 1);

        var text = new TextBuilder().Color(Color.BrightMagenta, Lead);
        if (shown.Count > 0)
        {
            text.Dark(" ");
            for (int i = 0; i < shown.Count; i++)
                Add(i == 0 ? text : text.Dark(Gap), shown[i]);
            text.Dark(" ");
        }

        var border = width - text.Length - 1 - Length(Help) - 1 - Tail.Length;
        if (border < 0)
            return text; // Not even the help fits

        text.Color(Color.BrightMagenta, new string('─', border)).Dark(" ");
        Add(text, Help);
        return text.Dark(" ").Color(Color.BrightMagenta, Tail);
    }

    // A status message, in place of the hints and set into the border the same way: green for what a
    // command did, yellow for why a key did nothing, red for what failed in the background. Cut to
    // the line if it is longer.
    //
    //   ── Pushed 'main' ─────────────────────────────────────
    public static Text ToText(StatusMessage message, int width)
    {
        var room = Math.Max(0, width - Lead.Length - 2 - Tail.Length);
        var text = message.Text.ReplaceLineEndings(" ");
        text = text.Length > room ? text[..Math.Max(0, room - 1)] + "┅" : text;
        var color = message.Kind switch
        {
            StatusKind.Info => Color.Green,
            StatusKind.Notice => Color.Yellow,
            _ => Color.BrightRed,
        };

        var border = Math.Max(0, width - Lead.Length - 2 - text.Length);
        return new TextBuilder()
            .Color(Color.BrightMagenta, Lead)
            .Dark(" ")
            .Color(color, text)
            .Dark(" ")
            .Color(Color.BrightMagenta, new string('─', border));
    }

    // The ranges the menu and 'd' work with: diff, squash and cherry-pick of the rows, and copying
    // them
    static IReadOnlyList<KeyHint> ForSelectedRows() => [Menu, new("d", "diff"), new("Ctrl-C", "copy")];

    static IReadOnlyList<KeyHint> ForCommit(IViewRepo repo, bool isDetailsShown)
    {
        var status = repo.Status;
        var isUncommitted = repo.RowCommit.IsUncommitted;
        var isConflicts = isUncommitted && status.Conflicted > 0;
        List<KeyHint> hints = [Menu];

        if (!status.IsOk)
            hints.Add(CommitHint(status));
        if (status.IsMerging)
            hints.Add(AbortHint);
        // On the uncommitted row with conflicts, the diff is where they are resolved, with Enter
        hints.Add(new("d", isConflicts ? "resolve" : "diff"));
        hints.Add(new("Enter", isDetailsShown ? "hide details" : "details"));
        hints.AddRange(PushPullHints(repo));
        if (repo.Graph.GetRowBranches(repo.CurrentIndex).Any())
            hints.Add(new("←→", "branch"));
        hints.Add(new("⇧→", "show branch"));
        hints.AddRange(UndoHint(repo));
        if (repo.SearchMatches.IsActive)
            hints.Add(new("n", "next match"));
        hints.Add(new("f", "search"));
        if (!isUncommitted)
            hints.Add(new("b", "new branch"));

        return hints;
    }

    // A branch hoovered with ← → or the mouse, named first, since it is what the keys act on and
    // the mouse can move the hoover without a key being pressed
    static IReadOnlyList<KeyHint> ForBranch(IViewRepo repo, Hoover hoover)
    {
        var status = repo.Status;
        var primary = repo.Repo.BranchByName[hoover.BranchPrimaryName];
        var branch = primary.LocalName != "" ? repo.Repo.BranchByName[primary.LocalName] : primary;

        // The menu and Enter act on the hoovered branch only where it crosses the current row, see
        // RepoViewInput.OnMenu and OnKeyEnter; the mouse can hoover one anywhere
        var isOnRow = repo
            .Graph.GetRowBranches(repo.CurrentIndex)
            .Any(b => b.B.PrimaryName == hoover.BranchPrimaryName);
        List<KeyHint> hints = [Label($"{primary.NiceNameUnique}:")];
        if (isOnRow)
            hints.Add(Menu);

        if (branch.IsCurrent)
        {
            if (status.IsOk)
                hints.AddRange([new("e", "merge from"), new("⇧e", "merge to")]);
        }
        else
        {
            hints.Add(new("s", "switch"));
            if (status.IsOk)
                hints.Add(new("e", "merge"));
        }

        // Enter shows or hides the branches meeting at the commit, the branch itself too at its tip
        // (RepoViewInput.TryShowHideCommitBranch). The main branch is always shown, so where it is
        // the only one, Enter does nothing to offer.
        if (isOnRow && repo.GetCommitBranches(true).Any(b => !b.IsMainBranch))
            hints.Add(new("Enter", "show/hide"));
        if (!branch.IsMainBranch)
            hints.Add(new("h", "hide"));
        hints.AddRange(UndoHint(repo));
        if (status.IsOk)
            hints.Add(new("d", "diff"));
        if (branch.IsCurrent)
        {
            hints.AddRange(PushPullHints(repo));
        }
        else
        { // The rules of the branch menu's Push and Pull, which the keys follow for this branch
            if (BranchPushPullCommands.CanPushBranch(repo.Repo, primary))
                hints.Add(new("p", "push"));
            if (BranchPushPullCommands.CanPullBranch(repo.Repo, primary))
                hints.Add(new("u", "pull"));
        }
        if (!status.IsOk)
            hints.Add(CommitHint(status));
        if (status.IsMerging)
            hints.Add(AbortHint);
        hints.Add(new("b", "new branch"));

        return hints;
    }

    // The way out of an operation git stopped part way through, in the repo menu, which 'M' opens
    static readonly KeyHint AbortHint = new("⇧m", "abort…");

    // 'c' finishes a merge, or a cherry pick or revert made in gmd, by committing, but anything
    // else git stopped part way through has to be continued instead, and 'c' offers that
    static KeyHint CommitHint(Status status) =>
        new("c", status.IsMerging && !status.IsFinishedByCommit ? "continue" : "commit");

    static IEnumerable<KeyHint> PushPullHints(IViewRepo repo)
    {
        if (BranchPushPullCommands.CanPushCurrentBranch(repo.Repo))
            yield return new("p", "push");
        if (BranchPushPullCommands.CanPullCurrentBranch(repo.Repo))
            yield return new("u", "pull");
    }

    // Backspace, once a branch has been shown or hidden, saying which of the two it goes back from
    static IEnumerable<KeyHint> UndoHint(IViewRepo repo)
    {
        if (repo.ShownHistory.Last is ShownChange last)
            yield return new("Bksp", $"undo {last.Verb.ToLowerInvariant()}");
    }

    // A hint with no key is a label, e.g. the name of the branch the keys act on
    static KeyHint Label(string text) => new("", text);

    static void Add(TextBuilder text, KeyHint hint)
    {
        if (hint.Key == "")
            text.White(hint.Text);
        else
            text.Cyan(hint.Key).Dark($" {hint.Text}");
    }

    static int Length(KeyHint hint) => hint.Key == "" ? hint.Text.Length : hint.Key.Length + 1 + hint.Text.Length;

    static int Length(IReadOnlyList<KeyHint> hints) => hints.Sum(Length) + Math.Max(0, hints.Count - 1) * Gap.Length;
}

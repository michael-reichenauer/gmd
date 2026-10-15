using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Commits;

// The rows of the commit details pane, which Enter opens below the log and the blame: the commit's
// id, branch, author and relatives, and its message. With no view, so they are tested without a
// terminal; CommitDetailsView draws them.
//
// The pane is ten rows high, so the fields are kept to four, and the message, which is what the pane
// is mostly opened for, has the rest. Nothing is said twice: the repo is in the application bar, and
// the branch tips are in the log row, so only a branch other than the commit's own is named.
static class CommitDetailsRows
{
    // The labels are padded to one width, so the values start in one column
    const int LabelWidth = 10;

    // At most this many of the branches an ambiguous commit could be on are named
    const int MaxAmbiguousNames = 3;

    public static TextBuilder Label(string label) => Text.Dark(label.PadRight(LabelWidth));

    // At most this many of the files a commit changed are listed, which is plenty to see what it did
    const int MaxFiles = 500;

    // The files are those the commit changed, null while they are not read yet. Those of the
    // uncommitted changes are in the repo's status already.
    public static IReadOnlyList<Text> Of(
        Repo repo,
        Commit commit,
        Branch branch,
        IBranchColorService colors,
        IReadOnlyList<ChangedFile>? files = null
    )
    {
        List<Text> rows = [IdRow(commit), BranchRow(repo, commit, branch, colors)];
        if (commit.Author != "")
            rows.Add(Label("Author:").White(commit.Author).Dark(", ").White(commit.AuthorTime.IsoZone()));
        rows.Add(RelativesRow(repo, commit, branch, colors));
        rows.Add(Text.Empty);
        rows.AddRange(commit.Message.Split('\n').Select(l => Text.White(l).ToText()));
        if (commit.Id == Repo.UncommittedId)
            rows.AddRange(UncommittedFiles(repo.Status));
        else if (files != null)
            rows.AddRange(ChangedFiles(files));

        return rows;
    }

    // Whether the commit has files of its own to read, i.e. is a commit of the repo, and not the
    // uncommitted changes, whose files are in the status, or a row that stands for no commit
    public static bool HasChangedFiles(Commit commit) =>
        commit.Id != Repo.UncommittedId
        && commit.Id != Repo.EmptyRepoCommitId
        && commit.Id != Repo.TruncatedLogCommitId;

    // The files the commit changed, below its message as 'git show --stat' has them: how many and
    // how many lines, then each file with the lines added and deleted in it
    static IEnumerable<Text> ChangedFiles(IReadOnlyList<ChangedFile> files)
    {
        if (files.Count == 0)
            return [Text.Empty, Text.Dark("No files changed")];

        var summary = Text.White($"{files.Count} file{(files.Count == 1 ? "" : "s")} changed");
        AddCounts(summary, files.Sum(f => f.Added), files.Sum(f => f.Deleted));
        List<Text> rows = [Text.Empty, summary];

        foreach (var f in files.Take(MaxFiles))
        {
            var row = FileRow(f.Kind, f.OldPath == "" ? f.Path : $"{f.OldPath} → {f.Path}").ToTextBuilder();
            if (f.IsBinary)
                row.Dark("  binary");
            else
                AddCounts(row, f.Added, f.Deleted);
            rows.Add(row);
        }
        if (files.Count > MaxFiles)
            rows.Add(Text.Dark($"┅ {files.Count - MaxFiles} more"));

        return rows;
    }

    // The lines added and deleted, each only when there are any, in the diff's green and red
    static void AddCounts(TextBuilder text, int added, int deleted)
    {
        if (added > 0)
            text.Dark("  ").Green($"+{added}");
        if (deleted > 0)
            text.Dark(added > 0 ? " " : "  ").Red($"-{deleted}");
    }

    // The files with changes not committed, below the count of them: the conflicted first, since
    // resolving them is what has to be done next, and then the rest as the commit dialog lists them
    static IEnumerable<Text> UncommittedFiles(Status status)
    {
        var conflicts = status.Conflicts.Select(c => c.Path).Order(StringComparer.Ordinal).ToList();
        var files = new CommitFiles(status).Files;
        if (conflicts.Count == 0 && files.Count == 0)
            return [];

        return
        [
            Text.Empty,
            .. conflicts.Select(path => FileRow('C', path)),
            .. files.Select(f => FileRow(f.Kind, f.Path)),
        ];
    }

    // A file and what changed in it, by the letter the commit dialog uses: 'M' modified, 'A' new,
    // 'D' deleted, 'R' renamed, and 'C' conflicted, in the red the application bar says conflicts in
    static Text FileRow(char kind, string path)
    {
        var letter = $"{kind}";
        var text = kind switch
        {
            'A' => Text.Green(letter),
            'D' => Text.Red(letter),
            'C' => Text.BrightRed(letter),
            _ => Text.Yellow(letter),
        };
        return text.White($" {path}");
    }

    // The full id, which is the one to copy into another tool, and the tags, as the log writes them
    static Text IdRow(Commit commit)
    {
        if (commit.Id == Repo.UncommittedId)
            return Label("Id:").BrightYellow("Uncommitted");

        var text = Label("Id:").White(commit.Id);
        if (commit.Tags.Count > 0)
            text.Green("  [" + string.Join("][", commit.Tags.Select(t => t.Name)) + "]");
        return text;
    }

    // The branch the commit is on, in its color, and what more there is to know of it there: not
    // pushed or not pulled yet, which branches it could be on when that is ambiguous, whether the user
    // chose it, whether the branch is deleted, and the other branches whose tip it is
    static Text BranchRow(Repo repo, Commit commit, Branch branch, IBranchColorService colors)
    {
        var text = Label("Branch:");
        if (commit.IsAmbiguous && !commit.IsBranchSetByUser)
        {
            var names = string.Join(", ", branch.AmbiguousBranchNames.Take(MaxAmbiguousNames));
            if (branch.AmbiguousBranchNames.Count > MaxAmbiguousNames)
                names += ",┅";
            text.White(NameOf(branch)).Dark("  ambiguous, could be: ").White(names);
        }
        else
        {
            text.Color(colors.GetColor(repo, branch), NameOf(branch));
            if (!branch.IsGitBranch)
                text.Dark("  (deleted)");
        }

        if (commit.IsBranchSetByUser)
            text.White("  Φ").Dark(" (ambiguity resolved by user)");
        if (commit.IsAhead)
            text.Green("  ▲ pushable");
        if (commit.IsBehind)
            text.Blue("  ▼ pullable");

        var otherTips = repo
            .ViewBranches.Where(b => b.TipId == commit.Id && b.NiceNameUnique != branch.NiceNameUnique)
            .DistinctBy(b => b.NiceNameUnique)
            .ToList();
        if (otherTips.Count > 0)
            text.Dark("  also tip of ").Add(Names(repo, otherTips, colors));

        return text;
    }

    // The parents in git's order and the children, by their short ids. A relative on another branch
    // than the commit's is named with its branch, so a merge says what was merged in, and the first
    // commit of a branch where it was started from.
    static Text RelativesRow(Repo repo, Commit commit, Branch branch, IBranchColorService colors)
    {
        var children = commit.AllChildIds.Where(id => id != Repo.UncommittedId).ToList();

        var text = Label("Parents:");
        text.Add(
            commit.GitParentIds.Count > 0 ? Relatives(repo, commit.GitParentIds, branch, colors) : Text.Dark("none")
        );
        if (children.Count > 0)
            text.Dark("    Children: ").Add(Relatives(repo, children, branch, colors));
        return text;
    }

    static Text Relatives(Repo repo, IEnumerable<string> ids, Branch branch, IBranchColorService colors)
    {
        var text = new TextBuilder();
        foreach (var (id, i) in ids.Select((id, i) => (id, i)))
        {
            if (i > 0)
                text.Dark(", ");
            text.White(id.Sid());
            if (
                repo.CommitById.TryGetValue(id, out var relative)
                && repo.BranchByName.TryGetValue(relative.BranchName, out var relativeBranch)
                && relativeBranch.NiceNameUnique != branch.NiceNameUnique
            )
            {
                text.Dark(" ").Color(colors.GetColor(repo, relativeBranch), NameOf(relativeBranch));
            }
        }
        return text;
    }

    static Text Names(Repo repo, IEnumerable<Branch> branches, IBranchColorService colors)
    {
        var text = new TextBuilder();
        foreach (var (b, i) in branches.Select((b, i) => (b, i)))
        {
            if (i > 0)
                text.Dark(", ");
            text.Color(colors.GetColor(repo, b), NameOf(b));
        }
        return text;
    }

    // A detached HEAD is a branch to gmd, named DETACHED, but not to the user
    static string NameOf(Branch branch) => branch.IsDetached ? "detached HEAD" : branch.NiceNameUnique;
}

using System.Security.Cryptography;
using System.Text;
using gmd.Server;

namespace gmd.Cui.Common;

// Picks the color of a branch the first time gmd sees it, which is then remembered
// (RepoConfig.BranchColors, see BranchColorService) and never picked again: a branch keeps its color
// whatever is shown, hidden, pushed, merged or deleted around it, until the user changes it ('g'). A
// color is the index of one of the palette's branch colors.
//
// A new branch takes the color its name hashes to, unless that is its parent's color, or a live
// sibling (a branch of the same parent) has it: then the color its siblings used least recently, i.e.
// the one whose latest sibling to have it was started longest ago, a color no sibling has first.
// Branches started close together in time are the ones drawn side by side, so they get different
// colors, and as many of the colors get used as there are siblings. Picked among the colors of the
// palette in use, twelve, or five of the 16 colors; a color picked in the other is drawn as its index
// modulo the count.
static class BranchColorPicker
{
    // The colors of the live branches, by Key, all but main's, which is always magenta: those
    // remembered, and a new pick for the others, parents before their children and siblings in the
    // order they were started, so that each is picked knowing the colors of those before it
    public static Dictionary<string, int> Pick(Repo repo, IReadOnlyDictionary<string, int> remembered, int count)
    {
        var branches = repo
            .AllBranches.Where(b => b.IsPrimary && b.IsGitBranch && !b.IsMainBranch && !b.IsDetached)
            .ToList();
        var colors = new Dictionary<string, int>();

        // A color is remembered by the key, or by the branch's name as an older gmd kept the colors
        // set with 'g', i.e. as 'origin/name' for a pushed branch, which the key is from now on
        foreach (var b in branches)
        {
            if (
                (remembered.TryGetValue(Key(b), out var color) || remembered.TryGetValue(b.Name, out color))
                && color >= 0
            )
                colors.TryAdd(Key(b), color);
        }

        var siblingsByParent = branches.ToLookup(b => ParentName(repo, b));
        var newBranches = branches
            .Where(b => !colors.ContainsKey(Key(b)))
            .OrderBy(b => b.AncestorNames.Count)
            .ThenBy(b => StartOf(repo, b))
            .ThenBy(b => b.Name, StringComparer.Ordinal);
        foreach (var b in newBranches)
        {
            if (!colors.ContainsKey(Key(b))) // Two branches can share a key, see Key
                colors[Key(b)] = PickNew(repo, b, siblingsByParent[ParentName(repo, b)], colors, count);
        }

        return colors;
    }

    // The key a branch's color is remembered by: its primary branch's name with no 'origin/', which
    // stays the same as the branch is pushed, and as its remote branch is deleted, e.g. once a pull
    // request of it is merged. A local branch not tracking the remote branch of its name shares the
    // remote branch's color, which is the same branch to the user.
    public static string Key(Repo repo, Branch branch) =>
        Key(repo.BranchByName.TryGetValue(branch.PrimaryName, out var primary) ? primary : branch);

    // The color a branch's name hashes to, which it is given unless it collides, see PickNew
    public static int FirstChoice(Repo repo, Branch branch, int count) =>
        Hash(repo.BranchByName.TryGetValue(branch.PrimaryName, out var primary) ? primary.Name : branch.Name) % count;

    static string Key(Branch primary) => primary.NiceName;

    // The color for a new branch, given the colors of the branches before it: the first one it hashes
    // to, except its parent's, that no sibling has, else that its siblings used least recently
    static int PickNew(
        Repo repo,
        Branch branch,
        IEnumerable<Branch> siblings,
        IReadOnlyDictionary<string, int> colors,
        int count
    )
    {
        var latestStart = Enumerable.Repeat(DateTime.MinValue, count).ToArray();
        foreach (var s in siblings)
        {
            if (Key(s) != Key(branch) && colors.TryGetValue(Key(s), out var color))
            {
                var start = StartOf(repo, s);
                if (start > latestStart[color % count])
                    latestStart[color % count] = start;
            }
        }

        var parentColor = ParentColor(repo, branch, colors) is int pc ? pc % count : -1;
        var first = FirstChoice(repo, branch, count);
        var picked = -1;
        for (int i = 0; i < count; i++)
        {
            var color = (first + i) % count;
            if (color == parentColor)
                continue;
            if (picked == -1 || latestStart[color] < latestStart[picked])
                picked = color;
        }

        return picked;
    }

    // The color of the branch's parent, if it has one of the branch colors: main's is magenta, and a
    // deleted parent's gray, neither of which a branch can be given
    static int? ParentColor(Repo repo, Branch branch, IReadOnlyDictionary<string, int> colors) =>
        repo.BranchByName.TryGetValue(ParentName(repo, branch), out var parent)
        && parent.IsGitBranch
        && !parent.IsMainBranch
        && colors.TryGetValue(Key(parent), out var color)
            ? color
            : null;

    // The name of the primary branch of the branch's parent, "" for a branch with none
    static string ParentName(Repo repo, Branch branch) =>
        repo.BranchByName.TryGetValue(branch.ParentBranchName, out var parent) ? parent.PrimaryName : "";

    // When a branch was started: the time of its first commit, which new commits on it, and a rebase,
    // leave as it is. A branch with no commits of its own yet, i.e. just made, is the latest.
    static DateTime StartOf(Repo repo, Branch branch) =>
        repo.CommitById.TryGetValue(branch.BottomId, out var bottom)
        && !bottom.IsUncommitted
        && bottom.BranchPrimaryName == branch.PrimaryName
            ? bottom.AuthorTime
            : DateTime.MaxValue;

    // A hash of a name, the same in every gmd and on every machine, since the first color of a branch
    // is picked by it
    static int Hash(string name)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return (int)Math.Abs((long)BitConverter.ToInt32(bytes, 0)); // A long, since -int.MinValue is no int
    }
}

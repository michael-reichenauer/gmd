using System.Runtime.CompilerServices;
using gmd.Common;
using gmd.Server;

namespace gmd.Cui.Common;

// Manages brach colors, each branch has a color that is used in the UI.
// A branch's color is picked the first time gmd sees the branch, unlike its parent's and its
// siblings' (see BranchColorPicker), and remembered in the repo config from then on, so it stays the
// same whatever happens around it, until the user changes it, which is remembered the same way.
// The colors of the branches that are gone are forgotten, so the config holds the live ones only.
// Branches that are no longer active (deleted branches, which gmd inferred from merge commits)
// are gray, so the active branches stand out. That color cannot be changed by the user.
interface IBranchColorService
{
    Color GetColor(Repo repo, Branch branch);
    void ChangeColor(Repo repo, Branch branch);

    // Remembers the colors of the branches new in the repo, and forgets those of the branches gone
    void Remember(Repo repo);
}

[SingleInstance]
class BranchColorService : IBranchColorService
{
    // The palette's, twelve of RGB, or five of the 16 colors, those that mean nothing else (main,
    // deleted, ahead and behind have the others); read as gmd draws, as the palette can change once
    // the terminal has said what its background is
    static IReadOnlyList<Color> BranchColors => Palette.Current.BranchColors;

    readonly IRepoConfigService repoConfig;

    // The colors of each repo, i.e. of each snapshot of it, which every branch drawn asks for, picked
    // once per snapshot and in the palette it was picked in
    readonly ConditionalWeakTable<Repo, Picked> pickedByRepo = new();

    internal BranchColorService(IRepoConfigService repoConfig)
    {
        this.repoConfig = repoConfig;
    }

    public Color GetColor(Repo repo, Branch branch)
    {
        if (branch.IsDetached)
            return Color.White;
        if (branch.IsMainBranch)
            return Color.Magenta;
        if (!branch.IsGitBranch)
            return Color.Dark; // Branch is no longer active (deleted), it is dimmed

        return BranchColor(ColorIdOf(repo, branch));
    }

    public void ChangeColor(Repo repo, Branch branch)
    {
        var key = BranchColorPicker.Key(repo, branch);
        var newColorId = (ColorIdOf(repo, branch) % BranchColors.Count + 1) % BranchColors.Count;

        ColorsOf(repo)[key] = newColorId; // In case the repo is drawn again before it is refreshed
        repoConfig.Set(repo.Path, s => s.BranchColors[key] = newColorId);
    }

    public void Remember(Repo repo)
    {
        // A search's results hold only some of the branches, and the others would be forgotten
        if (repo.Filter != "")
            return;

        var colors = ColorsOf(repo);
        var remembered = repoConfig.Get(repo.Path).BranchColors;
        if (colors.Count == remembered.Count && colors.All(c => remembered.GetValueOrDefault(c.Key, -1) == c.Value))
            return;

        // Picked again from the config as it is written, in case another gmd changed it since
        var count = BranchColors.Count;
        repoConfig.Set(repo.Path, s => s.BranchColors = BranchColorPicker.Pick(repo, s.BranchColors, count));
    }

    // A live branch's color: as picked for the repo, or, for a branch that is not in it, as it would
    // be picked if it had no parent nor siblings
    int ColorIdOf(Repo repo, Branch branch) =>
        ColorsOf(repo).TryGetValue(BranchColorPicker.Key(repo, branch), out var colorId)
            ? colorId
            : BranchColorPicker.FirstChoice(repo, branch, BranchColors.Count);

    Dictionary<string, int> ColorsOf(Repo repo)
    {
        var count = BranchColors.Count;
        if (pickedByRepo.TryGetValue(repo, out var picked) && picked.Count == count)
            return picked.Colors;

        var colors = BranchColorPicker.Pick(repo, repoConfig.Get(repo.Path).BranchColors, count);
        pickedByRepo.AddOrUpdate(repo, new Picked(count, colors));
        return colors;
    }

    static Color BranchColor(int colorId) => BranchColors[colorId % BranchColors.Count];

    record Picked(int Count, Dictionary<string, int> Colors);
}

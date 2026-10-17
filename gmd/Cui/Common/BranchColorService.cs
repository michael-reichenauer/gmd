using System.Security.Cryptography;
using System.Text;
using gmd.Common;
using gmd.Server;

namespace gmd.Cui.Common;

// Manages brach colors, each branch has a color that is used in the UI.
// By default, the color is based on the branch primary name (hashed id), but also if the color.
// is the same as the parent branch, it is changed to a different color.
// The user can manually change the color of a branch, and the color is stored in the state
// until user changes again.
// Branches that are no longer active (deleted branches, which gmd inferred from merge commits)
// are gray, so the active branches stand out. That color cannot be changed by the user.
interface IBranchColorService
{
    Color GetColor(Repo repo, Branch branch);
    void ChangeColor(Repo repo, Branch branch);
}

class BranchColorService : IBranchColorService
{
    // The palette's, twelve of RGB, or five of the 16 colors, those that mean nothing else (main,
    // deleted, ahead and behind have the others); read as gmd draws, as the palette can change once
    // the terminal has said what its background is
    static IReadOnlyList<Color> BranchColors => Palette.Current.BranchColors;

    readonly IRepoConfigService repoConfig;

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

        if (repoConfig.Get(repo.Path).BranchColors.TryGetValue(branch.PrimaryName, out var colorId))
        { // Branch has a color set by user, use it
            return GetColorByColorId(colorId);
        }

        if (branch.ParentBranchName == "")
        { // branch has no parent, get color based on branch name
            return GetColorByName(branch.PrimaryName);
        }

        // Branch has a parent, lets check the color of parent to determine branch color
        var parentBranch = repo.BranchByName[branch.ParentBranchName];

        if (branch.PrimaryName == parentBranch.PrimaryName)
        { // Same common name, lets use parent color
            return GetColor(repo, parentBranch);
        }

        // Parent is a different branch lets use a colors that is different
        Color color = GetColorByName(branch.PrimaryName);
        Color parentColor = GetColor(repo, parentBranch);
        if (color == parentColor)
        { // branch got same color as parent, lets change branch color one step
            color = GetColorByName(branch.PrimaryName, 1);
        }

        return color;
    }

    public void ChangeColor(Repo repo, Branch branch)
    {
        var color = GetColor(repo, branch);
        var colorId = GetColorId(color);
        var newColorId = (colorId + 1) % BranchColors.Count;

        repoConfig.Set(repo.Path, s => s.BranchColors[branch.PrimaryName] = newColorId);
    }

    static Color GetColorByName(string name, int addIndex = 0)
    {
        var branchColorId = (Hash(name) + addIndex) % BranchColors.Count;
        return GetColorByColorId(branchColorId);
    }

    // Create a simple string hash to int
    static int Hash(string plainText)
    {
        // Computing Hash - returns here byte array
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainText));
        return Math.Abs(BitConverter.ToInt32(bytes, 0));
    }

    static Color GetColorByColorId(int colorId)
    {
        var index = Math.Min(colorId, BranchColors.Count - 1);
        return BranchColors[index];
    }

    static int GetColorId(Color color) => BranchColors.ToList().FindIndex(c => c == color);
}

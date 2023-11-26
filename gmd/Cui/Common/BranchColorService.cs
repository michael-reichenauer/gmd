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
interface IBranchColorService
{
    Color GetColor(Repo repo, Branch branch);
    void ChangeColor(Repo repo, Branch branch);
}

class BranchColorService : IBranchColorService
{
    static readonly Color[] BranchColors = { Color.Blue, Color.Green, Color.Cyan, Color.Red, Color.Yellow };

    readonly IRepoConfig repoConfig;


    internal BranchColorService(IRepoConfig repoConfig)
    {
        this.repoConfig = repoConfig;
    }


    public Color GetColor(Repo repo, Branch branch)
    {
        if (branch.IsDetached) return Color.White;
        if (branch.IsMainBranch) return Color.Magenta;

        if (repoConfig.Get(repo.Path).BranchColors.TryGetValue(branch.PrimaryName, out var colorId))
        {   // Branch has a color set by user, use it
            return GetColorByColorId(colorId);
        }

        if (branch.ParentBranchName == "")
        {   // branch has no parent, get color based on branch name
            return GetColorByName(branch.PrimaryName);
        }

        // Branch has a parent, lets check the color of parent to determine branch color
        var parentBranch = repo.BranchByName[branch.ParentBranchName];

        if (branch.PrimaryName == parentBranch.PrimaryName)
        {   // Same common name, lets use parent color
            return GetColor(repo, parentBranch);
        }

        // Parent is a different branch lets use a colors that is different
        Color color = GetColorByName(branch.PrimaryName);
        Color parentColor = GetColor(repo, parentBranch);
        if (color == parentColor)
        {   // branch got same color as parent, lets change branch color one step
            color = GetColorByName(branch.PrimaryName, 1);
        }

        return color;
    }

    public void ChangeColor(Repo repo, Branch branch)
    {
        var color = GetColor(repo, branch);
        var colorId = GetColorId(color);
        var newColorId = (colorId + 1) % BranchColors.Length;

        repoConfig.Set(repo.Path, s => s.BranchColors[branch.PrimaryName] = newColorId);
    }

    static Color GetColorByName(string name, int addIndex = 0)
    {
        var branchColorId = (Hash(name) + addIndex) % BranchColors.Length;
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
        var index = Math.Min(colorId, BranchColors.Length - 1);
        return BranchColors[index];
    }

    static int GetColorId(Color color) =>
        Array.FindIndex(BranchColors, c => c == color);
}

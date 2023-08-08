using System.Security.Cryptography;
using System.Text;
using gmd.Common;
using gmd.Server;

namespace gmd.Cui.Common;


// Manages brach colors, each branch has a color that is used in the UI.
// By default, the color is based on the branch primary base name (hashed id), but also if the color.
// is the same as the parent branch, it is changed to a different color.
// The user can manually change the color of a branch, and the color is stored in the state
// until user changes again.
interface IBranchColorService
{
    Color GetColor(Repo repo, Branch branch);
    Color GetColorByBranchName(Repo repo, string primaryBaseName);
    void ChangeColor(Repo repo, Branch branch);
}

class BranchColorService : IBranchColorService
{
    static readonly Color[] BranchColors = { Color.Blue, Color.Green, Color.Cyan, Color.Red, Color.Yellow };

    readonly IRepoState repoState;


    internal BranchColorService(IRepoState repoState)
    {
        this.repoState = repoState;
    }


    public Color GetColor(Server.Repo repo, Server.Branch branch)
    {
        if (branch.IsDetached) return Color.White;
        if (branch.IsMainBranch) return Color.Magenta;

        if (repoState.Get(repo.Path).BranchColors.TryGetValue(branch.PrimaryBaseName, out var colorId))
        {   // Branch has a color set by user, use it
            return GetColorByColorId(colorId);
        }

        if (branch.ParentBranchName == "")
        {   // branch has no parent, get color based on branch base name
            return GetColorByBranchBaseName(branch.PrimaryBaseName);
        }

        // Branch has a parent, lets check the color of parent to determine branch color
        var parentBranch = repo.BranchByName[branch.ParentBranchName];

        if (branch.PrimaryName == parentBranch.PrimaryName)
        {   // Same common name, lets use parent color
            return GetColor(repo, parentBranch);
        }

        // Parent is a different branch lets use a colors that is different
        Color color = GetColorByBranchBaseName(branch.PrimaryBaseName);
        Color parentColor = GetColor(repo, parentBranch);
        if (color == parentColor)
        {   // branch got same color as parent, lets change branch color one step
            color = GetColorByBranchBaseName(branch.PrimaryBaseName, 1);
        }

        return color;
    }


    public Color GetColorByBranchName(Server.Repo repo, string primaryBaseName)
    {
        if (repoState.Get(repo.Path).BranchColors.TryGetValue(primaryBaseName, out var colorId))
        {   // Branch has a color set by user, use it
            return GetColorByColorId(colorId);
        }

        return GetColorByBranchBaseName(primaryBaseName);
    }


    public void ChangeColor(Repo repo, Branch branch)
    {
        var color = GetColor(repo, branch);
        var colorId = GetColorId(color);
        var newColorId = (colorId + 1) % BranchColors.Length;

        repoState.Set(repo.Path, s => s.BranchColors[branch.PrimaryBaseName] = newColorId);
    }

    static Color GetColorByBranchBaseName(string name, int addIndex = 0)
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

using System.Security.Cryptography;
using System.Text;
using gmd.Common;
using gmd.Server;

namespace gmd.Cui.Common;


class BranchColor
{
    public static readonly Color[] BranchColors = { Color.Blue, Color.Green, Color.Cyan, Color.Red, Color.Yellow };

    public static Color ColorById(int colorId)
    {
        var index = Math.Min(colorId, BranchColors.Length - 1);
        return BranchColors[index];
    }

    public static int GetColorId(Color color) =>
        Array.FindIndex(BranchColor.BranchColors, c => c == color);
}


// Manages brach colors
interface IBranchColorService
{
    Color GetColor(Server.Repo repo, Server.Branch branch);
    void ChangeColor(Server.Repo repo, Server.Branch branch);
}

class BranchColorService : IBranchColorService
{
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
        {
            return BranchColor.ColorById(colorId);
        }

        if (branch.ParentBranchName == "")
        {   // branch has no parent, get color based on parent name
            return BranchNameColor(branch.PrimaryBaseName, 0);
        }

        // Branch has a parent, lets check the color of parent to determine branch color
        var parentBranch = repo.BranchByName[branch.ParentBranchName];

        if (branch.PrimaryName == parentBranch.PrimaryName)
        {   // Same common name, lets use parent color
            return GetColor(repo, parentBranch);
        }

        // Parent is a different branch lets use a colors that is different
        Color color = BranchNameColor(branch.PrimaryBaseName, 0);
        Color parentColor = GetColor(repo, parentBranch);
        if (color == parentColor)
        {   // branch got same color as parent, lets change branch color one step
            color = BranchNameColor(branch.PrimaryBaseName, 1);
        }

        return color;
    }

    public void ChangeColor(Repo repo, Branch branch)
    {
        var color = GetColor(repo, branch);
        var colorId = BranchColor.GetColorId(color);
        var newColorId = (colorId + 1) % BranchColor.BranchColors.Length;

        repoState.Set(repo.Path, s => s.BranchColors[branch.PrimaryBaseName] = newColorId);
    }

    Color BranchNameColor(string name, int addIndex)
    {
        var branchColorId = (Hash(name) + addIndex) % BranchColor.BranchColors.Length;
        return BranchColor.ColorById(branchColorId);
    }


    // Create a simple string hash to int
    static int Hash(string plainText)
    {
        using (SHA256 sha256Hash = SHA256.Create())
        {
            // Computing Hash - returns here byte array
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(plainText));
            return Math.Abs(BitConverter.ToInt32(bytes, 0));
        }
    }
}
using System.Security.Cryptography;
using System.Text;
using gmd.Common;
using gmd.Server;

namespace gmd.Cui.Common;
using Color = Terminal.Gui.Attribute;

// Mangages brach colors
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
        if (repoState.Get(repo.Path).BranchColors.TryGetValue(branch.CommonName, out var colorId))
        {
            colorId = Math.Min(colorId, TextColor.BranchColors.Length - 1);
            return TextColor.BranchColors[colorId];
        }

        if (branch.ParentBranchName == "")
        {   // branch has no parent or parent is remote of this branch, lets use it.
            return BranchNameColor(branch.DisplayName, 0);
        }

        var parentBranch = repo.BranchByName[branch.ParentBranchName];

        if (branch.CommonName == parentBranch.CommonName)
        {
            // Parent is remote or this branch is a pull merge, lets use parent color
            return GetColor(repo, parentBranch);
        }

        Color color = BranchNameColor(branch.DisplayName, 0);
        Color parentColor = GetColor(repo, parentBranch);

        if (color == parentColor)
        {   // branch got same color as parent, lets change branch color one step
            color = BranchNameColor(branch.DisplayName, 1);
        }

        return color;
    }

    public void ChangeColor(Repo repo, Branch branch)
    {
        var color = GetColor(repo, branch);
        var colorId = Array.FindIndex(TextColor.BranchColors, c => c == color);
        var newColorId = (colorId + 1) % TextColor.BranchColors.Length;
        repoState.Set(repo.Path, s => s.BranchColors[branch.CommonName] = newColorId);
    }

    Color BranchNameColor(string name, int addIndex)
    {
        if (name == "main" || name == "master")
        {
            return TextColor.Magenta;
        }

        var branchColorId = (Hash(name) + addIndex) % TextColor.BranchColors.Length;
        return TextColor.BranchColors[branchColorId];
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

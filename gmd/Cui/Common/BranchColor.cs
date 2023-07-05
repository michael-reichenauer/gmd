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
        if (branch.IsDetached) return TextColor.White;
        if (branch.IsMainBranch) return TextColor.Magenta;

        if (repoState.Get(repo.Path).BranchColors.TryGetValue(branch.CommonName, out var colorId))
        {
            return TextColor.BranchColorById(colorId);
        }

        if (branch.ParentBranchName == "")
        {   // branch has no parent, get color based on parent name
            return BranchNameColor(branch.ViewName, 0);
        }

        // Branch has a parent, lets check the color of parent to determine branch color
        var parentBranch = repo.BranchByName[branch.ParentBranchName];

        if (branch.CommonName == parentBranch.CommonName)
        {   // Same common name, lets use parent color
            return GetColor(repo, parentBranch);
        }

        // Parent is a different branch lets use a colore that is different
        Color color = BranchNameColor(branch.ViewName, 0);
        Color parentColor = GetColor(repo, parentBranch);
        if (color == parentColor)
        {   // branch got same color as parent, lets change branch color one step
            color = BranchNameColor(branch.ViewName, 1);
        }

        return color;
    }

    public void ChangeColor(Repo repo, Branch branch)
    {
        var color = GetColor(repo, branch);
        var colorId = TextColor.GetBranchColorId(color);
        var newColorId = (colorId + 1) % TextColor.BranchColors.Length;

        repoState.Set(repo.Path, s => s.BranchColors[branch.CommonName] = newColorId);
    }

    Color BranchNameColor(string name, int addIndex)
    {
        var branchColorId = (Hash(name) + addIndex) % TextColor.BranchColors.Length;
        return TextColor.BranchColorById(branchColorId);
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

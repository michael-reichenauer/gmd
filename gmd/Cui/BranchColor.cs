namespace gmd.Cui;
using System.Security.Cryptography;
using System.Text;
using gmd.ViewRepos;
using Color = Terminal.Gui.Attribute;

interface IBranchColorService
{
    Color GetColor(Repo repo, Branch branch);
}

class BranchColorService : IBranchColorService
{
    public Color GetColor(Repo repo, Branch branch)
    {
        if (branch.ParentBranchName == "")
        {   // branch has no parent or parent is remote of this branch, lets use it
            return BranchNameColor(branch.CommonName, 0);
        }

        var parentBranch = repo.BranchByName[branch.ParentBranchName];

        if (branch.RemoteName == parentBranch.Name)
        {
            // Parent is remote of this branch, lets use parent color
            return GetColor(repo, parentBranch);
        }

        Color color = BranchNameColor(branch.CommonName, 0);
        Color parentColor = BranchNameColor(parentBranch.CommonName, 0);

        if (color == parentColor)
        {   // branch got same color as parent, lets change branch color one step
            color = BranchNameColor(branch.CommonName, 1);
        }

        return color;
    }

    private Color BranchNameColor(string name, int addIndex)
    {
        if (name == "main" || name == "master")
        {
            return Colors.Magenta;
        }

        var branchColorId = (Hash(name) + addIndex) % Colors.BranchColors.Length;
        return Colors.BranchColors[branchColorId];
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

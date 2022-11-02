

namespace gmd.ViewRepos.Private.Augmented.Private;

interface IConverter
{
    Repo ToRepo(AugRepo augRepo);
}

class Converter : IConverter
{
    public Repo ToRepo(AugRepo augRepo)
    {
        return new Repo(
            augRepo.Commits.Select(ToCommit).ToList(),
            augRepo.Branches.Select(ToBranch).ToList()
        );
    }

    Commit ToCommit(AugCommit c)
    {
        return new Commit(c.C.Id, c.C.Sid, c.C.ParentIds, c.C.Subject, c.C.Message,
            c.C.Author, c.C.AuthorTime, c.C.CommitTime);
    }
    Branch ToBranch(AugBranch b)
    {
        return new Branch(b.B.Name, b.B.DisplayName, b.B.TipID, b.B.IsCurrent,
            b.B.IsRemote, b.B.RemoteName, b.B.IsDetached,
            b.B.AheadCount, b.B.BehindCount, b.B.IsRemoteMissing);
    }
}


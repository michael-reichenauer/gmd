

namespace gmd.ViewRepos.Private.Augmented.Private;

interface IAugmenter
{
    Task<AugRepo> GetAugRepoAsync(GitRepo gitRepo);
}

class Augmenter : IAugmenter
{
    public Task<AugRepo> GetAugRepoAsync(GitRepo gitRepo)
    {
        return Task.Run(() => GetAugRepo(gitRepo));

    }

    private AugRepo GetAugRepo(GitRepo gitRepo)
    {
        return new AugRepo(gitRepo);
    }
}

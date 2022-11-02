namespace gmd.Utils.Git;

internal interface IGitService
{
    IGit GetGit(string path);
}
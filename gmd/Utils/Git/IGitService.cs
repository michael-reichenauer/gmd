namespace gmd.Utils.Git;

internal interface IGitService
{
    IGitRepo GetRepo(string path);
}
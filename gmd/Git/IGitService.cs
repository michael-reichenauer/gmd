namespace gmd.Utils.Git;

internal interface IGitService
{
    IGit Git(string path);
}
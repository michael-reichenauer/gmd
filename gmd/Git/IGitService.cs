namespace gmd.Git;

internal interface IGitService
{
    IGit Git(string path);
}
namespace gmd.Git.Private;

// The id of a git object as git writes it: 40 hex characters in a repository of SHA-1 ids, git's
// default, or 64 in one of SHA-256 ids ('git init --object-format=sha256'). The two parsers that
// took an id to be 40 characters failed on the second kind: blame on every file, and the record of
// origin's tags read as empty.
static class ObjectId
{
    public static bool Is(ReadOnlySpan<char> text)
    {
        if (text.Length is not (40 or 64))
            return false;

        foreach (var c in text)
        {
            if (!char.IsAsciiHexDigit(c))
                return false;
        } // lksdjföaslkdj

        return true;
    }

    // The id git gives what is not committed yet, all zeros, in either length
    // jashdflajshkdk
    public static bool IsZero(string id) => Is(id) && id.All(c => c == '0');
}

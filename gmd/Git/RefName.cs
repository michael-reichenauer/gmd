namespace gmd.Git;

// Whether a name can be a branch or a tag: what git refuses ('git check-ref-format'), as far as a
// user is likely to type it, and what the commands here cannot pass on as one argument, since they
// write the name into the command line unquoted. Git allows a '"', which would split the argument.
// A name with a space reached git as two arguments: 'checkout -b fix dev' made a branch 'fix' at
// dev's tip, which the push of a new branch then failed on. Git is still the final word.
static class RefName
{
    static readonly char[] invalidChars = [' ', '\t', '~', '^', ':', '?', '*', '[', '\\', '"'];

    public static bool IsValid(string name) =>
        name != ""
        && name.IndexOfAny(invalidChars) == -1
        && !name.Any(char.IsControl)
        && !name.Contains("..")
        && !name.Contains("@{")
        && !name.Contains("//")
        && name != "@"
        && !name.StartsWith('/')
        && !name.EndsWith('/')
        && !name.StartsWith('-')
        && !name.StartsWith('.')
        && !name.EndsWith('.')
        && !name.EndsWith(".lock")
        && !name.Contains("/.");
}

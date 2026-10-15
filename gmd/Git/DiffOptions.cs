namespace gmd.Git;

// What a diff is read with: how many unchanged lines git shows above and below each change
// ('--unified=<n>'), and whether changes of whitespace alone are left out ('--ignore-all-space'), as
// GitHub's 'Hide whitespace' leaves them out: a line whose spaces alone differ is then unchanged, and
// a file whose changes are all whitespace has no diff at all.
record DiffOptions(int ContextLines, bool IsIgnoreWhitespace = false)
{
    // A number of context lines alone is what most diffs are read with
    public static implicit operator DiffOptions(int contextLines) => new(contextLines);

    // The options as git takes them, after any command that writes a diff
    public string Args => $"--unified={ContextLines}{WhitespaceArgs}";

    // The whitespace option alone, for 'git log -L', which takes no context
    public string WhitespaceArgs => IsIgnoreWhitespace ? " --ignore-all-space" : "";
}

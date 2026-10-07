namespace gmd.Utils;

// gmd as the askpass program of the git it runs. Git, and the ssh it runs, ask for a passphrase, a
// password or whether to trust a host on the terminal, and gmd owns the terminal: a question drawn
// there scrolled the UI away, took the keys typed for gmd as its answer, and waited for an Enter that
// the raw terminal sends as '\r'. So Cmd gives git no terminal to ask on, and points ssh at gmd
// instead (Cmd.NeverAskOnTheTerminal), and git as well, which asks SSH_ASKPASS when it has no
// GIT_ASKPASS. Started as one, gmd gets the question as its only argument.
//
// For now it answers nothing: it writes what was asked to stderr, which ssh and git pass on into the
// error output of the command, where LoginError finds it and says what to do instead, and it exits
// with 1, which both take as no answer. MODERNIZATION.md has how it could ask the user instead, in a
// dialog of the gmd that started git.
static class Askpass
{
    // Set in the environment of every git process, and so of the ssh it runs and of the askpass that
    // runs, so that gmd started as one knows it is one rather than a gmd to show a repository in
    public const string Variable = "GMD_ASKPASS";

    // The start of the line it writes, which LoginError looks for in the output of git
    public const string Marker = "gmd cannot ask: ";

    public static bool IsAsked => Environment.GetEnvironmentVariable(Variable) == "1";

    // Touches nothing else: no log, no config, no terminal, since it runs inside a git command, often
    // while the gmd that started that command is drawing on the terminal
    public static int Answer(string[] args)
    {
        Console.Error.WriteLine($"{Marker}{string.Join(' ', args).Trim()}");
        return 1;
    }
}

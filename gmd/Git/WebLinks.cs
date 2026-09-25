namespace gmd.Git;

// cSpell:ignore gitea forgejo codeberg pullrequestcreate visualstudio

// The service hosting a remote, which decides what its web pages are called
enum WebHost
{
    Unknown, // A web page for the repository, but no known links below it
    GitHub,
    GitLab,
    Bitbucket,
    AzureDevOps,
    Gitea, // Also Forgejo and Codeberg, which kept Gitea's links
}

// The web pages of a repository whose remote is on a hosting service: the repository itself, a
// branch, a commit, and the page for opening a pull request. Git knows only the remote's URL, which
// is an address for git to fetch from, not a page, so this turns the forms that URL takes (https,
// ssh, and the scp-like 'git@host:path') into the web address of the repository, and adds the
// paths each service uses below it. Those differ per service, and a self-hosted one is recognized
// by its name only if it has one, so for an unknown host only the repository's own page is known.
//
// This is string work with no git or network, so it is tested with no repository.
record WebLinks(string RepoUrl, WebHost Host)
{
    // The web links of a remote URL, null for a remote that is a folder or has no web address
    public static WebLinks? From(string remoteUrl)
    {
        var (scheme, host, path) = Parse(remoteUrl.Trim());
        if (host == "" || path == "")
            return null;

        path = Escaped(path.Trim('/').TrimSuffix(".git").Trim('/'));
        var lowerHost = host.ToLowerInvariant();

        // Azure DevOps names its web pages differently from its ssh address, and has two of each
        if (lowerHost == "ssh.dev.azure.com" && path.StartsWith("v3/"))
            return AzureRepo("https://dev.azure.com", path[3..]);
        if (lowerHost.EndsWith("vs-ssh.visualstudio.com") && path.StartsWith("v3/"))
        {
            var parts = path[3..].Split('/');
            return parts.Length == 3
                ? AzureRepo($"https://{parts[0]}.visualstudio.com", $"{parts[1]}/{parts[2]}")
                : null;
        }
        if (lowerHost == "dev.azure.com" || lowerHost.EndsWith(".visualstudio.com"))
            return new WebLinks($"https://{host}/{path}", WebHost.AzureDevOps);

        return new WebLinks($"{scheme}://{host}/{path}", HostOf(lowerHost));
    }

    // The page of a branch, as it is named on the remote, i.e. without 'origin/'
    public string? Branch(string name) =>
        Host switch
        {
            WebHost.GitHub => $"{RepoUrl}/tree/{Path(name)}",
            WebHost.GitLab => $"{RepoUrl}/-/tree/{Path(name)}",
            WebHost.Bitbucket => $"{RepoUrl}/branch/{Path(name)}",
            WebHost.AzureDevOps => $"{RepoUrl}?version=GB{Query(name)}",
            WebHost.Gitea => $"{RepoUrl}/src/branch/{Path(name)}",
            _ => null,
        };

    public string? Commit(string id) =>
        Host switch
        {
            WebHost.GitHub or WebHost.AzureDevOps or WebHost.Gitea => $"{RepoUrl}/commit/{id}",
            WebHost.GitLab => $"{RepoUrl}/-/commit/{id}",
            WebHost.Bitbucket => $"{RepoUrl}/commits/{id}",
            _ => null,
        };

    // The page for opening a pull request (a merge request on GitLab) of a branch into another,
    // with both filled in, so that the base is the branch it was made from rather than whatever the
    // service defaults to
    public string? NewPullRequest(string branch, string into) =>
        Host switch
        {
            WebHost.GitHub => $"{RepoUrl}/compare/{Path(into)}...{Path(branch)}?expand=1",
            WebHost.GitLab => $"{RepoUrl}/-/merge_requests/new?merge_request%5Bsource_branch%5D={Query(branch)}"
                + $"&merge_request%5Btarget_branch%5D={Query(into)}",
            WebHost.Bitbucket => $"{RepoUrl}/pull-requests/new?source={Query(branch)}&dest={Query(into)}",
            WebHost.AzureDevOps => $"{RepoUrl}/pullrequestcreate?sourceRef={Query(branch)}&targetRef={Query(into)}",
            WebHost.Gitea => $"{RepoUrl}/compare/{Path(into)}...{Path(branch)}",
            _ => null,
        };

    public string HostName =>
        Host switch
        {
            WebHost.GitHub => "GitHub",
            WebHost.GitLab => "GitLab",
            WebHost.Bitbucket => "Bitbucket",
            WebHost.AzureDevOps => "Azure DevOps",
            WebHost.Gitea => "Gitea",
            _ => new Uri(RepoUrl).Host,
        };

    // The scheme, host and path of the three forms a remote URL takes, all empty for a local path.
    // The web address drops the user and, for ssh, the port, which is the ssh server's rather than
    // the web server's.
    static (string Scheme, string Host, string Path) Parse(string url)
    {
        if (url.Contains("://"))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.IsFile || uri.Host == "")
                return ("", "", "");
            var isWeb = uri.Scheme is "http" or "https";
            var host = isWeb && !uri.IsDefaultPort ? $"{uri.Host}:{uri.Port}" : uri.Host;
            return (uri.Scheme == "http" ? "http" : "https", host, Uri.UnescapeDataString(uri.AbsolutePath));
        }

        // The scp-like 'user@host:path', which is ssh. A ':' after a '/', or a single letter before
        // it, is a path rather than a host, e.g. '../repo:x' or 'C:\repos\x'.
        var colon = url.IndexOf(':');
        var slash = url.IndexOf('/');
        if (colon <= 1 || (slash >= 0 && slash < colon))
            return ("", "", "");
        var userAndHost = url[..colon];
        return ("https", userAndHost[(userAndHost.LastIndexOf('@') + 1)..], url[(colon + 1)..]);
    }

    static WebHost HostOf(string host) =>
        host.Contains("github") ? WebHost.GitHub
        : host.Contains("gitlab") ? WebHost.GitLab
        : host == "bitbucket.org" ? WebHost.Bitbucket
        : host == "codeberg.org" || host.Contains("gitea") || host.Contains("forgejo") ? WebHost.Gitea
        : WebHost.Unknown;

    // 'org/project/repo', from the ssh address, as the https one names it
    static WebLinks? AzureRepo(string site, string path)
    {
        var parts = path.Split('/');
        return parts.Length == 3 ? new WebLinks($"{site}/{parts[0]}/{parts[1]}/_git/{parts[2]}", WebHost.AzureDevOps)
            : parts.Length == 2 ? new WebLinks($"{site}/{parts[0]}/_git/{parts[1]}", WebHost.AzureDevOps)
            : null;
    }

    // A branch name in a path keeps its '/', every other character that means something in a URL
    // is escaped: git allows '#', '%' and '&' in a branch name
    static string Path(string name) => string.Join('/', name.Split('/').Select(Uri.EscapeDataString));

    // The repository's path escaped, whether the remote had it escaped or not: an Azure DevOps
    // project name often has a space, and a link with one in it is two arguments to the program it
    // is handed to, see BrowserService, and no link at all when copied
    static string Escaped(string path) =>
        string.Join('/', path.Split('/').Select(p => Uri.EscapeDataString(Uri.UnescapeDataString(p))));

    static string Query(string value) => Uri.EscapeDataString(value);
}

using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using gmd.Common;
using gmd.Cui.Common;
using gmd.Cui.Graphs;
using gmd.Server;
using WebHost = gmd.Git.WebHost;
using WebLinks = gmd.Git.WebLinks;

namespace gmd.Cui.WebLog;

// An open pull request, as 'gh pr list --json number,title,headRefName,url,isDraft' gives it
record PullRequest(int Number, string Title, string HeadRefName, string Url, bool IsDraft);

// The log view as a web page: the branch graph of the shown branches with each commit's subject,
// author and time, every commit linked to its page on the hosting service, every branch to its page
// and its open pull request, and above it the branches, the one changed last first. Made for a phone
// or a tablet, to see what is going on in a repo where gmd cannot run, e.g. by a GitHub Action
// publishing it to GitHub Pages. It is the log view's own graph, GraphCreater's and GraphWriter's,
// drawn as SVG by GraphSvg, in the colors of gmd's own dark and light palettes.
//
// This is string work on a view repo and its graph, with no git or view, so it is tested with
// RepoBuilder.
static class WebLogPage
{
    // The guide to the page, what it shows and how it is made
    const string GuideUrl = Project.Url + "/blob/dev/docs/BRANCH-GRAPH.md";

    static readonly Regex PullRequestMerge = new(@"^Merge pull request #(\d+)", RegexOptions.Compiled);

    public static string Html(
        Server.Repo repo,
        Graph graph,
        IGraphWriter graphWriter,
        Func<Branch, Color> branchColor,
        WebLinks? links,
        IReadOnlyList<PullRequest> pullRequests,
        string title,
        DateTime generated,
        int maxRows
    )
    {
        var prs = pullRequests.GroupBy(p => p.HeadRefName).ToDictionary(g => g.Key, g => g.First());
        var commits = repo.ViewCommits.Where(c => c.Id != Repo.UncommittedId).Take(maxRows).ToList();
        var branches = repo.ViewBranches.Where(b => b.IsPrimary && !b.IsDetached).ToList();
        var tips = branches.GroupBy(b => b.TipId).ToDictionary(g => g.Key, g => g.ToList());

        var html = new StringBuilder();
        html.Append(
            $$"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{E(title)}}</title>
            <style>{{Style()}}</style>
            </head>
            <body>
            <header>
            <h1>{{RepoLink(links, title)}}</h1>
            <p class="meta">Branch graph by gmd, updated <time data-t="{{Iso(
                generated
            )}}">{{generated:yyyy-MM-dd HH:mm}} UTC</time></p>
            <details class="about"><summary>About this page</summary>
            <p>What everyone is working on, across the branches: gmd's branch graph of this repo, made anew
            by a GitHub Action on every push. <b>Branches</b> lists the active branches, the latest changed
            first, with their open pull requests. In <b>Commits</b>, each branch is a line in its own color,
            main on the left, every branch to the right of the branch it came from; a dot is a commit, an
            arc in is a merge and an arc out is where a branch started, and the short grey arcs are branches
            not shown. Tap a subject for the commit's diff, or a pull request's merge for the pull request.</p>
            <p><a href="{{GuideUrl}}">How it works</a></p>
            </details>
            </header>

            """
        );

        AppendBranches(html, repo, branches, branchColor, links, prs);
        AppendLog(html, repo, graph, graphWriter, commits, tips, branchColor, links, prs);

        html.Append(
            $$"""
            <script>{{Script}}</script>
            </body>
            </html>

            """
        );
        return html.ToString();
    }

    // The branches, the one with the latest commit first, each with its last commit's time and
    // author, and its open pull request
    static void AppendBranches(
        StringBuilder html,
        Server.Repo repo,
        IReadOnlyList<Branch> branches,
        Func<Branch, Color> branchColor,
        WebLinks? links,
        IReadOnlyDictionary<string, PullRequest> prs
    )
    {
        html.Append("<section class=\"branches\"><h2>Branches</h2><ul>\n");
        foreach (var b in branches.OrderByDescending(b => repo.CommitById[b.TipId].AuthorTime))
        {
            var tip = repo.CommitById[b.TipId];
            var name = RemoteName(b);
            var color = GraphSvg.ColorClass(branchColor(b));
            html.Append($"<li><a class=\"bn {color}\" href=\"#{tip.Sid}\"><span class=\"dot\"></span>")
                .Append(E(b.NiceNameUnique))
                .Append("</a>");
            if (prs.TryGetValue(name, out var pr))
                html.Append(' ').Append(PullRequestLink(pr));
            else if (links?.Branch(name) is string page && b.IsGitBranch)
                html.Append($" <a class=\"ext\" href=\"{E(page)}\" title=\"The branch on the hosting service\">↗</a>");
            html.Append($"<span class=\"bm\">{E(tip.Author)} · {TimeTag(tip.AuthorTime)}</span></li>\n");
        }
        html.Append("</ul></section>\n");
    }

    static void AppendLog(
        StringBuilder html,
        Server.Repo repo,
        Graph graph,
        IGraphWriter graphWriter,
        IReadOnlyList<Commit> commits,
        IReadOnlyDictionary<string, List<Branch>> tips,
        Func<Branch, Color> branchColor,
        WebLinks? links,
        IReadOnlyDictionary<string, PullRequest> prs
    )
    {
        html.Append("<section class=\"log\"><h2>Commits</h2><div class=\"rows\">\n");
        foreach (var c in commits)
        {
            var graphRow = graphWriter.ToText(graph, c.ViewIndex, graph.Width, "", false);
            var dotIndex = graph.HasBranch(c.BranchName) ? graph.BranchByName(c.BranchName).X * 2 : -1;

            html.Append($"<div class=\"r\" id=\"{c.Sid}\">")
                .Append(GraphSvg.Row(graphRow, dotIndex, graph.Width))
                .Append("<div class=\"t\"><div class=\"s\">");

            if (tips.TryGetValue(c.Id, out var tipBranches))
            {
                foreach (var b in tipBranches)
                {
                    var name = RemoteName(b);
                    var color = GraphSvg.ColorClass(branchColor(b));
                    var page = b.IsGitBranch ? links?.Branch(name) : null;
                    html.Append(
                        page != null
                            ? $"<a class=\"tip {color}\" href=\"{E(page)}\">{E(b.NiceNameUnique)}</a>"
                            : $"<span class=\"tip {color}\">{E(b.NiceNameUnique)}</span>"
                    );
                    if (prs.TryGetValue(name, out var pr))
                        html.Append(PullRequestLink(pr));
                }
            }
            foreach (var tag in c.Tags)
                html.Append($"<span class=\"tag\">{E(tag.Name)}</span>");

            html.Append(SubjectHtml(c, links)).Append("</div>");
            html.Append($"<div class=\"m\">{E(c.Author)} · {TimeTag(c.AuthorTime)} · ");
            html.Append(
                links?.Commit(c.Id) is string commitPage
                    ? $"<a class=\"sid\" href=\"{E(commitPage)}\">{c.Sid}</a>"
                    : $"<span class=\"sid\">{c.Sid}</span>"
            );
            html.Append("</div></div></div>\n");
        }
        var more = repo.ViewCommits.Count(c => c.Id != Repo.UncommittedId) - commits.Count;
        if (more > 0)
            html.Append($"<p class=\"meta more\">… and {more} older commits</p>\n");
        html.Append("</div></section>\n");
    }

    // The subject, linked to the commit's page, and a pull request's merge to the pull request
    static string SubjectHtml(Commit c, WebLinks? links)
    {
        var subject = E(c.Subject);
        var cls = c.ParentIds.Count > 1 ? "subj merge" : "subj";
        if (links?.Host == WebHost.GitHub && PullRequestMerge.Match(c.Subject) is { Success: true } m)
            return $"<a class=\"{cls}\" href=\"{E(links.RepoUrl)}/pull/{m.Groups[1].Value}\">{subject}</a>";
        if (links?.Commit(c.Id) is string page)
            return $"<a class=\"{cls}\" href=\"{E(page)}\">{subject}</a>";
        return $"<span class=\"{cls}\">{subject}</span>";
    }

    static string PullRequestLink(PullRequest pr) =>
        $"<a class=\"pr{(pr.IsDraft ? " draft" : "")}\" href=\"{E(pr.Url)}\" title=\"{E(pr.Title)}\">#{pr.Number}</a>";

    static string RepoLink(WebLinks? links, string title) =>
        links != null ? $"<a href=\"{E(links.RepoUrl)}\">{E(title)}</a>" : E(title);

    // The branch's name on the remote, as its web page names it
    static string RemoteName(Branch b) =>
        b.IsRemote && b.RemoteName != "" && b.Name.StartsWith(b.RemoteName + "/") ? b.Name[(b.RemoteName.Length + 1)..]
        : b.IsRemote && b.Name.StartsWith("origin/") ? b.Name["origin/".Length..]
        : b.Name;

    static string TimeTag(DateTime time) =>
        $"<time data-t=\"{Iso(time)}\">{time.ToUniversalTime():yyyy-MM-dd HH:mm}</time>";

    static string Iso(DateTime time) =>
        time.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    static string E(string text) => WebUtility.HtmlEncode(text);

    // The colors of gmd's dark palette, and of its light one for a light page
    static string Style()
    {
        var css = new StringBuilder();
        css.Append(
            $$$"""
            :root{color-scheme:dark light;--bg:#0c0c0c;--fg:#e6e6e6;--dim:#8a8a8a;--line:#262626;--chip:#1c1c1c}
            @media (prefers-color-scheme: light){:root{--bg:#ffffff;--fg:#1a1a1a;--dim:#6b6b6b;--line:#e6e6e6;--chip:#f2f2f2}}
            *{box-sizing:border-box}
            body{margin:0;background:var(--bg);color:var(--fg);font:15px/1.35 -apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,sans-serif;-webkit-text-size-adjust:100%}
            header,section{padding:0 16px}
            h1{font-size:20px;margin:16px 0 2px}
            h1 a{color:inherit;text-decoration:none}
            h2{font-size:13px;text-transform:uppercase;letter-spacing:.06em;color:var(--dim);margin:20px 0 8px}
            a{color:inherit}
            .meta{color:var(--dim);font-size:13px;margin:0}
            .branches ul{list-style:none;margin:0;padding:0;display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:2px 16px}
            .branches li{display:flex;align-items:baseline;gap:6px;padding:6px 0;border-bottom:1px solid var(--line);min-width:0}
            .bn{font-weight:600;text-decoration:none;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;min-width:0}
            .bn .dot{display:inline-block;width:9px;height:9px;border-radius:50%;background:currentColor;margin-right:6px}
            .bm{margin-left:auto;color:var(--dim);font-size:12px;white-space:nowrap}
            .ext{color:var(--dim);font-size:12px}
            .log{padding-right:0}
            .rows{overflow-x:auto;-webkit-overflow-scrolling:touch}
            .r{display:flex;align-items:center;height:{{{GraphSvg.RowHeight}}}px;scroll-margin-top:40px}
            .r:target{background:var(--chip)}
            .g{flex:none;display:block}
            .t{flex:1;min-width:0;padding:0 16px 0 6px;line-height:18px}
            .s{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
            .subj{text-decoration:none}
            .subj.merge{color:var(--dim)}
            .m{color:var(--dim);font-size:12px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
            .sid{font-family:ui-monospace,Menlo,monospace;text-decoration:none}
            .tip{display:inline-block;font-size:12px;font-weight:600;padding:0 6px;margin-right:4px;border:1px solid currentColor;border-radius:9px;text-decoration:none}
            .pr{display:inline-block;font-size:12px;font-weight:600;padding:0 6px;margin-right:4px;border-radius:9px;background:#238636;color:#fff;text-decoration:none}
            .pr.draft{background:#6e7681}
            .tag{display:inline-block;font-size:12px;padding:0 6px;margin-right:4px;border-radius:9px;background:var(--chip);color:var(--c-Green)}
            .more{padding:12px 16px}
            .about{margin:8px 0 0;font-size:14px;color:var(--dim)}
            .about summary{cursor:pointer}
            .about p{margin:6px 0}

            """
        );
        css.Append(GraphSvg.Style);
        css.Append(PaletteCss(Palette.Dark)).Append('\n');
        css.Append("@media (prefers-color-scheme: light){").Append(PaletteCss(Palette.Light)).Append("}\n");
        return css.ToString();
    }

    static string PaletteCss(Palette palette)
    {
        var css = new StringBuilder();
        foreach (var name in Enum.GetValues<ColorName>().Where(n => n != ColorName.Background))
            css.Append($".c-{name}{{color:{Rgb(palette, name)}}}");
        css.Append($":root{{--c-Green:{Rgb(palette, ColorName.Green)}}}");
        return css.ToString();
    }

    static string Rgb(Palette palette, ColorName name) =>
        $"#{palette[name].R:X2}{palette[name].G:X2}{palette[name].B:X2}";

    // The times as how long ago, in the reader's own time zone, as the page is read rather than made
    const string Script = """
        (function(){
        const now=Date.now();
        function ago(t){const s=(now-t)/1000;if(s<60)return"now";const m=s/60;if(m<60)return Math.floor(m)+"m ago";
        const h=m/60;if(h<24)return Math.floor(h)+"h ago";const d=h/24;if(d<30)return Math.floor(d)+"d ago";
        return new Date(t).toLocaleDateString(undefined,{year:"numeric",month:"short",day:"numeric"});}
        document.querySelectorAll("time[data-t]").forEach(e=>{const t=Date.parse(e.dataset.t);
        e.title=new Date(t).toLocaleString();e.textContent=ago(t);});
        })();
        """;
}

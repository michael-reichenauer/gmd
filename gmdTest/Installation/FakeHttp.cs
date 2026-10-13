using System.Net;
using gmd.Installation;

namespace gmdTest.Installation;

// A request the fake answered: the url, and the ETag it was sent with, if any
record HttpCall(string Url, string IfNoneMatch);

// FakeHttp is a test double for IHttp, the seam between the updater and GitHub: a real HttpClient
// whose requests are answered by the test rather than sent, so the updater's own handling of what
// comes back is what is tested.
//
//     var http = new FakeHttp(request => FakeHttp.Text(releasesJson, eTag: "\"abc\""));
//     var releases = new GitHubReleases(config, http);
class FakeHttp : HttpMessageHandler, IHttp
{
    readonly Func<HttpRequestMessage, HttpResponseMessage> respond;

    public FakeHttp(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        this.respond = respond;
        Client = new HttpClient(this);
    }

    public HttpClient Client { get; }

    public List<HttpCall> Calls { get; } = [];

    public static HttpResponseMessage Text(string text, string eTag = "")
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text) };
        if (eTag != "")
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(eTag);
        return response;
    }

    public static HttpResponseMessage Status(HttpStatusCode status, string text = "") =>
        new(status) { Content = new StringContent(text) };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls.Add(new HttpCall(request.RequestUri?.ToString() ?? "", request.Headers.IfNoneMatch.ToString()));
        return Task.FromResult(respond(request));
    }
}

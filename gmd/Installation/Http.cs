namespace gmd.Installation;

// The HTTP client the updater talks to GitHub with, a seam so that the tests can answer the
// requests themselves. One client for the process, which is how HttpClient is meant to be used.
interface IHttp
{
    HttpClient Client { get; }
}

class Http : IHttp
{
    const string UserAgent = "gmd"; // GitHub's API refuses a request with no user agent

    static readonly HttpClient client = NewClient();

    public HttpClient Client => client;

    static HttpClient NewClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("user-agent", UserAgent);
        return client;
    }
}

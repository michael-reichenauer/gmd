using gmd.Common;

namespace gmdTest.Fixtures;

// A Config whose Set persists: the change is applied to the config straight away, and nothing is
// written to '~/.gmdconfig'. A bare 'new Config()' makes Set a no-op, so a test of what a class
// saves, rather than merely reads, needs this one.
class FakeConfigService : IConfigService
{
    public FakeConfigService(Action<Config>? init = null)
    {
        Config = new Config(new Lazy<IConfigService>(() => this));
        init?.Invoke(Config);
    }

    public Config Config { get; }

    public Config Get() => Config;

    public void Set(Action<Config> set) => set(Config);
}

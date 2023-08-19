namespace gmd.Common;


// This class is intended to be used in dependency injection as injected config parameter, which
// will have the latest config data, it gets/sets config values from/to ConfigService
class Config : ConfigData
{
    readonly IConfigService configService;

    public Config(IConfigService configService)
    {
        this.configService = configService;
        configService.Register(this);
    }

    public void Set(Action<ConfigData> set) => configService.Set(set);
}


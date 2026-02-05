namespace NebulaPanel.Application.Tests.Builders;

/// <summary>
/// Fluent builder for creating Game test instances.
/// </summary>
public class GameBuilder
{
    private readonly Game _game;

    public GameBuilder()
    {
        _game = new Game
        {
            Id = Guid.NewGuid(),
            Name = "Test Game",
            Slug = "test-game",
            SourceType = GameSourceType.Custom,
            ExecutableType = ExecutableType.Exe,
            DefaultExecutablePath = "test.exe",
            DefaultStartCommand = "",
            SupportsDocker = true,
            SupportsMods = false,
            IsEnabled = true
        };
    }

    public GameBuilder WithId(Guid id)
    {
        _game.Id = id;
        return this;
    }

    public GameBuilder WithName(string name)
    {
        _game.Name = name;
        return this;
    }

    public GameBuilder WithSlug(string slug)
    {
        _game.Slug = slug;
        return this;
    }

    public GameBuilder WithSteamAppId(string? steamAppId)
    {
        _game.SteamAppId = steamAppId;
        return this;
    }

    public GameBuilder WithExecutableType(ExecutableType executableType)
    {
        _game.ExecutableType = executableType;
        return this;
    }

    public GameBuilder WithDefaultExecutablePath(string path)
    {
        _game.DefaultExecutablePath = path;
        return this;
    }

    public GameBuilder WithDefaultStartCommand(string command)
    {
        _game.DefaultStartCommand = command;
        return this;
    }

    public GameBuilder WithDefaultStopCommand(string? command)
    {
        _game.DefaultStopCommand = command;
        return this;
    }

    public GameBuilder WithDockerSupport(bool supportsDocker, string? defaultImage = null)
    {
        _game.SupportsDocker = supportsDocker;
        _game.DefaultDockerImage = defaultImage;
        return this;
    }

    public GameBuilder WithModSupport(bool supportsMods)
    {
        _game.SupportsMods = supportsMods;
        return this;
    }

    public GameBuilder WithIconPath(string? iconPath)
    {
        _game.IconPath = iconPath;
        return this;
    }

    public GameBuilder AsEnabled()
    {
        _game.IsEnabled = true;
        return this;
    }

    public GameBuilder AsDisabled()
    {
        _game.IsEnabled = false;
        return this;
    }

    public GameBuilder AsMinecraft()
    {
        _game.Name = "Minecraft";
        _game.Slug = "minecraft";
        _game.ExecutableType = ExecutableType.Jar;
        _game.DefaultExecutablePath = "server.jar";
        _game.DefaultStartCommand = "-Xmx2G -Xms1G -jar server.jar nogui";
        _game.SupportsDocker = true;
        _game.DefaultDockerImage = "itzg/minecraft-server";
        _game.SupportsMods = true;
        return this;
    }

    public GameBuilder AsValheim()
    {
        _game.Name = "Valheim";
        _game.Slug = "valheim";
        _game.SteamAppId = "896660";
        _game.ExecutableType = ExecutableType.Exe;
        _game.DefaultExecutablePath = "valheim_server.exe";
        _game.SupportsDocker = true;
        return this;
    }

    public Game Build() => _game;
}

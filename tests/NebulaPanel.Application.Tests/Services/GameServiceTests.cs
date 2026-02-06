using NebulaPanel.Application.Tests.Builders;

namespace NebulaPanel.Application.Tests.Services;

public class GameServiceTests
{
    private readonly Mock<IGameRepository> _mockGameRepo;
    private readonly Mock<IImageService> _mockImageService;
    private readonly Mock<IOfficialGameRegistry> _mockRegistry;
    private readonly GameService _sut;

    public GameServiceTests()
    {
        _mockGameRepo = new Mock<IGameRepository>();
        _mockImageService = new Mock<IImageService>();
        _mockRegistry = new Mock<IOfficialGameRegistry>();
        _sut = new GameService(
            _mockGameRepo.Object,
            _mockImageService.Object,
            _mockRegistry.Object,
            Mock.Of<ILogger<GameService>>());
    }

    #region GetAllGamesAsync

    [Fact]
    public async Task GetAllGamesAsync_ReturnsListItemsWithServerCounts()
    {
        var game1 = new GameBuilder().WithName("Game1").WithSlug("game1").Build();
        var game2 = new GameBuilder().WithName("Game2").WithSlug("game2").Build();
        _mockGameRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Game> { game1, game2 });
        _mockGameRepo.Setup(r => r.GetServerCountAsync(game1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _mockGameRepo.Setup(r => r.GetServerCountAsync(game2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.GetAllGamesAsync();

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Game1");
        result[0].ServerCount.Should().Be(3);
        result[1].ServerCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAllGamesAsync_WithNoGames_ReturnsEmpty()
    {
        _mockGameRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Game>());

        var result = await _sut.GetAllGamesAsync();

        result.Should().BeEmpty();
    }

    #endregion

    #region GetGameByIdAsync

    [Fact]
    public async Task GetGameByIdAsync_WithExistingGame_ReturnsSuccess()
    {
        var game = new GameBuilder().WithName("Minecraft").Build();
        _mockGameRepo.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _mockGameRepo.Setup(r => r.GetServerCountAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var result = await _sut.GetGameByIdAsync(game.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Minecraft");
        result.Value.ServerCount.Should().Be(5);
    }

    [Fact]
    public async Task GetGameByIdAsync_WithNonExistentId_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _mockGameRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var result = await _sut.GetGameByIdAsync(id);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region GetGameBySlugAsync

    [Fact]
    public async Task GetGameBySlugAsync_WithExistingSlug_ReturnsSuccess()
    {
        var game = new GameBuilder().WithName("Valheim").WithSlug("valheim").Build();
        _mockGameRepo.Setup(r => r.GetBySlugAsync("valheim", It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _mockGameRepo.Setup(r => r.GetServerCountAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _sut.GetGameBySlugAsync("valheim");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Valheim");
    }

    [Fact]
    public async Task GetGameBySlugAsync_WithNonExistentSlug_ReturnsFailure()
    {
        _mockGameRepo.Setup(r => r.GetBySlugAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var result = await _sut.GetGameBySlugAsync("unknown");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region CreateGameAsync

    [Fact]
    public async Task CreateGameAsync_WithValidRequest_CreatesAndReturnsDto()
    {
        var request = CreateGameRequest("New Game", "new-game");
        _mockGameRepo.Setup(r => r.SlugExistsAsync("new-game", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.CreateGameAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("New Game");
        result.Value.Slug.Should().Be("new-game");
        result.Value.ServerCount.Should().Be(0);
        _mockGameRepo.Verify(r => r.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateGameAsync_WithDuplicateSlug_ReturnsFailure()
    {
        var request = CreateGameRequest("Game", "taken-slug");
        _mockGameRepo.Setup(r => r.SlugExistsAsync("taken-slug", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.CreateGameAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreateGameAsync_WithRemoteIconUrl_DownloadsAndStoresLocally()
    {
        var request = CreateGameRequest("Game", "game-icon", iconPath: "https://example.com/icon.png");
        _mockGameRepo.Setup(r => r.SlugExistsAsync("game-icon", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockImageService.Setup(s => s.IsRemoteUrl("https://example.com/icon.png")).Returns(true);
        _mockImageService.Setup(s => s.EnsureLocalAsync("https://example.com/icon.png", "games", "game-icon", It.IsAny<CancellationToken>()))
            .ReturnsAsync("/images/games/game-icon.png");

        var result = await _sut.CreateGameAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IconPath.Should().Be("/images/games/game-icon.png");
    }

    [Fact]
    public async Task CreateGameAsync_WithFailedIconDownload_UsesOriginalUrl()
    {
        var request = CreateGameRequest("Game", "game-fail", iconPath: "https://example.com/icon.png");
        _mockGameRepo.Setup(r => r.SlugExistsAsync("game-fail", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockImageService.Setup(s => s.IsRemoteUrl("https://example.com/icon.png")).Returns(true);
        _mockImageService.Setup(s => s.EnsureLocalAsync("https://example.com/icon.png", "games", "game-fail", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.CreateGameAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IconPath.Should().Be("https://example.com/icon.png");
    }

    #endregion

    #region UpdateGameAsync

    [Fact]
    public async Task UpdateGameAsync_WithValidRequest_UpdatesAndReturnsDto()
    {
        var game = new GameBuilder().WithName("Old Name").WithSlug("old-slug").Build();
        _mockGameRepo.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _mockGameRepo.Setup(r => r.SlugExistsAsync("new-slug", game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockGameRepo.Setup(r => r.GetServerCountAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var request = UpdateGameRequest("New Name", "new-slug");

        var result = await _sut.UpdateGameAsync(game.Id, request);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("New Name");
        _mockGameRepo.Verify(r => r.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateGameAsync_WithNonExistentId_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _mockGameRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);
        var request = UpdateGameRequest("Name", "slug");

        var result = await _sut.UpdateGameAsync(id, request);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task UpdateGameAsync_WithOfficialGame_ReturnsFailure()
    {
        var game = new GameBuilder().WithName("Minecraft").Build();
        game.SourceType = GameSourceType.Official;
        _mockGameRepo.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        var request = UpdateGameRequest("Modified", "modified");

        var result = await _sut.UpdateGameAsync(game.Id, request);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Official games");
    }

    [Fact]
    public async Task UpdateGameAsync_WithDuplicateSlug_ReturnsFailure()
    {
        var game = new GameBuilder().WithName("Game").WithSlug("game").Build();
        _mockGameRepo.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _mockGameRepo.Setup(r => r.SlugExistsAsync("taken", game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var request = UpdateGameRequest("Game", "taken");

        var result = await _sut.UpdateGameAsync(game.Id, request);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
    }

    #endregion

    #region DeleteGameAsync

    [Fact]
    public async Task DeleteGameAsync_WithExistingGameNoServers_ReturnsSuccess()
    {
        var id = Guid.NewGuid();
        _mockGameRepo.Setup(r => r.ExistsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockGameRepo.Setup(r => r.GetServerCountAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.DeleteGameAsync(id);

        result.IsSuccess.Should().BeTrue();
        _mockGameRepo.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteGameAsync_WithNonExistentGame_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _mockGameRepo.Setup(r => r.ExistsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.DeleteGameAsync(id);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task DeleteGameAsync_WithServersStillUsing_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _mockGameRepo.Setup(r => r.ExistsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockGameRepo.Setup(r => r.GetServerCountAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await _sut.DeleteGameAsync(id);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("3 server(s)");
    }

    #endregion

    #region GetOfficialGamesAsync

    [Fact]
    public async Task GetOfficialGamesAsync_ReturnsRegisteredProviders()
    {
        var mockProvider = new Mock<IOfficialGameProvider>();
        mockProvider.Setup(p => p.GetGameDefinition()).Returns(new Domain.ValueObjects.GameDefinition
        {
            Name = "Minecraft",
            Slug = "minecraft",
            ExecutableType = ExecutableType.Jar,
            DefaultExecutablePath = "server.jar",
            DefaultStartCommand = "java -jar server.jar",
            SupportsMods = true,
            SupportsDocker = true,
            IconPath = "/icons/minecraft.png"
        });
        _mockRegistry.Setup(r => r.GetAllProviders())
            .Returns(new List<IOfficialGameProvider> { mockProvider.Object });

        var result = await _sut.GetOfficialGamesAsync();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Minecraft");
        result[0].Slug.Should().Be("minecraft");
        result[0].SupportsMods.Should().BeTrue();
    }

    #endregion

    #region GetAvailableVersionsAsync

    [Fact]
    public async Task GetAvailableVersionsAsync_WithValidSlug_ReturnsVersions()
    {
        var mockProvider = new Mock<IOfficialGameProvider>();
        mockProvider.Setup(p => p.GetAvailableVersionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Domain.ValueObjects.GameVersionInfo>
            {
                new() { Version = "1.20.4", DisplayName = "1.20.4", IsRecommended = true }
            });
        _mockRegistry.Setup(r => r.GetProvider("minecraft"))
            .Returns(mockProvider.Object);

        var result = await _sut.GetAvailableVersionsAsync("minecraft");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Version.Should().Be("1.20.4");
    }

    [Fact]
    public async Task GetAvailableVersionsAsync_WithUnknownSlug_ReturnsFailure()
    {
        _mockRegistry.Setup(r => r.GetProvider("unknown"))
            .Returns((IOfficialGameProvider?)null);

        var result = await _sut.GetAvailableVersionsAsync("unknown");

        result.IsFailure.Should().BeTrue();
        result.ErrorInfo?.Code.Should().Be(ErrorCode.NotFound);
    }

    #endregion

    private static CreateGameRequest CreateGameRequest(string name, string slug, string? iconPath = null)
    {
        return new CreateGameRequest(
            Name: name,
            Slug: slug,
            SteamAppId: null,
            ExecutableType: ExecutableType.Exe,
            DefaultExecutablePath: "server.exe",
            DefaultStartCommand: "./server",
            DefaultStopCommand: null,
            SupportsDocker: false,
            DefaultDockerImage: null,
            DockerDataPath: null,
            IconPath: iconPath,
            SupportsMods: false,
            ModProviders: null,
            RconDefaults: null,
            ConfigurationSchemas: null);
    }

    private static UpdateGameRequest UpdateGameRequest(string name, string slug)
    {
        return new UpdateGameRequest(
            Name: name,
            Slug: slug,
            SteamAppId: null,
            ExecutableType: ExecutableType.Exe,
            DefaultExecutablePath: "server.exe",
            DefaultStartCommand: "./server",
            DefaultStopCommand: null,
            SupportsDocker: false,
            DefaultDockerImage: null,
            DockerDataPath: null,
            IconPath: null,
            SupportsMods: false,
            ModProviders: null,
            RconDefaults: null,
            ConfigurationSchemas: null);
    }
}

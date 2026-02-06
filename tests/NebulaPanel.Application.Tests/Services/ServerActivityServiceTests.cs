using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Application.Tests.Services;

public class ServerActivityServiceTests
{
    private readonly Mock<IServerActivityRepository> _mockRepo;
    private readonly ServerActivityService _sut;

    public ServerActivityServiceTests()
    {
        _mockRepo = new Mock<IServerActivityRepository>();
        _sut = new ServerActivityService(_mockRepo.Object);
    }

    #region LogActivityAsync

    [Fact]
    public async Task LogActivityAsync_CreatesActivityWithCorrectFields()
    {
        var serverId = Guid.NewGuid();

        await _sut.LogActivityAsync(serverId, ServerActivityType.Started, "Server started");

        _mockRepo.Verify(r => r.AddAsync(
            It.Is<ServerActivity>(a =>
                a.ServerId == serverId &&
                a.ActivityType == ServerActivityType.Started &&
                a.Description == "Server started" &&
                a.UserId == null &&
                a.Metadata == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogActivityAsync_WithOptionalUserIdAndMetadata()
    {
        var serverId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await _sut.LogActivityAsync(serverId, ServerActivityType.ConfigChanged, "Config updated", userId, "{\"key\":\"value\"}");

        _mockRepo.Verify(r => r.AddAsync(
            It.Is<ServerActivity>(a =>
                a.ServerId == serverId &&
                a.UserId == userId &&
                a.Metadata == "{\"key\":\"value\"}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetRecentAsync

    [Fact]
    public async Task GetRecentAsync_ReturnsMappedDtos()
    {
        var activities = new List<ServerActivity>
        {
            CreateActivity(ServerActivityType.Started, "Server started"),
            CreateActivity(ServerActivityType.Stopped, "Server stopped")
        };
        _mockRepo.Setup(r => r.GetRecentAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activities);

        var result = await _sut.GetRecentAsync(10);

        result.Should().HaveCount(2);
        result[0].Description.Should().Be("Server started");
        result[0].Category.Should().Be("Server");
        result[1].Description.Should().Be("Server stopped");
    }

    [Fact]
    public async Task GetRecentAsync_WithEmptyList_ReturnsEmpty()
    {
        _mockRepo.Setup(r => r.GetRecentAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServerActivity>());

        var result = await _sut.GetRecentAsync(10);

        result.Should().BeEmpty();
    }

    #endregion

    #region GetByServerAsync

    [Fact]
    public async Task GetByServerAsync_ReturnsMappedDtos()
    {
        var serverId = Guid.NewGuid();
        var activities = new List<ServerActivity>
        {
            CreateActivity(ServerActivityType.BackupCreated, "Backup created", serverId)
        };
        _mockRepo.Setup(r => r.GetByServerIdAsync(serverId, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activities);

        var result = await _sut.GetByServerAsync(serverId, 20);

        result.Should().HaveCount(1);
        result[0].Description.Should().Be("Backup created");
    }

    [Fact]
    public async Task GetByServerAsync_WithEmptyList_ReturnsEmpty()
    {
        var serverId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByServerIdAsync(serverId, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServerActivity>());

        var result = await _sut.GetByServerAsync(serverId, 20);

        result.Should().BeEmpty();
    }

    #endregion

    #region Icon and Color Mapping

    [Theory]
    [InlineData(ServerActivityType.Started, "play", "success")]
    [InlineData(ServerActivityType.Stopped, "square", "info")]
    [InlineData(ServerActivityType.Crashed, "alert-triangle", "error")]
    [InlineData(ServerActivityType.Restarted, "refresh-cw", "info")]
    [InlineData(ServerActivityType.Killed, "x-circle", "warning")]
    [InlineData(ServerActivityType.InstallStarted, "download", "info")]
    [InlineData(ServerActivityType.InstallCompleted, "check-circle", "success")]
    [InlineData(ServerActivityType.InstallFailed, "x-circle", "error")]
    [InlineData(ServerActivityType.UpdateStarted, "download-cloud", "info")]
    [InlineData(ServerActivityType.UpdateCompleted, "check-circle", "success")]
    [InlineData(ServerActivityType.UpdateFailed, "x-circle", "error")]
    [InlineData(ServerActivityType.BackupCreated, "archive", "success")]
    [InlineData(ServerActivityType.BackupRestored, "upload", "info")]
    [InlineData(ServerActivityType.ConfigChanged, "settings", "info")]
    [InlineData(ServerActivityType.ModInstalled, "package-plus", "success")]
    [InlineData(ServerActivityType.ModRemoved, "package-minus", "info")]
    [InlineData(ServerActivityType.CommandExecuted, "terminal", "info")]
    public async Task GetRecentAsync_MapsIconAndColorCorrectly(ServerActivityType activityType, string expectedIcon, string expectedColor)
    {
        var activities = new List<ServerActivity> { CreateActivity(activityType, "test") };
        _mockRepo.Setup(r => r.GetRecentAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activities);

        var result = await _sut.GetRecentAsync(1);

        result[0].IconName.Should().Be(expectedIcon);
        result[0].StatusColor.Should().Be(expectedColor);
    }

    #endregion

    private static ServerActivity CreateActivity(ServerActivityType type = ServerActivityType.Started, string description = "Test", Guid? serverId = null)
    {
        var sid = serverId ?? Guid.NewGuid();
        return new ServerActivity
        {
            Id = Guid.NewGuid(),
            ServerId = sid,
            Server = new GameServer
            {
                Id = sid,
                Name = "TestServer",
                InstallPath = "/srv/test",
                PrimaryPort = 25565,
                BindAddress = "0.0.0.0",
                OwnerId = Guid.NewGuid(),
                GameId = Guid.NewGuid()
            },
            Timestamp = DateTime.UtcNow,
            ActivityType = type,
            Description = description
        };
    }
}

using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Application.Tests.Services;

public class NotificationServiceTests
{
    private readonly Mock<INotificationRepository> _mockRepo;
    private readonly NotificationService _sut;

    public NotificationServiceTests()
    {
        _mockRepo = new Mock<INotificationRepository>();
        _sut = new NotificationService(_mockRepo.Object, Mock.Of<ILogger<NotificationService>>());
    }

    #region GetUserNotificationsAsync

    [Fact]
    public async Task GetUserNotificationsAsync_ReturnsMappedDtos()
    {
        var userId = Guid.NewGuid();
        var notifications = new List<Notification>
        {
            CreateNotification(userId, NotificationType.Info, "Test", "Hello"),
            CreateNotification(userId, NotificationType.ServerStatus, "Server Up", "Running")
        };
        _mockRepo.Setup(r => r.GetByUserIdAsync(userId, 50, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        var result = await _sut.GetUserNotificationsAsync(userId);

        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Test");
        result[1].Title.Should().Be("Server Up");
    }

    [Fact]
    public async Task GetUserNotificationsAsync_WithEmptyList_ReturnsEmpty()
    {
        var userId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByUserIdAsync(userId, 50, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification>());

        var result = await _sut.GetUserNotificationsAsync(userId);

        result.Should().BeEmpty();
    }

    #endregion

    #region GetSummaryAsync

    [Fact]
    public async Task GetSummaryAsync_ReturnsCorrectCounts()
    {
        var userId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _mockRepo.Setup(r => r.GetByUserIdAsync(userId, 5, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification> { CreateNotification(userId) });
        _mockRepo.Setup(r => r.GetCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var result = await _sut.GetSummaryAsync(userId);

        result.UnreadCount.Should().Be(3);
        result.TotalCount.Should().Be(5);
        result.RecentNotifications.Should().HaveCount(1);
    }

    #endregion

    #region GetUnreadCountAsync

    [Fact]
    public async Task GetUnreadCountAsync_DelegatesToRepository()
    {
        var userId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var count = await _sut.GetUnreadCountAsync(userId);

        count.Should().Be(7);
    }

    #endregion

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_CreatesNotificationAndReturnsDto()
    {
        var userId = Guid.NewGuid();
        var request = new CreateNotificationRequest(
            UserId: userId,
            Type: NotificationType.Info,
            Title: "Test Notification",
            Message: "This is a test",
            ActionUrl: "/test",
            RelatedEntityId: Guid.NewGuid()
        );

        var result = await _sut.CreateAsync(request);

        result.Title.Should().Be("Test Notification");
        result.Message.Should().Be("This is a test");
        result.Type.Should().Be(NotificationType.Info);
        result.IsRead.Should().BeFalse();
        result.ActionUrl.Should().Be("/test");
        _mockRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Delegation Methods

    [Fact]
    public async Task MarkAsReadAsync_DelegatesToRepository()
    {
        var id = Guid.NewGuid();

        await _sut.MarkAsReadAsync(id);

        _mockRepo.Verify(r => r.MarkAsReadAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_DelegatesToRepository()
    {
        var userId = Guid.NewGuid();

        await _sut.MarkAllAsReadAsync(userId);

        _mockRepo.Verify(r => r.MarkAllAsReadAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_DelegatesToRepository()
    {
        var id = Guid.NewGuid();

        await _sut.DeleteAsync(id);

        _mockRepo.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAllForUserAsync_DelegatesToRepository()
    {
        var userId = Guid.NewGuid();

        await _sut.DeleteAllForUserAsync(userId);

        _mockRepo.Verify(r => r.DeleteAllForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region NotifyServerStatusAsync

    [Fact]
    public async Task NotifyServerStatusAsync_CreatesServerStatusNotification()
    {
        var userId = Guid.NewGuid();
        var serverId = Guid.NewGuid();

        var result = await _sut.NotifyServerStatusAsync(userId, serverId, "MyServer", "Running");

        result.Type.Should().Be(NotificationType.ServerStatus);
        result.Title.Should().Be("Server Running");
        result.Message.Should().Contain("MyServer");
        result.Message.Should().Contain("running");
        result.ActionUrl.Should().Contain(serverId.ToString());
        _mockRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region NotifyBackupCompletedAsync

    [Fact]
    public async Task NotifyBackupCompletedAsync_CreatesBackupCompletedNotification()
    {
        var userId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var backupId = Guid.NewGuid();

        var result = await _sut.NotifyBackupCompletedAsync(userId, serverId, "MyServer", backupId);

        result.Type.Should().Be(NotificationType.BackupCompleted);
        result.Title.Should().Be("Backup Completed");
        result.Message.Should().Contain("MyServer");
        result.ActionUrl.Should().Contain("backups");
    }

    #endregion

    #region NotifyBackupFailedAsync

    [Fact]
    public async Task NotifyBackupFailedAsync_CreatesBackupFailedNotification()
    {
        var userId = Guid.NewGuid();
        var serverId = Guid.NewGuid();

        var result = await _sut.NotifyBackupFailedAsync(userId, serverId, "MyServer", "disk full");

        result.Type.Should().Be(NotificationType.BackupFailed);
        result.Title.Should().Be("Backup Failed");
        result.Message.Should().Contain("disk full");
    }

    #endregion

    #region NotifyUpdateAvailableAsync

    [Fact]
    public async Task NotifyUpdateAvailableAsync_CreatesUpdateNotification()
    {
        var userId = Guid.NewGuid();

        var result = await _sut.NotifyUpdateAvailableAsync(userId, "Update Available", "v2.0 is out", "/updates");

        result.Type.Should().Be(NotificationType.UpdateAvailable);
        result.Title.Should().Be("Update Available");
        result.Message.Should().Be("v2.0 is out");
        result.ActionUrl.Should().Be("/updates");
    }

    #endregion

    #region NotifyInstallCompletedAsync

    [Fact]
    public async Task NotifyInstallCompletedAsync_CreatesInstallCompletedNotification()
    {
        var userId = Guid.NewGuid();
        var serverId = Guid.NewGuid();

        var result = await _sut.NotifyInstallCompletedAsync(userId, serverId, "MyServer", "Minecraft");

        result.Type.Should().Be(NotificationType.InstallCompleted);
        result.Title.Should().Be("Installation Completed");
        result.Message.Should().Contain("Minecraft");
        result.Message.Should().Contain("MyServer");
    }

    #endregion

    #region NotifyInstallFailedAsync

    [Fact]
    public async Task NotifyInstallFailedAsync_CreatesInstallFailedNotification()
    {
        var userId = Guid.NewGuid();
        var serverId = Guid.NewGuid();

        var result = await _sut.NotifyInstallFailedAsync(userId, serverId, "MyServer", "Valheim", "download error");

        result.Type.Should().Be(NotificationType.InstallFailed);
        result.Title.Should().Be("Installation Failed");
        result.Message.Should().Contain("Valheim");
        result.Message.Should().Contain("download error");
    }

    #endregion

    private static Notification CreateNotification(Guid? userId = null, NotificationType type = NotificationType.Info, string title = "Test", string message = "Test message")
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            Type = type,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
    }
}

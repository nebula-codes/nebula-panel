using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Application.Tests.Services;

public class AnnouncementServiceTests
{
    private readonly Mock<IAnnouncementRepository> _mockRepo;
    private readonly AnnouncementService _sut;

    public AnnouncementServiceTests()
    {
        _mockRepo = new Mock<IAnnouncementRepository>();
        _sut = new AnnouncementService(_mockRepo.Object);
    }

    #region GetActiveAnnouncementsAsync

    [Fact]
    public async Task GetActiveAnnouncementsAsync_ReturnsMappedDtos()
    {
        var announcements = new List<Announcement>
        {
            CreateAnnouncement("Welcome", "Hello everyone"),
            CreateAnnouncement("Maintenance", "Scheduled downtime")
        };
        _mockRepo.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(announcements);

        var result = await _sut.GetActiveAnnouncementsAsync();

        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Welcome");
        result[1].Title.Should().Be("Maintenance");
    }

    [Fact]
    public async Task GetActiveAnnouncementsAsync_WithEmptyList_ReturnsEmpty()
    {
        _mockRepo.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Announcement>());

        var result = await _sut.GetActiveAnnouncementsAsync();

        result.Should().BeEmpty();
    }

    #endregion

    #region GetAllAnnouncementsAsync

    [Fact]
    public async Task GetAllAnnouncementsAsync_ReturnsMappedDtos()
    {
        var announcements = new List<Announcement> { CreateAnnouncement("Test", "Content") };
        _mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(announcements);

        var result = await _sut.GetAllAnnouncementsAsync();

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Test");
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsSuccess()
    {
        var announcement = CreateAnnouncement("Found", "Content");
        _mockRepo.Setup(r => r.GetByIdAsync(announcement.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(announcement);

        var result = await _sut.GetByIdAsync(announcement.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("Found");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Announcement?)null);

        var result = await _sut.GetByIdAsync(id);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesAndReturnsDto()
    {
        var userId = Guid.NewGuid();
        var request = new CreateAnnouncementRequest("New Post", "Content here", "Info", false, null);
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => CreateAnnouncement("New Post", "Content here", id));

        var result = await _sut.CreateAsync(request, userId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("New Post");
        _mockRepo.Verify(r => r.AddAsync(It.IsAny<Announcement>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidType_ReturnsFailure()
    {
        var request = new CreateAnnouncementRequest("Title", "Content", "InvalidType", false, null);

        var result = await _sut.CreateAsync(request, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid announcement type");
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_WithExistingId_UpdatesAndReturnsDto()
    {
        var announcement = CreateAnnouncement("Old Title", "Old Content");
        _mockRepo.Setup(r => r.GetByIdAsync(announcement.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(announcement);
        var request = new UpdateAnnouncementRequest("New Title", "New Content", "Warning", true, true, null, 1);

        var result = await _sut.UpdateAsync(announcement.Id, request);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("New Title");
        result.Value.Type.Should().Be("Warning");
        _mockRepo.Verify(r => r.UpdateAsync(It.IsAny<Announcement>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentId_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Announcement?)null);
        var request = new UpdateAnnouncementRequest("Title", "Content", "Info", true, false, null, 0);

        var result = await _sut.UpdateAsync(id, request);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidType_ReturnsFailure()
    {
        var announcement = CreateAnnouncement("Title", "Content");
        _mockRepo.Setup(r => r.GetByIdAsync(announcement.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(announcement);
        var request = new UpdateAnnouncementRequest("Title", "Content", "BadType", true, false, null, 0);

        var result = await _sut.UpdateAsync(announcement.Id, request);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid announcement type");
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_WithExistingId_ReturnsSuccess()
    {
        var announcement = CreateAnnouncement("To Delete", "Content");
        _mockRepo.Setup(r => r.GetByIdAsync(announcement.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(announcement);

        var result = await _sut.DeleteAsync(announcement.Id);

        result.IsSuccess.Should().BeTrue();
        _mockRepo.Verify(r => r.DeleteAsync(announcement.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Announcement?)null);

        var result = await _sut.DeleteAsync(id);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    #endregion

    private static Announcement CreateAnnouncement(string title = "Test", string content = "Content", Guid? id = null)
    {
        var userId = Guid.NewGuid();
        return new Announcement
        {
            Id = id ?? Guid.NewGuid(),
            Title = title,
            Content = content,
            Type = AnnouncementType.Info,
            IsActive = true,
            IsPinned = false,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId,
            CreatedByUser = new User
            {
                Id = userId,
                Username = "testuser",
                Email = "test@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow
            }
        };
    }
}

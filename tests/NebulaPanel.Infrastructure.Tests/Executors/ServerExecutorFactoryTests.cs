using NebulaPanel.Infrastructure.Executors;

namespace NebulaPanel.Infrastructure.Tests.Executors;

public class ServerExecutorFactoryTests
{
    [Fact]
    public void GetExecutor_WithNativeType_ReturnsNativeExecutor()
    {
        // Arrange
        var nativeExecutor = new Mock<IServerExecutor>();
        nativeExecutor.Setup(e => e.DeploymentType).Returns(ServerDeploymentType.Native);

        var factory = new ServerExecutorFactory([nativeExecutor.Object]);

        // Act
        var result = factory.GetExecutor(ServerDeploymentType.Native);

        // Assert
        result.Should().Be(nativeExecutor.Object);
    }

    [Fact]
    public void GetExecutor_WithDockerType_ReturnsDockerExecutor()
    {
        // Arrange
        var dockerExecutor = new Mock<IServerExecutor>();
        dockerExecutor.Setup(e => e.DeploymentType).Returns(ServerDeploymentType.Docker);

        var factory = new ServerExecutorFactory([dockerExecutor.Object]);

        // Act
        var result = factory.GetExecutor(ServerDeploymentType.Docker);

        // Assert
        result.Should().Be(dockerExecutor.Object);
    }

    [Fact]
    public void GetExecutor_WithMultipleExecutors_ReturnsCorrectExecutor()
    {
        // Arrange
        var nativeExecutor = new Mock<IServerExecutor>();
        nativeExecutor.Setup(e => e.DeploymentType).Returns(ServerDeploymentType.Native);

        var dockerExecutor = new Mock<IServerExecutor>();
        dockerExecutor.Setup(e => e.DeploymentType).Returns(ServerDeploymentType.Docker);

        var factory = new ServerExecutorFactory([nativeExecutor.Object, dockerExecutor.Object]);

        // Act
        var nativeResult = factory.GetExecutor(ServerDeploymentType.Native);
        var dockerResult = factory.GetExecutor(ServerDeploymentType.Docker);

        // Assert
        nativeResult.Should().Be(nativeExecutor.Object);
        dockerResult.Should().Be(dockerExecutor.Object);
    }

    [Fact]
    public void GetExecutor_WithUnregisteredType_ThrowsNotSupportedException()
    {
        // Arrange
        var nativeExecutor = new Mock<IServerExecutor>();
        nativeExecutor.Setup(e => e.DeploymentType).Returns(ServerDeploymentType.Native);

        var factory = new ServerExecutorFactory([nativeExecutor.Object]);

        // Act & Assert
        var act = () => factory.GetExecutor(ServerDeploymentType.Docker);
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Docker*");
    }

    [Fact]
    public void GetAllExecutors_ReturnsAllRegisteredExecutors()
    {
        // Arrange
        var nativeExecutor = new Mock<IServerExecutor>();
        nativeExecutor.Setup(e => e.DeploymentType).Returns(ServerDeploymentType.Native);

        var dockerExecutor = new Mock<IServerExecutor>();
        dockerExecutor.Setup(e => e.DeploymentType).Returns(ServerDeploymentType.Docker);

        var factory = new ServerExecutorFactory([nativeExecutor.Object, dockerExecutor.Object]);

        // Act
        var result = factory.GetAllExecutors();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(nativeExecutor.Object);
        result.Should().Contain(dockerExecutor.Object);
    }

    [Fact]
    public void GetAllExecutors_WithNoExecutors_ReturnsEmptyCollection()
    {
        // Arrange
        var factory = new ServerExecutorFactory([]);

        // Act
        var result = factory.GetAllExecutors();

        // Assert
        result.Should().BeEmpty();
    }
}

using EtlKit.AI.Models;
using EtlKit.Common;
using Microsoft.Extensions.AI;
using Moq;

namespace EtlKit.AI.Tests;

public class AIChatClientFactoryTests
{
    [Fact]
    public void CustomFactory_IsUsed_ReturnsSameInstance()
    {
        // Arrange
        var settings = new ApiSettings { ApiModel = "gpt-test", ApiKey = "k" };
        var mock = new Mock<IChatClient>(MockBehavior.Strict);
        mock.As<IDisposable>().Setup(d => d.Dispose());

        // Act
        using var client = AIChatClientFactory.Create(settings, _ => mock.Object);

        // Assert
        Assert.Equal(client, mock.Object);
    }

    [Fact]
    public void Create_WithApiKeyInSettings_ShouldReturnClient_NotNull()
    {
        // Arrange
        var settings = new ApiSettings { ApiModel = "gpt-test", ApiKey = "abc" };

        // Act
        using var client = AIChatClientFactory.Create(settings);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void Create_WithApiBaseUriInSettings_ShouldReturnClient_NotNull()
    {
        // Arrange
        var settings = new ApiSettings
        {
            ApiModel = "gpt-test",
            ApiKey = "abc",
            ApiBaseUrl = "http://localhost",
        };

        // Act
        using var client = AIChatClientFactory.Create(settings);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void Create_WithoutApiKeyAnywhere_ShouldThrowEtlKitException()
    {
        // Arrange
        var settings = new ApiSettings { ApiModel = "gpt-test", ApiKey = null };

        // Act
        var act = () => AIChatClientFactory.Create(settings);

        // Assert
        Assert.Throws<EtlKitException>(act);
    }
}

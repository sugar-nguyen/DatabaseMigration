using DatabaseMigrationTool.Services;
using DatabaseMigrationTool.Models;
using FluentAssertions;
using Moq;
using Microsoft.Data.SqlClient;
using System.IO;

namespace DatabaseMigrationTool.Tests.Services;

public class ConnectionSettingsServiceTests
{
    private readonly string _testSettingsPath;
    private readonly ConnectionSettingsService _service;

    public ConnectionSettingsServiceTests()
    {
        // Use temp directory for test settings
        _testSettingsPath = Path.Combine(Path.GetTempPath(), "DatabaseMigrationTool_Test", "connections.json");
        
        // Clean up before each test
        if (File.Exists(_testSettingsPath))
        {
            File.Delete(_testSettingsPath);
        }

        _service = new ConnectionSettingsService();
    }

    [Fact]
    public void GetConnections_WhenNoConnectionsExist_ReturnsEmptyList()
    {
        // Arrange & Act
        var connections = _service.GetConnections();

        // Assert
        connections.Should().NotBeNull();
        connections.Should().BeEmpty();
    }

    [Fact]
    public void SaveConnection_WhenValidConnection_SavesSuccessfully()
    {
        // Arrange
        var connection = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "TestDB",
            UseWindowsAuthentication = true,
            Username = "",
            Password = ""
        };

        // Act
        _service.SaveConnection(connection);
        var connections = _service.GetConnections();

        // Assert
        connections.Should().HaveCount(1);
        connections[0].ServerName.Should().Be("localhost");
        connections[0].DatabaseName.Should().Be("TestDB");
    }

    [Fact]
    public void SaveConnection_WhenDuplicateConnection_UpdatesExisting()
    {
        // Arrange
        var connection1 = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "TestDB",
            UseWindowsAuthentication = true,
            LastUsed = DateTime.Now.AddDays(-1)
        };

        var connection2 = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "TestDB",
            UseWindowsAuthentication = true,
            LastUsed = DateTime.Now
        };

        // Act
        _service.SaveConnection(connection1);
        _service.SaveConnection(connection2);
        var connections = _service.GetConnections();

        // Assert
        connections.Should().HaveCount(1);
        connections[0].LastUsed.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RemoveConnection_WhenConnectionExists_RemovesSuccessfully()
    {
        // Arrange
        var connection = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "TestDB",
            UseWindowsAuthentication = true
        };
        _service.SaveConnection(connection);

        // Act
        _service.RemoveConnection(connection);
        var connections = _service.GetConnections();

        // Assert
        connections.Should().BeEmpty();
    }

    [Fact]
    public void GetServerConnections_ReturnsOnlyServerLevelConnections()
    {
        // Arrange
        var serverConnection = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "",
            UseWindowsAuthentication = true
        };

        var databaseConnection = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "TestDB",
            UseWindowsAuthentication = true
        };

        _service.SaveConnection(serverConnection);
        _service.SaveConnection(databaseConnection);

        // Act
        var serverConnections = _service.GetServerConnections();

        // Assert
        serverConnections.Should().HaveCount(1);
        serverConnections[0].DatabaseName.Should().BeEmpty();
    }

    [Fact]
    public void UpdateLastUsed_WhenConnectionExists_UpdatesTimestamp()
    {
        // Arrange
        var connection = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "TestDB",
            UseWindowsAuthentication = true,
            LastUsed = DateTime.Now.AddDays(-5)
        };
        _service.SaveConnection(connection);

        // Act
        System.Threading.Thread.Sleep(100); // Small delay to ensure timestamp difference
        _service.UpdateLastUsed(connection);
        var connections = _service.GetConnections();

        // Assert
        connections[0].LastUsed.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SaveServerConnection_CreatesServerLevelConnection()
    {
        // Arrange
        var connection = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "SomeDB",
            UseWindowsAuthentication = true
        };

        // Act
        _service.SaveServerConnection(connection);
        var serverConnections = _service.GetServerConnections();

        // Assert
        serverConnections.Should().HaveCount(1);
        serverConnections[0].ServerName.Should().Be("localhost");
        serverConnections[0].DatabaseName.Should().BeEmpty();
    }
}

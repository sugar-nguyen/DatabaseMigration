using DatabaseMigrationTool.Models;
using FluentAssertions;

namespace DatabaseMigrationTool.Tests.Models;

public class ConnectionSettingsTests
{
    [Fact]
    public void GetConnectionString_WithWindowsAuthentication_ReturnsCorrectString()
    {
        // Arrange
        var settings = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "TestDB",
            UseWindowsAuthentication = true
        };

        // Act
        var connectionString = settings.GetConnectionString();

        // Assert
        connectionString.Should().Contain("Data Source=localhost");
        connectionString.Should().Contain("Initial Catalog=TestDB");
        connectionString.Should().Contain("Integrated Security=True");
        connectionString.Should().NotContain("User ID");
        connectionString.Should().NotContain("Password");
    }

    [Fact]
    public void GetConnectionString_WithSqlAuthentication_ReturnsCorrectString()
    {
        // Arrange
        var settings = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "TestDB",
            UseWindowsAuthentication = false,
            Username = "sa",
            Password = "P@ssw0rd"
        };

        // Act
        var connectionString = settings.GetConnectionString();

        // Assert
        connectionString.Should().Contain("Data Source=localhost");
        connectionString.Should().Contain("Initial Catalog=TestDB");
        connectionString.Should().Contain("User ID=sa");
        connectionString.Should().Contain("Password=P@ssw0rd");
        connectionString.Should().NotContain("Integrated Security");
    }

    [Fact]
    public void GetConnectionString_WithoutDatabaseName_ReturnsServerConnectionString()
    {
        // Arrange
        var settings = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "",
            UseWindowsAuthentication = true
        };

        // Act
        var connectionString = settings.GetConnectionString();

        // Assert
        connectionString.Should().Contain("Data Source=localhost");
        connectionString.Should().NotContain("Initial Catalog");
    }

    [Fact]
    public void GetUniqueKey_ReturnsSameKeyForSameConnection()
    {
        // Arrange
        var settings1 = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "TestDB",
            UseWindowsAuthentication = true,
            Username = ""
        };

        var settings2 = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "TestDB",
            UseWindowsAuthentication = true,
            Username = ""
        };

        // Act
        var key1 = settings1.GetUniqueKey();
        var key2 = settings2.GetUniqueKey();

        // Assert
        key1.Should().Be(key2);
    }

    [Fact]
    public void GetUniqueKey_ReturnsDifferentKeyForDifferentConnections()
    {
        // Arrange
        var settings1 = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "TestDB1",
            UseWindowsAuthentication = true,
            Username = ""
        };

        var settings2 = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "TestDB2",
            UseWindowsAuthentication = true,
            Username = ""
        };

        // Act
        var key1 = settings1.GetUniqueKey();
        var key2 = settings2.GetUniqueKey();

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void ToString_WithWindowsAuth_ReturnsCorrectDisplayString()
    {
        // Arrange
        var settings = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "TestDB",
            UseWindowsAuthentication = true
        };

        // Act
        var displayString = settings.ToString();

        // Assert
        displayString.Should().Contain("localhost");
        displayString.Should().Contain("TestDB");
        displayString.Should().Contain("Windows Authentication");
    }

    [Fact]
    public void ToString_WithSqlAuth_ReturnsCorrectDisplayString()
    {
        // Arrange
        var settings = new ConnectionSettings
        {
            ServerName = "localhost",
            DatabaseName = "TestDB",
            UseWindowsAuthentication = false,
            Username = "sa"
        };

        // Act
        var displayString = settings.ToString();

        // Assert
        displayString.Should().Contain("localhost");
        displayString.Should().Contain("TestDB");
        displayString.Should().Contain("sa");
        displayString.Should().Contain("SQL Auth");
    }
}

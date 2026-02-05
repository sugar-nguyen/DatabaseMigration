using DatabaseMigrationTool.Models;
using FluentAssertions;

namespace DatabaseMigrationTool.Tests.Models;

public class StoredProcedureTests
{
    [Fact]
    public void FullName_ReturnsSchemaAndName()
    {
        // Arrange
        var sp = new StoredProcedure
        {
            Schema = "dbo",
            Name = "GetUsers"
        };

        // Act
        var fullName = sp.FullName;

        // Assert
        fullName.Should().Be("dbo.GetUsers");
    }

    [Fact]
    public void IsSelected_DefaultsToFalse()
    {
        // Arrange & Act
        var sp = new StoredProcedure
        {
            Schema = "dbo",
            Name = "GetUsers"
        };

        // Assert
        sp.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void PropertyChanged_RaisesWhenIsSelectedChanged()
    {
        // Arrange
        var sp = new StoredProcedure
        {
            Schema = "dbo",
            Name = "GetUsers"
        };

        var propertyChangedRaised = false;
        sp.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(StoredProcedure.IsSelected))
            {
                propertyChangedRaised = true;
            }
        };

        // Act
        sp.IsSelected = true;

        // Assert
        propertyChangedRaised.Should().BeTrue();
    }
}

public class TableTests
{
    [Fact]
    public void FullName_ReturnsSchemaAndName()
    {
        // Arrange
        var table = new Table
        {
            Schema = "dbo",
            Name = "Users"
        };

        // Act
        var fullName = table.FullName;

        // Assert
        fullName.Should().Be("dbo.Users");
    }

    [Fact]
    public void IsSelected_DefaultsToFalse()
    {
        // Arrange & Act
        var table = new Table
        {
            Schema = "dbo",
            Name = "Users"
        };

        // Assert
        table.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void PropertyChanged_RaisesWhenIsSelectedChanged()
    {
        // Arrange
        var table = new Table
        {
            Schema = "dbo",
            Name = "Users"
        };

        var propertyChangedRaised = false;
        table.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(Table.IsSelected))
            {
                propertyChangedRaised = true;
            }
        };

        // Act
        table.IsSelected = true;

        // Assert
        propertyChangedRaised.Should().BeTrue();
    }
}

public class TargetDatabaseTests
{
    [Fact]
    public void IsSelected_DefaultsToFalse()
    {
        // Arrange & Act
        var targetDb = new TargetDatabase
        {
            Name = "TestDB"
        };

        // Assert
        targetDb.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void PropertyChanged_RaisesWhenIsSelectedChanged()
    {
        // Arrange
        var targetDb = new TargetDatabase
        {
            Name = "TestDB"
        };

        var propertyChangedRaised = false;
        targetDb.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(TargetDatabase.IsSelected))
            {
                propertyChangedRaised = true;
            }
        };

        // Act
        targetDb.IsSelected = true;

        // Assert
        propertyChangedRaised.Should().BeTrue();
    }

    [Fact]
    public void Name_CanBeSetAndRetrieved()
    {
        // Arrange & Act
        var targetDb = new TargetDatabase
        {
            Name = "ProductionDB"
        };

        // Assert
        targetDb.Name.Should().Be("ProductionDB");
    }
}

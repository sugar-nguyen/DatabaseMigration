using FluentAssertions;

namespace DatabaseMigrationTool.Tests.Validation;

public class ScriptValidationTests
{
    [Fact]
    public void ValidateStoredProcedureScript_WithValidCreateProcedure_ReturnsValid()
    {
        // Arrange
        var script = @"
CREATE PROCEDURE dbo.GetUsers
AS
BEGIN
    SELECT * FROM Users
END";

        // Act
        var result = ValidateStoredProcedureScript(script);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ScriptType.Should().Be("CREATE PROCEDURE");
        result.ProcedureName.Should().Be("dbo.GetUsers");
    }

    [Fact]
    public void ValidateStoredProcedureScript_WithValidAlterProcedure_ReturnsValid()
    {
        // Arrange
        var script = @"
ALTER PROCEDURE dbo.UpdateUser
AS
BEGIN
    UPDATE Users SET LastModified = GETDATE()
END";

        // Act
        var result = ValidateStoredProcedureScript(script);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ScriptType.Should().Be("ALTER PROCEDURE");
        result.ProcedureName.Should().Be("dbo.UpdateUser");
    }

    [Fact]
    public void ValidateStoredProcedureScript_WithoutCreateOrAlter_ReturnsInvalid()
    {
        // Arrange
        var script = "SELECT * FROM Users";

        // Act
        var result = ValidateStoredProcedureScript(script);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("must start with CREATE PROCEDURE or ALTER PROCEDURE");
    }

    [Fact]
    public void ValidateStoredProcedureScript_WithDangerousDDL_ReturnsInvalid()
    {
        // Arrange
        var script = @"
CREATE PROCEDURE dbo.DangerousProc
    @db VARCHAR(50) = 'DROP DATABASE master'
AS
BEGIN
    SELECT * FROM Users
END";

        // Act
        var result = ValidateStoredProcedureScript(script);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("forbidden DDL operation");
    }

    [Fact]
    public void ValidateStoredProcedureScript_WithMultipleProcedures_ReturnsInvalid()
    {
        // Arrange
        var script = @"
CREATE PROCEDURE dbo.Proc1 AS BEGIN SELECT 1 END
CREATE PROCEDURE dbo.Proc2 AS BEGIN SELECT 2 END";

        // Act
        var result = ValidateStoredProcedureScript(script);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Only one procedure per script");
    }

    [Fact]
    public void ValidateStoredProcedureScript_WithDeleteInBody_IsValid()
    {
        // Arrange
        var script = @"
CREATE PROCEDURE dbo.CleanOldData
AS
BEGIN
    DELETE FROM Orders WHERE OrderDate < '2020-01-01'
END";

        // Act
        var result = ValidateStoredProcedureScript(script);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ProcedureName.Should().Be("dbo.CleanOldData");
    }

    [Fact]
    public void ValidateStoredProcedureScript_WithComments_IgnoresComments()
    {
        // Arrange
        var script = @"
-- This is a comment
CREATE PROCEDURE dbo.GetUsers
/* Another comment */
AS
BEGIN
    SELECT * FROM Users
END";

        // Act
        var result = ValidateStoredProcedureScript(script);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ProcedureName.Should().Be("dbo.GetUsers");
    }

    // Helper method to simulate validation logic
    private ScriptValidation ValidateStoredProcedureScript(string script)
    {
        var result = new ScriptValidation();

        try
        {
            // Remove comments
            var cleanedScript = System.Text.RegularExpressions.Regex.Replace(script, @"--.*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            cleanedScript = System.Text.RegularExpressions.Regex.Replace(cleanedScript, @"/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            cleanedScript = cleanedScript.Trim();

            // Check for CREATE/ALTER
            var createMatch = System.Text.RegularExpressions.Regex.Match(cleanedScript,
                @"^\s*CREATE\s+(?:PROCEDURE|PROC)\s+(?:\[?(\w+)\]?\.)?\[?(\w+)\]?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var alterMatch = System.Text.RegularExpressions.Regex.Match(cleanedScript,
                @"^\s*ALTER\s+(?:PROCEDURE|PROC)\s+(?:\[?(\w+)\]?\.)?\[?(\w+)\]?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (createMatch.Success)
            {
                result.IsValid = true;
                result.ScriptType = "CREATE PROCEDURE";
                result.ProcedureName = createMatch.Groups[2].Value;
                if (!string.IsNullOrEmpty(createMatch.Groups[1].Value))
                {
                    result.ProcedureName = $"{createMatch.Groups[1].Value}.{result.ProcedureName}";
                }
            }
            else if (alterMatch.Success)
            {
                result.IsValid = true;
                result.ScriptType = "ALTER PROCEDURE";
                result.ProcedureName = alterMatch.Groups[2].Value;
                if (!string.IsNullOrEmpty(alterMatch.Groups[1].Value))
                {
                    result.ProcedureName = $"{alterMatch.Groups[1].Value}.{result.ProcedureName}";
                }
            }
            else
            {
                result.ErrorMessage = "Script must start with CREATE PROCEDURE or ALTER PROCEDURE statement.";
                return result;
            }

            // Check for dangerous DDL in header
            var headerMatch = System.Text.RegularExpressions.Regex.Match(cleanedScript,
                @"(?:CREATE|ALTER)\s+(?:PROCEDURE|PROC)\s+.*?\s+AS\s+",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

            if (headerMatch.Success)
            {
                var headerPart = cleanedScript.Substring(0, headerMatch.Index + headerMatch.Length);
                var dangerousDDL = new[] { "DROP DATABASE", "DROP TABLE", "TRUNCATE DATABASE" };
                
                foreach (var keyword in dangerousDDL)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(headerPart, $@"\b{keyword}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Script contains forbidden DDL operation: {keyword}. Only stored procedure definitions are allowed.";
                        return result;
                    }
                }
            }

            // Check for multiple procedures
            var procedureCount = System.Text.RegularExpressions.Regex.Matches(cleanedScript,
                @"\b(?:CREATE|ALTER)\s+(?:PROCEDURE|PROC)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;

            if (procedureCount > 1)
            {
                result.ErrorMessage = "Script contains multiple CREATE/ALTER PROCEDURE statements. Only one procedure per script is allowed.";
                result.IsValid = false;
                return result;
            }

            result.IsValid = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Validation error: {ex.Message}";
            result.IsValid = false;
        }

        return result;
    }

    private class ScriptValidation
    {
        public bool IsValid { get; set; }
        public string ScriptType { get; set; } = string.Empty;
        public string ProcedureName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}

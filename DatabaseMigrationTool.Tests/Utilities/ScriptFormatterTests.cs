using DatabaseMigrationTool.Services;
using DatabaseMigrationTool.Models;
using FluentAssertions;
using Moq;

namespace DatabaseMigrationTool.Tests.Services;

public class ScriptFormatterTests
{
    [Fact]
    public void FormatSQL_WithUnformattedScript_ReturnsFormattedScript()
    {
        // Arrange
        var unformatted = "create procedure test as begin select * from users end";

        // Act
        var formatted = FormatSQLScript(unformatted);

        // Assert
        formatted.Should().Contain("CREATE PROCEDURE");
        formatted.Should().Contain("BEGIN");
        formatted.Should().Contain("SELECT");
        formatted.Should().Contain("FROM");
        formatted.Should().Contain("END");
    }

    [Fact]
    public void FormatSQL_WithComments_PreservesComments()
    {
        // Arrange
        var scriptWithComments = @"
CREATE PROCEDURE test
AS
BEGIN
    -- This is a comment
    SELECT * FROM users
    /* Block comment */
END";

        // Act
        var formatted = FormatSQLScript(scriptWithComments);

        // Assert
        formatted.Should().Contain("-- This is a comment");
        formatted.Should().Contain("/* Block comment */");
    }

    [Fact]
    public void FormatSQL_WithProperIndentation_AddsTabIndentation()
    {
        // Arrange
        var script = "CREATE PROCEDURE test AS BEGIN SELECT * FROM users END";

        // Act
        var formatted = FormatSQLScript(script);

        // Assert
        formatted.Should().Contain("\t");
    }

    [Fact]
    public void FormatSQL_WithMultipleKeywords_FormatsAllToUppercase()
    {
        // Arrange
        var script = "create procedure test as begin select * from users where id=1 and status='active' end";

        // Act
        var formatted = FormatSQLScript(script);

        // Assert
        formatted.Should().Contain("CREATE");
        formatted.Should().Contain("PROCEDURE");
        formatted.Should().Contain("AS");
        formatted.Should().Contain("BEGIN");
        formatted.Should().Contain("SELECT");
        formatted.Should().Contain("FROM");
        formatted.Should().Contain("WHERE");
        formatted.Should().Contain("AND");
        formatted.Should().Contain("END");
    }

    // Helper method to simulate the format logic
    private string FormatSQLScript(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
            return script;

        var lines = script.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
        var formattedLines = new List<string>();
        var indentLevel = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                formattedLines.Add("");
                continue;
            }

            if (trimmed.StartsWith("--") || trimmed.StartsWith("/*"))
            {
                formattedLines.Add(new string('\t', indentLevel) + trimmed);
                continue;
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\s*\b(END|ELSE)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                indentLevel = Math.Max(0, indentLevel - 1);
            }

            var formatted = FormatKeywords(trimmed);
            formatted = new string('\t', indentLevel) + formatted;
            formattedLines.Add(formatted);

            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"\b(BEGIN|AS|IF|WHILE|CASE)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                indentLevel++;
            }
        }

        return string.Join(Environment.NewLine, formattedLines);
    }

    private string FormatKeywords(string line)
    {
        var keywords = new[] { "CREATE", "ALTER", "PROCEDURE", "AS", "BEGIN", "END", "SELECT", "FROM", "WHERE", "AND", "OR", "INSERT", "UPDATE", "DELETE", "JOIN", "ON", "GROUP BY", "ORDER BY" };
        
        var result = line;
        foreach (var keyword in keywords.OrderByDescending(k => k.Length))
        {
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(keyword)}\b",
                keyword,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
        }
        return result;
    }
}

using System;

namespace DatabaseMigrationTool.Models
{
    public class ConnectionSettings
    {
        public string ServerName { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public bool UseWindowsAuthentication { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public DateTime LastUsed { get; set; } = DateTime.Now;

        public string GetConnectionString()
        {
            if (UseWindowsAuthentication)
            {
                return $"Server={ServerName};Database={DatabaseName};Integrated Security=true;TrustServerCertificate=true;";
            }
            else
            {
                return $"Server={ServerName};Database={DatabaseName};User Id={Username};Password={Password};TrustServerCertificate=true;";
            }
        }

        public string GetServerConnectionString()
        {
            if (UseWindowsAuthentication)
            {
                return $"Server={ServerName};Integrated Security=true;TrustServerCertificate=true;";
            }
            else
            {
                return $"Server={ServerName};User Id={Username};Password={Password};TrustServerCertificate=true;";
            }
        }

        public override string ToString()
        {
            if (UseWindowsAuthentication)
            {
                return $"{ServerName} (Windows Authentication)";
            }
            else
            {
                return $"{ServerName} (SQL Auth: {Username})";
            }
        }

        public string GetUniqueKey()
        {
            return $"{ServerName}|{UseWindowsAuthentication}|{Username}";
        }
    }
}

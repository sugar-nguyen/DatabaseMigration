using DatabaseMigrationTool.Models;
using Newtonsoft.Json;
using System.IO;

namespace DatabaseMigrationTool.Services
{
    public class ConnectionSettingsService
    {
        private readonly string _settingsFilePath;
        private List<ConnectionSettings> _connections;

        public ConnectionSettingsService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "DatabaseMigrationTool");

            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            _settingsFilePath = Path.Combine(appFolder, "connections.json");
            _connections = new List<ConnectionSettings>();
            LoadConnections();
        }

        public List<ConnectionSettings> GetConnections()
        {
            return _connections.OrderByDescending(c => c.LastUsed).ToList();
        }

        public void SaveConnection(ConnectionSettings connection)
        {
            // Remove existing connection with same unique key (server + auth type + username)
            var uniqueKey = connection.GetUniqueKey();
            _connections.RemoveAll(c => c.GetUniqueKey().Equals(uniqueKey, StringComparison.OrdinalIgnoreCase));

            // Add the new/updated connection
            connection.LastUsed = DateTime.Now;
            _connections.Add(connection);

            SaveConnections();
        }

        public void SaveServerConnection(ConnectionSettings connection)
        {
            // Create a server-level connection (without database name)
            var serverConnection = new ConnectionSettings
            {
                ServerName = connection.ServerName,
                DatabaseName = "", // Server-level connection
                UseWindowsAuthentication = connection.UseWindowsAuthentication,
                Username = connection.Username,
                Password = connection.Password,
                DisplayName = connection.ToString(),
                LastUsed = DateTime.Now
            };

            SaveConnection(serverConnection);
        }

        public void RemoveConnection(ConnectionSettings connection)
        {
            var uniqueKey = connection.GetUniqueKey();
            _connections.RemoveAll(c => c.GetUniqueKey().Equals(uniqueKey, StringComparison.OrdinalIgnoreCase));
            SaveConnections();
        }

        public void UpdateLastUsed(ConnectionSettings connection)
        {
            var uniqueKey = connection.GetUniqueKey();
            var existing = _connections.FirstOrDefault(c => c.GetUniqueKey().Equals(uniqueKey, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.LastUsed = DateTime.Now;
                SaveConnections();
            }
        }

        public List<ConnectionSettings> GetServerConnections()
        {
            return _connections.Where(c => string.IsNullOrEmpty(c.DatabaseName))
                              .OrderByDescending(c => c.LastUsed)
                              .ToList();
        }

        private void LoadConnections()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    _connections = JsonConvert.DeserializeObject<List<ConnectionSettings>>(json) ?? new List<ConnectionSettings>();
                }
            }
            catch
            {
                _connections = new List<ConnectionSettings>();
            }
        }

        private void SaveConnections()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_connections, Formatting.Indented);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch
            {
                // Handle save error silently or log it
            }
        }
    }
}

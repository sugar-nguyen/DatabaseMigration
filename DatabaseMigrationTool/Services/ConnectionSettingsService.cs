using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DatabaseMigrationTool.Models;

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
            // Remove existing connection with same server and database
            _connections.RemoveAll(c => c.ServerName.Equals(connection.ServerName, StringComparison.OrdinalIgnoreCase) &&
                                      c.DatabaseName.Equals(connection.DatabaseName, StringComparison.OrdinalIgnoreCase));

            // Add the new/updated connection
            connection.LastUsed = DateTime.Now;
            _connections.Add(connection);

            SaveConnections();
        }

        public void RemoveConnection(ConnectionSettings connection)
        {
            _connections.RemoveAll(c => c.ServerName.Equals(connection.ServerName, StringComparison.OrdinalIgnoreCase) &&
                                      c.DatabaseName.Equals(connection.DatabaseName, StringComparison.OrdinalIgnoreCase));
            SaveConnections();
        }

        public void UpdateLastUsed(ConnectionSettings connection)
        {
            var existing = _connections.FirstOrDefault(c => c.ServerName.Equals(connection.ServerName, StringComparison.OrdinalIgnoreCase) &&
                                                          c.DatabaseName.Equals(connection.DatabaseName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.LastUsed = DateTime.Now;
                SaveConnections();
            }
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

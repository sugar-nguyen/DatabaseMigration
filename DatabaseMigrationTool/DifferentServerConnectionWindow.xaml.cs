using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DatabaseMigrationTool.Models;
using DatabaseMigrationTool.Services;

namespace DatabaseMigrationTool
{
    public partial class DifferentServerConnectionWindow : Window
    {
        private readonly DatabaseService _databaseService;
        private ObservableCollection<TargetDatabase> _targetDatabases;
        private ConnectionSettings? _connectionSettings;

        public ConnectionSettings? ConnectionSettings => _connectionSettings;
        public ObservableCollection<TargetDatabase> TargetDatabases => _targetDatabases;

        public DifferentServerConnectionWindow()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            _targetDatabases = new ObservableCollection<TargetDatabase>();
            
            lstTargetDatabases.ItemsSource = _targetDatabases;
            
            // Load recent servers from settings if available
            LoadRecentServers();
        }

        private void LoadRecentServers()
        {
            // Add some common server names - you can expand this with actual recent connections
            var commonServers = new List<string>
            {
                "localhost",
                ".\\SQLEXPRESS",
                "(local)",
                "127.0.0.1"
            };

            foreach (var server in commonServers)
            {
                cmbTargetServer.Items.Add(server);
            }
        }

        private void TargetWindowsAuth_Changed(object sender, RoutedEventArgs e)
        {
            if (targetSqlAuthPanel != null)
            {
                targetSqlAuthPanel.Visibility = chkTargetWindowsAuth.IsChecked == true 
                    ? Visibility.Collapsed 
                    : Visibility.Visible;
            }
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnTestConnection.IsEnabled = false;
                btnTestConnection.Content = "Testing...";

                var connectionString = BuildConnectionString();
                var connectionSettings = new ConnectionSettings
                {
                    ServerName = cmbTargetServer.Text?.Trim() ?? "",
                    DatabaseName = "master",
                    UseWindowsAuthentication = chkTargetWindowsAuth.IsChecked ?? true,
                    Username = txtTargetUsername.Text?.Trim() ?? "",
                    Password = txtTargetPassword.Password ?? ""
                };
                
                var isConnected = await _databaseService.TestConnectionAsync(connectionSettings);

                if (isConnected)
                {
                    MessageBox.Show("Connection successful!", "Test Connection", 
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    btnLoadDatabases.IsEnabled = true;
                }
                else
                {
                    MessageBox.Show("Connection failed. Please check your connection details.", 
                                   "Test Connection", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection error: {ex.Message}", "Test Connection", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnTestConnection.IsEnabled = true;
                btnTestConnection.Content = "Test Connection";
            }
        }

        private async void LoadDatabases_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnLoadDatabases.IsEnabled = false;
                btnLoadDatabases.Content = "🔄 Loading...";

                var connectionSettings = new ConnectionSettings
                {
                    ServerName = cmbTargetServer.Text?.Trim() ?? "",
                    DatabaseName = "master", // Use master to get list of databases
                    UseWindowsAuthentication = chkTargetWindowsAuth.IsChecked ?? true,
                    Username = txtTargetUsername.Text?.Trim() ?? "",
                    Password = txtTargetPassword.Password ?? "",
                    DisplayName = cmbTargetServer.Text?.Trim() ?? ""
                };

                var databases = await _databaseService.GetDatabasesAsync(connectionSettings);
                
                _targetDatabases.Clear();
                foreach (var db in databases.Where(d => d != "master" && d != "tempdb" && d != "model" && d != "msdb"))
                {
                    _targetDatabases.Add(new TargetDatabase { Name = db });
                }

                UpdateSelectedCount();
                btnConnect.IsEnabled = _targetDatabases.Any();

                if (_targetDatabases.Any())
                {
                    MessageBox.Show($"✅ Loaded {_targetDatabases.Count} databases successfully!", 
                                   "Load Databases", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("⚠️ No user databases found on the server.", 
                                   "Load Databases", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error loading databases: {ex.Message}", "Load Databases", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnLoadDatabases.IsEnabled = true;
                btnLoadDatabases.Content = "📋 Load Databases";
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            var selectedDatabases = _targetDatabases.Where(d => d.IsSelected).ToList();
            
            if (!selectedDatabases.Any())
            {
                MessageBox.Show("Please select at least one database.", "No Selection", 
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _connectionSettings = new ConnectionSettings
            {
                ServerName = cmbTargetServer.Text?.Trim() ?? "",
                DatabaseName = "", // Will be set per database during migration
                UseWindowsAuthentication = chkTargetWindowsAuth.IsChecked ?? true,
                Username = txtTargetUsername.Text?.Trim() ?? "",
                Password = txtTargetPassword.Password ?? "",
                DisplayName = cmbTargetServer.Text?.Trim() ?? ""
            };

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private string BuildConnectionString()
        {
            var serverName = cmbTargetServer.Text?.Trim() ?? "";
            var useWindowsAuth = chkTargetWindowsAuth.IsChecked ?? true;

            if (string.IsNullOrEmpty(serverName))
            {
                throw new ArgumentException("Server name is required.");
            }

            if (useWindowsAuth)
            {
                return $"Server={serverName};Database=master;Trusted_Connection=true;";
            }
            else
            {
                var username = txtTargetUsername.Text?.Trim() ?? "";
                var password = txtTargetPassword.Password ?? "";

                if (string.IsNullOrEmpty(username))
                {
                    throw new ArgumentException("Username is required for SQL Server authentication.");
                }

                return $"Server={serverName};Database=master;User Id={username};Password={password};";
            }
        }

        private void UpdateSelectedCount()
        {
            var selectedCount = _targetDatabases.Count(d => d.IsSelected);
            txtSelectedDatabasesCount.Text = $"{selectedCount} database(s) selected";
        }

        // Handle selection changes to update count
        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            
            // Subscribe to property changes on all target databases
            foreach (var db in _targetDatabases)
            {
                db.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(TargetDatabase.IsSelected))
                    {
                        UpdateSelectedCount();
                        btnConnect.IsEnabled = _targetDatabases.Any(d => d.IsSelected);
                    }
                };
            }
        }

        private void FullscreenDatabases_Click(object sender, RoutedEventArgs e)
        {
            var selectorWindow = new DatabaseSelectorWindow(_targetDatabases);
            selectorWindow.Owner = this;
            
            if (selectorWindow.ShowDialog() == true)
            {
                // Update the original collection with the selections
                foreach (var db in _targetDatabases)
                {
                    db.IsSelected = false;
                }

                foreach (var selectedDb in selectorWindow.SelectedDatabases)
                {
                    var originalDb = _targetDatabases.FirstOrDefault(d => d.Name == selectedDb.Name);
                    if (originalDb != null)
                    {
                        originalDb.IsSelected = true;
                    }
                }
                
                // Update the count display
                UpdateSelectedDatabasesCount();
            }
        }

        private void UpdateSelectedDatabasesCount()
        {
            if (_targetDatabases != null)
            {
                var selectedCount = _targetDatabases.Count(db => db.IsSelected);
                txtSelectedDatabasesCount.Text = $"{selectedCount} database(s) selected";
            }
        }
    }
}

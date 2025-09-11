using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using DatabaseMigrationTool.Models;
using DatabaseMigrationTool.Services;

namespace DatabaseMigrationTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly DatabaseService _databaseService;
        private readonly ConnectionSettingsService _connectionService;
        private List<StoredProcedure> _storedProcedures;
        private List<Table> _tables;
        private ObservableCollection<TargetDatabase> _targetDatabases;
        private ObservableCollection<TargetDatabase> _targetServerDatabases; // For different server
        private ICollectionView? _storedProceduresView;
        private ICollectionView? _tablesView;

        // Track current target server mode
        private bool _isDifferentServerMode = false;
        private ConnectionSettings? _differentServerSettings;
        private ObservableCollection<TargetDatabase>? _differentServerDatabases;

        public MainWindow()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            _connectionService = new ConnectionSettingsService();
            _storedProcedures = new List<StoredProcedure>();
            _tables = new List<Table>();
            _targetDatabases = new ObservableCollection<TargetDatabase>();
            _targetServerDatabases = new ObservableCollection<TargetDatabase>();

            LoadConnectionHistory();
            UpdateTargetConfigurationDisplay();
            
            // Initialize Migration Log after the window is loaded
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeMigrationLog();
        }

        #region Connection Management

        private void LoadConnectionHistory()
        {
            try
            {
                var connections = _connectionService.GetServerConnections();
                
                cmbServer.Items.Clear();
                
                // Add server names as strings first
                var serverNames = connections.Select(c => c.ServerName).Distinct().ToList();
                foreach (var serverName in serverNames)
                {
                    cmbServer.Items.Add(serverName);
                }
                
                // Store connections for later use
                cmbServer.Tag = connections;
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application
                SetStatus($"Warning: Could not load connection history: {ex.Message}");
            }
        }

        private bool ValidateServerConnection()
        {
            if (string.IsNullOrWhiteSpace(cmbServer.Text))
            {
                MessageBox.Show("Please enter a server name first.", "Missing Information", 
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!(chkWindowsAuth.IsChecked ?? true) && string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                MessageBox.Show("Please enter username for SQL Server authentication.", "Missing Information", 
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private ConnectionSettings GetCurrentConnectionSettings()
        {
            return new ConnectionSettings
            {
                ServerName = cmbServer.Text?.Trim() ?? "",
                DatabaseName = cmbSourceDatabase.Text?.Trim() ?? "",
                UseWindowsAuthentication = chkWindowsAuth.IsChecked ?? true,
                Username = txtUsername.Text?.Trim() ?? "",
                Password = txtPassword.Password ?? "",
                DisplayName = $"{cmbServer.Text}\\{cmbSourceDatabase.Text}"
            };
        }

        #endregion

        #region Source Database Operations

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cmbServer.Text))
                {
                    MessageBox.Show("Please enter a server name.", "Missing Information", 
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SetStatus("Testing connection...");
                LogMessage($"=== CONNECTION TEST ===");
                LogMessage($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                LogMessage($"Server: {cmbServer.Text}");
                LogMessage($"Authentication: {(chkWindowsAuth.IsChecked == true ? "Windows Authentication" : "SQL Server Authentication")}");
                
                var settings = GetCurrentConnectionSettings();
                var success = await _databaseService.TestConnectionAsync(settings);

                if (success)
                {
                    SetStatus("Connection test successful");
                    LogMessage("✓ Connection test successful");
                    LogMessage("🔗 Server is accessible and credentials are valid");
                    
                    // Save connection to history
                    _connectionService.SaveServerConnection(settings);
                    
                    // Preserve current server name when refreshing history
                    var currentServerName = cmbServer.Text;
                    LoadConnectionHistory(); 
                    cmbServer.Text = currentServerName; // Restore the server name
                    
                    MessageBox.Show("Connection successful!", "Connection Test", 
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    SetStatus("Connection test failed");
                    LogMessage("✗ Connection test failed");
                    LogMessage("❌ Could not connect - check server name and credentials");
                    MessageBox.Show("Connection failed. Check the Migration Log for details.", 
                                   "Connection Test", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                LogMessage("");
            }
            catch (Exception ex)
            {
                SetStatus("Connection error");
                LogMessage($"❌ Connection error: {ex.Message}");
                LogMessage($"Error Type: {ex.GetType().Name}");
                MessageBox.Show("Connection error! Check the Migration Log for details.", 
                               "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshDatabases_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cmbServer.Text))
                {
                    MessageBox.Show("Please enter a server name first.", "Missing Information", 
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SetStatus("Loading databases...");
                LogMessage($"=== LOADING DATABASES ===");
                LogMessage($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                LogMessage($"Server: {cmbServer.Text}");
                
                var settings = GetCurrentConnectionSettings();
                var databases = await _databaseService.GetDatabasesAsync(settings);

                cmbSourceDatabase.Items.Clear();
                foreach (var db in databases)
                {
                    cmbSourceDatabase.Items.Add(db);
                }

                // Also update target databases for same server
                _targetDatabases.Clear();
                foreach (var db in databases)
                {
                    _targetDatabases.Add(new TargetDatabase { Name = db, IsSelected = false });
                }

                // Update the target database count display
                UpdateTargetDatabaseCount();

                // Save connection to history when databases loaded successfully
                _connectionService.SaveServerConnection(settings);
                
                // Preserve current server name when refreshing history
                var currentServerName = cmbServer.Text;
                LoadConnectionHistory(); 
                cmbServer.Text = currentServerName; // Restore the server name

                SetStatus($"Loaded {databases.Count} databases");
                LogMessage($"✓ Successfully loaded {databases.Count} databases");
                LogMessage($"Available databases: {string.Join(", ", databases.Take(10))}{(databases.Count > 10 ? $" and {databases.Count - 10} more..." : "")}");
                LogMessage("");
            }
            catch (Exception ex)
            {
                SetStatus("Error loading databases");
                LogMessage($"✗ Failed to load databases: {ex.Message}");
                LogMessage($"Error Type: {ex.GetType().Name}");
                LogMessage("");
                MessageBox.Show("Error loading databases! Check the Migration Log for details.", 
                               "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadStoredProcedures_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate server connection first
                if (!ValidateServerConnection())
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(cmbSourceDatabase.Text))
                {
                    MessageBox.Show("Please select a source database first.", "Missing Information", 
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SetStatus("Loading database objects...");
                LogMessage($"=== LOADING DATABASE OBJECTS ===");
                LogMessage($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                LogMessage($"Source Database: {cmbSourceDatabase.Text}");
                LogMessage($"Server: {cmbServer.Text}");
                LogMessage($"Authentication: {(chkWindowsAuth.IsChecked == true ? "Windows Authentication" : $"SQL Server Authentication (User: {txtUsername.Text})")}");
                
                var settings = GetCurrentConnectionSettings();
                LogMessage($"Connection String Preview: Server={settings.ServerName};Database={settings.DatabaseName};[Auth Info Hidden]");
                
                // Test connection before loading objects
                LogMessage("Testing connection to source database...");
                var connectionTest = await _databaseService.TestConnectionAsync(settings);
                if (!connectionTest)
                {
                    LogMessage("✗ Connection test failed - cannot load database objects");
                    MessageBox.Show("Connection to source database failed. Please check your connection settings and try 'Test Connection' first.", 
                                   "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                LogMessage("✓ Connection test successful");
                
                // Load stored procedures
                LogMessage("Loading stored procedures...");
                var procedures = await _databaseService.GetStoredProceduresAsync(settings);

                _storedProcedures = procedures.Select(sp => new StoredProcedure
                {
                    Schema = sp.Schema,
                    Name = sp.Name,
                    Definition = sp.Definition,
                    IsSelected = false
                }).ToList();

                _storedProceduresView = CollectionViewSource.GetDefaultView(_storedProcedures);
                lstStoredProcedures.ItemsSource = _storedProceduresView;

                UpdateProcedureCount();
                
                // Update the Database Summary total count
                lblTotalProcedures.Text = _storedProcedures.Count.ToString();
                LogMessage($"✓ Loaded {_storedProcedures.Count} stored procedures");
                
                // Also load tables
                LogMessage("Loading tables...");
                await LoadTablesInternal();
                
                SetStatus($"Loaded {_storedProcedures.Count} stored procedures and {_tables?.Count ?? 0} tables");
                LogMessage($"✓ Loaded {_tables?.Count ?? 0} tables");
                LogMessage($"📊 Total objects available for migration: {_storedProcedures.Count + (_tables?.Count ?? 0)}");
                LogMessage("");
            }
            catch (Exception ex)
            {
                SetStatus("Error loading stored procedures");
                LogMessage($"✗ Failed to load database objects: {ex.Message}");
                LogMessage($"Error Type: {ex.GetType().Name}");
                LogMessage("");
                MessageBox.Show("Error loading database objects! Check the Migration Log for details.", 
                               "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Target Configuration

        private void DifferentServer_Click(object sender, RoutedEventArgs e)
        {
            var differentServerWindow = new DifferentServerConnectionWindow();
            differentServerWindow.Owner = this;
            
            if (differentServerWindow.ShowDialog() == true)
            {
                _isDifferentServerMode = true;
                _differentServerSettings = differentServerWindow.ConnectionSettings;
                _differentServerDatabases = new ObservableCollection<TargetDatabase>(
                    differentServerWindow.TargetDatabases.Where(db => db.IsSelected).Select(db => new TargetDatabase 
                    { 
                        Name = db.Name, 
                        IsSelected = true 
                    })
                );
                UpdateTargetConfigurationDisplay();
            }
        }

        private void SameServer_Click(object sender, RoutedEventArgs e)
        {
            _isDifferentServerMode = false;
            _differentServerSettings = null;
            _differentServerDatabases = null;
            UpdateTargetConfigurationDisplay();
        }

        private void UpdateTargetConfigurationDisplay()
        {
            if (_isDifferentServerMode && _differentServerSettings != null)
            {
                lblTargetMode.Text = $"🔗 Different Server: {_differentServerSettings.ServerName}";
                btnSameServer.Visibility = Visibility.Visible;
                btnDifferentServer.Visibility = Visibility.Collapsed;
                
                // Update target databases list for different server
                lstTargetDatabases.ItemsSource = _differentServerDatabases;
            }
            else
            {
                lblTargetMode.Text = "📍 Same Server";
                btnSameServer.Visibility = Visibility.Collapsed;
                btnDifferentServer.Visibility = Visibility.Visible;
                
                // Update target databases list for same server
                lstTargetDatabases.ItemsSource = _targetDatabases;
            }
            
            // Force refresh the ListBox to update checkboxes
            if (lstTargetDatabases.ItemsSource != null)
            {
                var view = CollectionViewSource.GetDefaultView(lstTargetDatabases.ItemsSource);
                view?.Refresh();
            }
            
            // Update the database count
            UpdateTargetDatabaseCount();
        }

        #endregion

        #region Migration Operations (Placeholder)

        private async void StartMigration_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if we have target databases selected - handle both same server and different server modes
                List<TargetDatabase>? selectedTargetDbs = null;
                
                if (_isDifferentServerMode && _differentServerDatabases != null)
                {
                    // Different server mode - check _differentServerDatabases
                    selectedTargetDbs = _differentServerDatabases.Where(d => d.IsSelected).ToList();
                }
                else
                {
                    // Same server mode - check _targetDatabases
                    selectedTargetDbs = _targetDatabases?.Where(d => d.IsSelected).ToList();
                }
                
                if (selectedTargetDbs == null || !selectedTargetDbs.Any())
                {
                    MessageBox.Show("Please select target databases before starting migration.", "No Target Databases", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check if we have stored procedures or tables selected
                var selectedProcedures = _storedProcedures?.Where(sp => sp.IsSelected).ToList();
                var selectedTables = _tables?.Where(t => t.IsSelected).ToList();
                
                if ((selectedProcedures == null || !selectedProcedures.Any()) && 
                    (selectedTables == null || !selectedTables.Any()))
                {
                    MessageBox.Show("Please select stored procedures or tables to migrate.", "Nothing Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Start migration to {selectedTargetDbs.Count} database(s)?\n\n" +
                    $"Stored Procedures: {selectedProcedures?.Count ?? 0}\n" +
                    $"Tables: {selectedTables?.Count ?? 0}",
                    "Confirm Migration", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await PerformMigrationAsync(selectedTargetDbs, selectedProcedures, selectedTables);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting migration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task PerformMigrationAsync(List<TargetDatabase> targetDatabases, List<StoredProcedure>? selectedProcedures, List<Table>? selectedTables)
        {
            var sourceSettings = GetCurrentConnectionSettings();
            var totalOperations = targetDatabases.Count * ((selectedProcedures?.Count ?? 0) + (selectedTables?.Count ?? 0));
            var completedOperations = 0;
            var errorCount = 0;

            try
            {
                // Clear previous logs
                txtMigrationLog.Clear();
                
                SetStatus("Starting migration process...");
                LogMessage($"=== MIGRATION STARTED ===");
                LogMessage($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                LogMessage($"Source: {sourceSettings.ServerName}\\{sourceSettings.DatabaseName}");
                LogMessage($"Target Databases: {targetDatabases.Count}");
                LogMessage($"Stored Procedures: {selectedProcedures?.Count ?? 0}");
                LogMessage($"Tables: {selectedTables?.Count ?? 0}");
                LogMessage($"Total Operations: {totalOperations}");
                LogMessage("");

                foreach (var targetDb in targetDatabases)
                {
                    var targetSettings = GetTargetConnectionSettings(targetDb.Name);
                    
                    SetStatus($"Migrating to database: {targetDb.Name}");
                    LogMessage($"--- Processing Target Database: {targetDb.Name} ---");

                    // Migrate stored procedures
                    if (selectedProcedures != null && selectedProcedures.Any())
                    {
                        LogMessage($"Migrating {selectedProcedures.Count} stored procedures...");
                        foreach (var sp in selectedProcedures)
                        {
                            try
                            {
                                SetStatus($"Migrating procedure {sp.Name} to {targetDb.Name}...");
                                LogMessage($"  • Migrating procedure: {sp.Schema}.{sp.Name}");
                                
                                // Check if backup will be created based on checkbox
                                bool createBackup = chkCreateBackup.IsChecked == true;
                                var procedureExists = await _databaseService.StoredProcedureExistsAsync(targetSettings, sp.Schema, sp.Name);
                                if (procedureExists && createBackup)
                                {
                                    LogMessage($"    → Creating backup: {sp.Name}_{DateTime.Now:ddMMyyyy}");
                                }
                                else if (procedureExists && !createBackup)
                                {
                                    LogMessage($"    → Backup skipped (Create backup option not selected)");
                                }
                                
                                await _databaseService.CreateOrAlterStoredProcedureAsync(targetSettings, sp, createBackup: createBackup);
                                completedOperations++;
                                
                                LogMessage($"    ✓ Success");
                                SetStatus($"Progress: {completedOperations}/{totalOperations} completed");
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                LogMessage($"    ✗ Failed: {ex.Message}");
                                LogMessage($"    ↳ Error Details: {ex.GetType().Name}");
                                // Log error but don't show popup - user can see details in log
                            }
                        }
                    }

                    // Migrate tables
                    if (selectedTables != null && selectedTables.Any())
                    {
                        LogMessage($"Migrating {selectedTables.Count} tables...");
                        foreach (var table in selectedTables)
                        {
                            try
                            {
                                SetStatus($"Migrating table {table.Name} to {targetDb.Name}...");
                                LogMessage($"  • Migrating table: {table.Schema}.{table.Name}");
                                
                                // Check if table exists
                                var tableExists = await _databaseService.TableExistsAsync(targetSettings, table.Schema, table.Name);
                                if (tableExists)
                                {
                                    LogMessage($"    → Table exists, will attempt ALTER operations");
                                }
                                else
                                {
                                    LogMessage($"    → Creating new table");
                                }
                                
                                await _databaseService.CreateOrAlterTableAsync(sourceSettings, targetSettings, table, replaceIfExists: false);
                                completedOperations++;
                                
                                LogMessage($"    ✓ Success");
                                SetStatus($"Progress: {completedOperations}/{totalOperations} completed");
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                LogMessage($"    ✗ Failed: {ex.Message}");
                                LogMessage($"    ↳ Error Details: {ex.GetType().Name}");
                                // Log error but don't show popup - user can see details in log
                            }
                        }
                    }
                    
                    LogMessage($"Completed target database: {targetDb.Name}");
                    LogMessage("");
                }

                SetStatus($"Migration completed! {completedOperations}/{totalOperations} operations successful");
                LogMessage("=== MIGRATION COMPLETED ===");
                LogMessage($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                LogMessage($"Success: {completedOperations}/{totalOperations} operations");
                LogMessage($"Errors: {errorCount}");
                LogMessage($"Success Rate: {(totalOperations > 0 ? (completedOperations * 100.0 / totalOperations):0):F1}%");
                LogMessage("");
                LogMessage("📊 MIGRATION SUMMARY:");
                LogMessage($"   Target Databases: {targetDatabases.Count}");
                LogMessage($"   Stored Procedures: {selectedProcedures?.Count ?? 0}");
                LogMessage($"   Tables: {selectedTables?.Count ?? 0}");
                LogMessage($"   Success Rate: {(totalOperations > 0 ? (completedOperations * 100.0 / totalOperations):0):F1}%");
                LogMessage("");
                if (errorCount == 0)
                {
                    LogMessage("🎉 All operations completed successfully!");
                }
                else
                {
                    LogMessage($"⚠️  {errorCount} operations failed - check log details above");
                }
                LogMessage("=====================================");
                
                // Show simple completion notification without detailed stats (user can see in log)
                MessageBox.Show($"Migration completed!\n\nCheck the Migration Log for detailed results.", 
                              "Migration Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetStatus("Migration failed");
                LogMessage($"=== MIGRATION FAILED ===");
                LogMessage($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                LogMessage($"Fatal Error: {ex.Message}");
                LogMessage($"Error Type: {ex.GetType().Name}");
                LogMessage($"Operations completed before failure: {completedOperations}/{totalOperations}");
                LogMessage($"Stack Trace: {ex.StackTrace}");
                LogMessage("=====================================");
                
                MessageBox.Show($"Migration failed! Check the Migration Log for details.", "Migration Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ConnectionSettings GetTargetConnectionSettings(string databaseName)
        {
            if (_isDifferentServerMode && _differentServerSettings != null)
            {
                // Use different server settings
                return new ConnectionSettings
                {
                    ServerName = _differentServerSettings.ServerName,
                    DatabaseName = databaseName,
                    UseWindowsAuthentication = _differentServerSettings.UseWindowsAuthentication,
                    Username = _differentServerSettings.Username,
                    Password = _differentServerSettings.Password,
                    DisplayName = $"{_differentServerSettings.ServerName}\\{databaseName}"
                };
            }
            else
            {
                // Use same server as source
                var sourceSettings = GetCurrentConnectionSettings();
                return new ConnectionSettings
                {
                    ServerName = sourceSettings.ServerName,
                    DatabaseName = databaseName,
                    UseWindowsAuthentication = sourceSettings.UseWindowsAuthentication,
                    Username = sourceSettings.Username,
                    Password = sourceSettings.Password,
                    DisplayName = $"{sourceSettings.ServerName}\\{databaseName}"
                };
            }
        }

        private void MigrateStoredProcedures_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Reimplement with new UI architecture
            MessageBox.Show("Migration functionality temporarily disabled during UI refactoring.", "Under Construction", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PreviewScripts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if we have anything selected to preview
                var selectedProcedures = _storedProcedures?.Where(sp => sp.IsSelected).ToList() ?? new List<StoredProcedure>();
                var selectedTables = _tables?.Where(t => t.IsSelected).ToList() ?? new List<Table>();
                
                if (!selectedProcedures.Any() && !selectedTables.Any())
                {
                    MessageBox.Show("Please select stored procedures or tables to preview scripts.", "Nothing Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SetStatus("Generating preview scripts...");
                LogMessage($"=== SCRIPT PREVIEW GENERATION ===");
                LogMessage($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                LogMessage($"Selected Procedures: {selectedProcedures.Count}");
                LogMessage($"Selected Tables: {selectedTables.Count}");
                
                // Open the script preview window
                var previewWindow = new ScriptPreviewWindow();
                
                // Generate placeholder scripts for preview
                var procedureScripts = selectedProcedures.Select(p => $"-- Script for {p.Name}\nCREATE OR ALTER PROCEDURE [{p.Name}]\nAS\nBEGIN\n    -- Procedure implementation\nEND").ToList();
                var tableScripts = selectedTables.Select(t => $"-- Script for {t.Name}\nCREATE TABLE [{t.Name}] (\n    -- Table structure\n);").ToList();
                
                LogMessage("Generated script preview for:");
                foreach (var proc in selectedProcedures)
                    LogMessage($"  • Procedure: {proc.Schema}.{proc.Name}");
                foreach (var table in selectedTables)
                    LogMessage($"  • Table: {table.Schema}.{table.Name}");
                
                previewWindow.SetScripts(selectedProcedures, selectedTables, procedureScripts, tableScripts);
                previewWindow.ShowDialog();
                
                SetStatus("Script preview completed");
                LogMessage("Script preview window opened successfully");
                LogMessage("");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating script preview: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Script preview failed");
                LogMessage($"Script preview failed: {ex.Message}");
            }
        }

        #endregion

        #region Tables

        private async Task LoadTablesInternal()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cmbSourceDatabase.Text))
                    return;

                var settings = GetCurrentConnectionSettings();
                var tables = await _databaseService.GetTablesAsync(settings);

                _tables = tables.Select(t => new Table
                {
                    Schema = t.Schema,
                    Name = t.Name,
                    Definition = "", // Will be loaded when needed
                    IsSelected = false
                }).ToList();

                _tablesView = CollectionViewSource.GetDefaultView(_tables);
                lstTables.ItemsSource = _tablesView;
                UpdateTableCount();
                
                // Update the Database Summary total count
                lblTotalTables.Text = _tables.Count.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading tables: {ex.Message}", "Database Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadTables()
        {
            await LoadTablesInternal();
        }

        #endregion

        #region Helper Methods

        private void UpdateProcedureCount()
        {
            if (_storedProcedures != null)
            {
                var selectedCount = _storedProcedures.Count(sp => sp.IsSelected);
                txtSelectedProceduresCount.Text = $"{selectedCount} procedure(s) selected";
            }
        }

        private void UpdateTableCount()
        {
            if (_tables != null)
            {
                var selectedCount = _tables.Count(t => t.IsSelected);
                txtSelectedTablesCount.Text = $"{selectedCount} table(s) selected";
            }
        }

        private void UpdateTargetDatabaseCount()
        {
            if (_isDifferentServerMode && _differentServerDatabases != null)
            {
                var selectedCount = _differentServerDatabases.Count(db => db.IsSelected);
                txtSelectedDatabasesCount.Text = $"{selectedCount} database(s) selected";
            }
            else if (_targetDatabases != null)
            {
                var selectedCount = _targetDatabases.Count(db => db.IsSelected);
                txtSelectedDatabasesCount.Text = $"{selectedCount} database(s) selected";
            }
        }

        private void SetStatus(string status)
        {
            lblStatus.Text = status;
        }

        private void LogMessage(string message)
        {
            txtMigrationLog.AppendText(message + Environment.NewLine);
            txtMigrationLog.ScrollToEnd();
        }

        private void InitializeMigrationLog()
        {
            LogMessage("=== DATABASE MIGRATION TOOL ===");
            LogMessage($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            LogMessage("Version: 1.0.0");
            LogMessage("");
            LogMessage("📋 Instructions:");
            LogMessage("1. Configure source connection and test connectivity");
            LogMessage("2. Load databases and select source database");
            LogMessage("3. Load stored procedures and tables");
            LogMessage("4. Configure target databases");
            LogMessage("5. Select objects to migrate");
            LogMessage("6. Use Migration Control buttons:");
            LogMessage("   • Generate Settings: Create migration configuration");
            LogMessage("   • Preview Scripts: View SQL scripts before migration");
            LogMessage("   • Start Migration: Execute the migration process");
            LogMessage("");
            LogMessage("💡 All operation details and backup information will appear here.");
            LogMessage("====================================");
            LogMessage("");
        }

        #endregion

        #region Event Handlers

        private void Server_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Auto-fill connection settings when server is selected from history
            if (cmbServer.SelectedItem != null && cmbServer.Tag is List<ConnectionSettings> connections)
            {
                var selectedServerName = cmbServer.SelectedItem.ToString();
                var matchingConnection = connections.FirstOrDefault(c => c.ServerName.Equals(selectedServerName, StringComparison.OrdinalIgnoreCase));
                
                if (matchingConnection != null)
                {
                    // Auto-fill authentication settings
                    chkWindowsAuth.IsChecked = matchingConnection.UseWindowsAuthentication;
                    
                    if (!matchingConnection.UseWindowsAuthentication)
                    {
                        // Fill username and password for SQL authentication
                        txtUsername.Text = matchingConnection.Username;
                        txtPassword.Password = matchingConnection.Password;
                    }
                    else
                    {
                        // Clear username and password for Windows authentication
                        txtUsername.Text = "";
                        txtPassword.Password = "";
                    }
                    
                    // Trigger the auth panel visibility change
                    WindowsAuth_Changed(chkWindowsAuth, new RoutedEventArgs());
                    
                    // Update last used
                    _connectionService.UpdateLastUsed(matchingConnection);
                    
                    // Refresh target configuration display to ensure UI is updated
                    UpdateTargetConfigurationDisplay();
                }
            }
        }

        private void SourceDatabase_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Auto-load objects when database is selected
        }

        private void TabDatabaseObjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Handle tab changes
        }

        private void SearchProcedures_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_storedProceduresView != null)
            {
                var searchText = txtSearchProcedures.Text?.ToLower() ?? string.Empty;
                
                if (string.IsNullOrEmpty(searchText))
                {
                    _storedProceduresView.Filter = null;
                }
                else
                {
                    _storedProceduresView.Filter = item =>
                    {
                        if (item is StoredProcedure sp)
                        {
                            return sp.Name.ToLower().Contains(searchText);
                        }
                        return false;
                    };
                }
                _storedProceduresView.Refresh();
            }
        }

        private void SearchTables_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_tablesView != null)
            {
                var searchText = txtSearchTables.Text?.ToLower() ?? string.Empty;
                
                if (string.IsNullOrEmpty(searchText))
                {
                    _tablesView.Filter = null;
                }
                else
                {
                    _tablesView.Filter = item =>
                    {
                        if (item is Table table)
                        {
                            return table.Name.ToLower().Contains(searchText);
                        }
                        return false;
                    };
                }
                _tablesView.Refresh();
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            // Clear all procedure selections
            if (_storedProcedures != null)
            {
                foreach (var sp in _storedProcedures)
                {
                    sp.IsSelected = false;
                }
                UpdateProcedureCount();
            }
        }

        private void ClearAllTables_Click(object sender, RoutedEventArgs e)
        {
            // Clear all table selections
            if (_tables != null)
            {
                foreach (var table in _tables)
                {
                    table.IsSelected = false;
                }
                UpdateTableCount();
            }
        }

        private void GenerateTargetSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if we have source connection
                if (string.IsNullOrEmpty(cmbServer.Text))
                {
                    MessageBox.Show("Please configure source connection first.", "No Source Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check if we have target databases selected - handle both same server and different server modes
                List<TargetDatabase>? selectedTargetDbs = null;
                
                if (_isDifferentServerMode && _differentServerDatabases != null)
                {
                    // Different server mode - check _differentServerDatabases
                    selectedTargetDbs = _differentServerDatabases.Where(d => d.IsSelected).ToList();
                }
                else
                {
                    // Same server mode - check _targetDatabases
                    selectedTargetDbs = _targetDatabases?.Where(d => d.IsSelected).ToList();
                }
                
                if (selectedTargetDbs == null || !selectedTargetDbs.Any())
                {
                    MessageBox.Show("Please select target databases first.", "No Target Databases", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check if we have stored procedures or tables selected
                var selectedProcedures = _storedProcedures?.Where(sp => sp.IsSelected).ToList();
                var selectedTables = _tables?.Where(t => t.IsSelected).ToList();
                
                if ((selectedProcedures == null || !selectedProcedures.Any()) && 
                    (selectedTables == null || !selectedTables.Any()))
                {
                    MessageBox.Show("Please select stored procedures or tables to migrate.", "Nothing Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SetStatus("Generating migration settings...");

                // Generate settings summary
                var settings = new StringBuilder();
                settings.AppendLine("=== DATABASE MIGRATION SETTINGS ===");
                settings.AppendLine($"Generated: {DateTime.Now}");
                settings.AppendLine();
                
                settings.AppendLine("SOURCE CONNECTION:");
                settings.AppendLine($"  Server: {cmbServer.Text}");
                settings.AppendLine($"  Database: {cmbSourceDatabase.Text}");
                settings.AppendLine($"  Authentication: {(chkWindowsAuth.IsChecked == true ? "Windows Authentication" : "SQL Server Authentication")}");
                settings.AppendLine();
                
                settings.AppendLine("TARGET DATABASES:");
                foreach (var db in selectedTargetDbs)
                {
                    settings.AppendLine($"  • {db.Name}");
                }
                settings.AppendLine();
                
                if (selectedProcedures != null && selectedProcedures.Any())
                {
                    settings.AppendLine($"STORED PROCEDURES ({selectedProcedures.Count}):");
                    foreach (var proc in selectedProcedures.Take(10)) // Show first 10
                    {
                        settings.AppendLine($"  • {proc.Name}");
                    }
                    if (selectedProcedures.Count > 10)
                        settings.AppendLine($"  ... and {selectedProcedures.Count - 10} more");
                    settings.AppendLine();
                }
                
                if (selectedTables != null && selectedTables.Any())
                {
                    settings.AppendLine($"TABLES ({selectedTables.Count}):");
                    foreach (var table in selectedTables.Take(10)) // Show first 10
                    {
                        settings.AppendLine($"  • {table.Name}");
                    }
                    if (selectedTables.Count > 10)
                        settings.AppendLine($"  ... and {selectedTables.Count - 10} more");
                }

                // Show settings in a message box
                var result = MessageBox.Show(
                    settings.ToString() + "\n\nSave settings to file?", 
                    "Migration Settings Generated", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    // TODO: Implement save to file functionality
                    MessageBox.Show("Settings saved! (File save implementation in progress)", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                SetStatus("Migration settings generated successfully");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Settings generation failed");
            }
        }

        private void WindowsAuth_Changed(object sender, RoutedEventArgs e)
        {
            if (sqlAuthPanel == null)
                return;

            var useWindowsAuth = chkWindowsAuth.IsChecked == true;
            sqlAuthPanel.Visibility = useWindowsAuth ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            if (_storedProcedures != null)
            {
                foreach (var sp in _storedProcedures)
                {
                    sp.IsSelected = true;
                }
                UpdateProcedureCount();
            }
        }

        private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_storedProcedures != null)
            {
                foreach (var sp in _storedProcedures)
                {
                    sp.IsSelected = false;
                }
                UpdateProcedureCount();
            }
        }

        private void SelectAllTables_Checked(object sender, RoutedEventArgs e)
        {
            if (_tables != null)
            {
                foreach (var table in _tables)
                {
                    table.IsSelected = true;
                }
                UpdateTableCount();
            }
        }

        private void SelectAllTables_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_tables != null)
            {
                foreach (var table in _tables)
                {
                    table.IsSelected = false;
                }
                UpdateTableCount();
            }
        }

        private void ProcedureCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateProcedureCount();
        }

        private void TableCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateTableCount();
        }

        private void TargetDatabaseCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateTargetDatabaseCount();
        }

        private void TargetDatabaseSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox searchBox)
            {
                var searchTerm = searchBox.Text?.ToLower() ?? string.Empty;
                
                // Get the current target databases collection
                ObservableCollection<TargetDatabase> databasesToFilter;
                
                if (_isDifferentServerMode && _differentServerDatabases != null)
                {
                    databasesToFilter = _differentServerDatabases;
                }
                else
                {
                    databasesToFilter = _targetDatabases;
                }
                
                // Filter and update the ListBox
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    // Show all databases when search is empty
                    lstTargetDatabases.ItemsSource = databasesToFilter;
                }
                else
                {
                    // Filter databases based on search term
                    var filteredDatabases = databasesToFilter
                        .Where(db => db.Name.ToLower().Contains(searchTerm))
                        .ToList();
                    
                    lstTargetDatabases.ItemsSource = filteredDatabases;
                }
            }
        }

        private void cmbSourceDatabase_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSourceDatabase.SelectedItem != null)
            {
                // Auto-load stored procedures and tables when database is selected
                Task.Run(() =>
                {
                    LoadTables();
                });
            }
        }

        private void FullscreenTargetDatabases_Click(object sender, RoutedEventArgs e)
        {
            ObservableCollection<TargetDatabase> databasesToShow;
            
            if (_isDifferentServerMode && _differentServerDatabases != null)
            {
                databasesToShow = _differentServerDatabases;
            }
            else
            {
                databasesToShow = _targetDatabases;
            }

            var selectorWindow = new DatabaseSelectorWindow(databasesToShow);
            selectorWindow.Owner = this;
            
            if (selectorWindow.ShowDialog() == true)
            {
                // Update the original collection with the selections
                if (_isDifferentServerMode && _differentServerDatabases != null)
                {
                    UpdateDatabaseSelections(_differentServerDatabases, selectorWindow.SelectedDatabases);
                }
                else
                {
                    UpdateDatabaseSelections(_targetDatabases, selectorWindow.SelectedDatabases);
                }
                
                // Update the count display
                UpdateTargetDatabaseCount();
            }
        }

        private void UpdateDatabaseSelections(ObservableCollection<TargetDatabase> originalList, ObservableCollection<TargetDatabase> selectedList)
        {
            // First, unselect all databases
            foreach (var db in originalList)
            {
                db.IsSelected = false;
            }

            // Then, select the ones that were chosen in the fullscreen dialog
            foreach (var selectedDb in selectedList)
            {
                var originalDb = originalList.FirstOrDefault(d => d.Name == selectedDb.Name);
                if (originalDb != null)
                {
                    originalDb.IsSelected = true;
                }
            }
        }

        #endregion
    }
}

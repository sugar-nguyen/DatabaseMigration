using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DatabaseMigrationTool.Models;
using DatabaseMigrationTool.Services;
using Microsoft.Data.SqlClient;
using Syncfusion.Windows.Shared;

namespace DatabaseMigrationTool
{
    public partial class QuickMigrateWindow : ChromelessWindow
    {
        private readonly DatabaseService _databaseService;
        private readonly ConnectionSettingsService _connectionService;
        private string _currentScript = string.Empty;
        private ObservableCollection<TargetDatabase> _targetDatabases;

        public QuickMigrateWindow()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            _connectionService = new ConnectionSettingsService();
            _targetDatabases = new ObservableCollection<TargetDatabase>();
            
            // Attach event handler after InitializeComponent to avoid null reference
            if (chkWindowsAuth != null)
            {
                chkWindowsAuth.Checked += WindowsAuth_Changed;
                chkWindowsAuth.Unchecked += WindowsAuth_Changed;
            }
            
            // Load connection history
            LoadConnectionHistory();
            
            InitializeLog();
        }

        private void InitializeLog()
        {
            LogMessage("=== QUICK MIGRATE - STORED PROCEDURE ===");
            LogMessage($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            LogMessage("");
            LogMessage("📋 Instructions:");
            LogMessage("1. Paste your CREATE or ALTER PROCEDURE script");
            LogMessage("2. Configure connection and test it");
            LogMessage("3. Load databases and select target");
            LogMessage("4. Validate and Execute script");
            LogMessage("");
            LogMessage("⚠️  Only stored procedure scripts are allowed!");
            LogMessage("⚠️  Script will be executed exactly as provided!");
            LogMessage("====================================");
            LogMessage("");
        }

        private void LoadConnectionHistory()
        {
            try
            {
                var connections = _connectionService.GetServerConnections();

                cmbServer.Items.Clear();

                // Add server names from history
                var serverNames = connections.Select(c => c.ServerName).Distinct().ToList();
                foreach (var serverName in serverNames)
                {
                    cmbServer.Items.Add(serverName);
                }

                // Store connections for later use
                cmbServer.Tag = connections;

                // Add common server names if no history exists
                if (!serverNames.Any())
                {
                    var commonServers = new List<string>
                    {
                        "localhost",
                        ".\\SQLEXPRESS",
                        "(local)",
                        "127.0.0.1"
                    };

                    foreach (var server in commonServers)
                    {
                        cmbServer.Items.Add(server);
                    }
                }

                LogMessage($"Loaded {connections.Count} connection(s) from history");
            }
            catch (Exception ex)
            {
                LogMessage($"Warning: Could not load connection history: {ex.Message}");
            }
        }

        private void ScriptEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _currentScript = txtScriptEditor.Text;

                // Update statistics
                var lines = _currentScript.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                txtLineCount.Text = $"{lines.Length} lines";
                txtCharCount.Text = $"{_currentScript.Length:N0} chars";

                // Update status
                if (string.IsNullOrWhiteSpace(_currentScript))
                {
                    txtScriptStatus.Text = "Ready";
                    txtScriptStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4EC9B0"));
                }
                else
                {
                    txtScriptStatus.Text = "Script Loaded";
                    txtScriptStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#569CD6"));
                }

                UpdateExecuteButtonState();
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating script statistics: {ex.Message}");
            }
        }

        private void ValidateScript_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogMessage("=== VALIDATING SCRIPT ===");

                if (string.IsNullOrWhiteSpace(_currentScript))
                {
                    LogMessage("✗ No script to validate");
                    MessageBox.Show("Please paste a script first.", "No Script",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var validation = ValidateStoredProcedureScript(_currentScript);

                if (validation.IsValid)
                {
                    LogMessage($"✓ Script is valid");
                    LogMessage($"  Type: {validation.ScriptType}");
                    LogMessage($"  Procedure Name: {validation.ProcedureName}");
                    LogMessage("");

                    txtScriptStatus.Text = $"Valid - {validation.ScriptType}";
                    txtScriptStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4EC9B0"));

                    MessageBox.Show(
                        $"✓ Script is valid!\n\n" +
                        $"Type: {validation.ScriptType}\n" +
                        $"Procedure: {validation.ProcedureName}",
                        "Validation Successful",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    LogMessage($"✗ Validation failed: {validation.ErrorMessage}");
                    LogMessage("");

                    txtScriptStatus.Text = "Invalid Script";
                    txtScriptStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F44336"));

                    MessageBox.Show(
                        $"✗ Script validation failed!\n\n{validation.ErrorMessage}",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                UpdateExecuteButtonState();
            }
            catch (Exception ex)
            {
                LogMessage($"✗ Error during validation: {ex.Message}");
                MessageBox.Show($"Validation error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ScriptValidation ValidateStoredProcedureScript(string script)
        {
            var result = new ScriptValidation();

            try
            {
                // Remove comments but keep the script structure
                var cleanedScript = Regex.Replace(script, @"--.*$", "", RegexOptions.Multiline);
                cleanedScript = Regex.Replace(cleanedScript, @"/\*.*?\*/", "", RegexOptions.Singleline);
                cleanedScript = cleanedScript.Trim();

                // Check for CREATE PROCEDURE or ALTER PROCEDURE at the beginning
                var createMatch = Regex.Match(cleanedScript, 
                    @"^\s*CREATE\s+(?:PROCEDURE|PROC)\s+(?:\[?(\w+)\]?\.)?\[?(\w+)\]?",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                var alterMatch = Regex.Match(cleanedScript, 
                    @"^\s*ALTER\s+(?:PROCEDURE|PROC)\s+(?:\[?(\w+)\]?\.)?\[?(\w+)\]?",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

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

                // Additional validation: Check for dangerous DDL operations at script level (not inside procedure body)
                // Only check before the AS keyword (procedure definition)
                var procedureHeaderMatch = Regex.Match(cleanedScript, 
                    @"(?:CREATE|ALTER)\s+(?:PROCEDURE|PROC)\s+.*?\s+AS\s+",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (procedureHeaderMatch.Success)
                {
                    // Get only the header part (before AS)
                    var headerPart = cleanedScript.Substring(0, procedureHeaderMatch.Index + procedureHeaderMatch.Length);

                    // Check for dangerous DDL keywords in header only
                    var dangerousDDLKeywords = new[] { "DROP DATABASE", "DROP TABLE", "TRUNCATE DATABASE" };
                    foreach (var keyword in dangerousDDLKeywords)
                    {
                        if (Regex.IsMatch(headerPart, $@"\b{keyword}\b", RegexOptions.IgnoreCase))
                        {
                            result.IsValid = false;
                            result.ErrorMessage = $"Script contains forbidden DDL operation: {keyword}. Only stored procedure definitions are allowed.";
                            return result;
                        }
                    }
                }

                // Warn if script contains multiple statements (possible SQL injection or unsafe batch)
                var procedureCount = Regex.Matches(cleanedScript, 
                    @"\b(?:CREATE|ALTER)\s+(?:PROCEDURE|PROC)\b", 
                    RegexOptions.IgnoreCase).Count;

                if (procedureCount > 1)
                {
                    result.ErrorMessage = "Script contains multiple CREATE/ALTER PROCEDURE statements. Only one procedure per script is allowed.";
                    result.IsValid = false;
                    return result;
                }

                // Validation passed
                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Validation error: {ex.Message}";
                result.IsValid = false;
            }

            return result;
        }

        private void ClearScript_Click(object sender, RoutedEventArgs e)
        {
            txtScriptEditor.Clear();
            _currentScript = string.Empty;
            txtLineCount.Text = "0 lines";
            txtCharCount.Text = "0 chars";
            txtScriptStatus.Text = "Ready";
            txtScriptStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4EC9B0"));

            LogMessage("Script cleared");
            UpdateExecuteButtonState();
        }

        private void WindowsAuth_Changed(object sender, RoutedEventArgs e)
        {
            // Check if controls are initialized to prevent NullReferenceException
            if (pnlSqlAuth != null && chkWindowsAuth != null)
            {
                pnlSqlAuth.Visibility = chkWindowsAuth.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
            }
        }

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

                LogMessage($"=== TESTING CONNECTION ===");
                LogMessage($"Server: {cmbServer.Text}");
                LogMessage($"Auth: {(chkWindowsAuth.IsChecked == true ? "Windows" : "SQL Server")}");

                var settings = new ConnectionSettings
                {
                    ServerName = cmbServer.Text,
                    UseWindowsAuthentication = chkWindowsAuth.IsChecked == true,
                    Username = txtUsername.Text,
                    Password = txtPassword.Password
                };

                var success = await _databaseService.TestConnectionAsync(settings);

                if (success)
                {
                    LogMessage("✓ Connection successful");
                    LogMessage("");
                    
                    // Save connection to history
                    _connectionService.SaveServerConnection(settings);
                    
                    // Preserve current server name when refreshing history
                    var currentServerName = cmbServer.Text;
                    LoadConnectionHistory();
                    cmbServer.Text = currentServerName; // Restore the server name
                    
                    MessageBox.Show("Connection successful!", "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    LogMessage("✗ Connection failed");
                    LogMessage("");
                    MessageBox.Show("Connection failed. Check server name and credentials.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }

                UpdateExecuteButtonState();
            }
            catch (Exception ex)
            {
                LogMessage($"✗ Connection error: {ex.Message}");
                LogMessage("");
                MessageBox.Show($"Connection error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

                    // Update last used
                    _connectionService.UpdateLastUsed(matchingConnection);

                    LogMessage($"Auto-filled credentials from history for: {selectedServerName}");
                }
            }
        }

        private async void LoadDatabases_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cmbServer.Text))
                {
                    MessageBox.Show("Please enter a server name first.", "Missing Information",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                LogMessage("=== LOADING DATABASES ===");

                var settings = new ConnectionSettings
                {
                    ServerName = cmbServer.Text,
                    UseWindowsAuthentication = chkWindowsAuth.IsChecked == true,
                    Username = txtUsername.Text,
                    Password = txtPassword.Password
                };

                var databases = await _databaseService.GetDatabasesAsync(settings);

                _targetDatabases.Clear();
                foreach (var db in databases)
                {
                    _targetDatabases.Add(new TargetDatabase { Name = db, IsSelected = false });
                }

                // Bind to ListBox
                lstDatabases.ItemsSource = _targetDatabases;

                LogMessage($"✓ Loaded {databases.Count} databases");
                LogMessage("");
                
                // Save connection to history when databases loaded successfully
                _connectionService.SaveServerConnection(settings);

                // Preserve current server name when refreshing history
                var currentServerName = cmbServer.Text;
                LoadConnectionHistory();
                cmbServer.Text = currentServerName; // Restore the server name

                UpdateSelectedDatabaseCount();
                UpdateExecuteButtonState();
            }
            catch (Exception ex)
            {
                LogMessage($"✗ Error loading databases: {ex.Message}");
                LogMessage("");
                MessageBox.Show($"Error loading databases: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DatabaseSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = txtDatabaseSearch.Text?.ToLower() ?? string.Empty;
            
            if (string.IsNullOrEmpty(searchText))
            {
                lstDatabases.ItemsSource = _targetDatabases;
            }
            else
            {
                var filtered = _targetDatabases.Where(db => db.Name.ToLower().Contains(searchText)).ToList();
                lstDatabases.ItemsSource = filtered;
            }
        }

        private void DatabaseCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSelectedDatabaseCount();
            UpdateExecuteButtonState();
        }

        private void SelectAllDatabases_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var db in _targetDatabases)
            {
                db.IsSelected = true;
            }
            UpdateSelectedDatabaseCount();
            UpdateExecuteButtonState();
        }

        private void SelectAllDatabases_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var db in _targetDatabases)
            {
                db.IsSelected = false;
            }
            UpdateSelectedDatabaseCount();
            UpdateExecuteButtonState();
        }

        private void ClearAllDatabases_Click(object sender, RoutedEventArgs e)
        {
            if (_targetDatabases != null)
            {
                foreach (var db in _targetDatabases)
                {
                    db.IsSelected = false;
                }
                chkSelectAllDatabases.IsChecked = false;
                UpdateSelectedDatabaseCount();
                UpdateExecuteButtonState();
            }
        }

        private void UpdateSelectedDatabaseCount()
        {
            if (_targetDatabases != null && txtSelectedDbCount != null)
            {
                var selectedCount = _targetDatabases.Count(db => db.IsSelected);
                txtSelectedDbCount.Text = $"{selectedCount} selected";
            }
        }

        private async void ExecuteScript_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate all requirements
                if (string.IsNullOrWhiteSpace(_currentScript))
                {
                    MessageBox.Show("Please paste a script first.", "No Script",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(cmbServer.Text))
                {
                    MessageBox.Show("Please configure server connection.", "No Connection",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedDatabases = _targetDatabases?.Where(db => db.IsSelected).ToList();
                if (selectedDatabases == null || !selectedDatabases.Any())
                {
                    MessageBox.Show("Please select at least one target database.", "No Database",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validate script
                var validation = ValidateStoredProcedureScript(_currentScript);
                if (!validation.IsValid)
                {
                    MessageBox.Show($"Script validation failed:\n\n{validation.ErrorMessage}",
                        "Invalid Script", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Confirm execution
                var databaseList = string.Join("\n  • ", selectedDatabases.Select(db => db.Name));
                var result = MessageBox.Show(
                    $"Execute script on {selectedDatabases.Count} database(s)?\n\n" +
                    $"Target Databases:\n  • {databaseList}\n\n" +
                    $"Type: {validation.ScriptType}\n" +
                    $"Procedure: {validation.ProcedureName}\n\n" +
                    $"⚠️ The script will be executed exactly as provided.",
                    "Confirm Execution",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                LogMessage("=== EXECUTING SCRIPT ===");
                LogMessage($"Target Databases: {selectedDatabases.Count}");
                foreach (var db in selectedDatabases)
                {
                    LogMessage($"  • {db.Name}");
                }
                LogMessage($"Procedure: {validation.ProcedureName}");
                LogMessage($"Script Type: {validation.ScriptType}");
                LogMessage("");

                btnExecute.IsEnabled = false;

                // Execute script on all selected databases
                var successCount = 0;
                var failCount = 0;

                foreach (var targetDb in selectedDatabases)
                {
                    LogMessage($"→ Processing database: {targetDb.Name}");

                    var settings = new ConnectionSettings
                    {
                        ServerName = cmbServer.Text,
                        DatabaseName = targetDb.Name,
                        UseWindowsAuthentication = chkWindowsAuth.IsChecked == true,
                        Username = txtUsername.Text,
                        Password = txtPassword.Password
                    };

                    try
                    {
                        var success = await ExecuteScriptAsync(settings, _currentScript, validation);
                        if (success)
                        {
                            successCount++;
                            LogMessage($"  ✓ {targetDb.Name}: Success");
                        }
                        else
                        {
                            failCount++;
                            LogMessage($"  ✗ {targetDb.Name}: Failed");
                        }
                    }
                    catch (Exception dbEx)
                    {
                        failCount++;
                        LogMessage($"  ✗ {targetDb.Name}: {dbEx.Message}");
                    }

                    LogMessage("");
                }

                // Summary
                LogMessage("=== EXECUTION SUMMARY ===");
                LogMessage($"Total Databases: {selectedDatabases.Count}");
                LogMessage($"Successful: {successCount}");
                LogMessage($"Failed: {failCount}");
                LogMessage($"Success Rate: {(selectedDatabases.Count > 0 ? (successCount * 100.0 / selectedDatabases.Count) : 0):F1}%");
                LogMessage("");

                if (failCount == 0)
                {
                    MessageBox.Show(
                        $"✓ Script executed successfully on all {successCount} database(s)!",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Script execution completed:\n\n" +
                        $"✓ Success: {successCount}\n" +
                        $"✗ Failed: {failCount}\n\n" +
                        $"Check the log for details.",
                        "Execution Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"✗ Execution error: {ex.Message}");
                LogMessage($"Stack trace: {ex.StackTrace}");
                LogMessage("");
                MessageBox.Show($"Execution error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnExecute.IsEnabled = true;
            }
        }

        private async Task<bool> ExecuteScriptAsync(ConnectionSettings settings, string script,
            ScriptValidation validation)
        {
            try
            {
                // Parse schema and name from procedure
                var parts = validation.ProcedureName.Split('.');
                var schema = parts.Length > 1 ? parts[0] : "dbo";
                var name = parts.Length > 1 ? parts[1] : parts[0];

                // Check if procedure exists (for logging only)
                var procedureExists = await _databaseService.StoredProcedureExistsAsync(settings, schema, name);
                LogMessage($"Procedure currently exists: {procedureExists}");
                
                // Log warning if there's a mismatch
                if (validation.ScriptType == "CREATE PROCEDURE" && procedureExists)
                {
                    LogMessage("⚠️  Warning: Using CREATE but procedure already exists - this may fail!");
                }
                else if (validation.ScriptType == "ALTER PROCEDURE" && !procedureExists)
                {
                    LogMessage("⚠️  Warning: Using ALTER but procedure doesn't exist - this may fail!");
                }
                
                LogMessage("");
                LogMessage("→ Executing script as provided (no modifications)...");

                // Execute script exactly as provided by user
                using var connection = new SqlConnection(settings.GetConnectionString());
                await connection.OpenAsync();
                
                using var command = new SqlCommand(script, connection);
                command.CommandTimeout = 60;
                await command.ExecuteNonQueryAsync();

                LogMessage($"✓ Script executed successfully");
                return true;
            }
            catch (SqlException sqlEx)
            {
                LogMessage($"✗ SQL Error: {sqlEx.Message}");
                LogMessage($"  Error Number: {sqlEx.Number}");
                LogMessage($"  Line Number: {sqlEx.LineNumber}");
                
                // Provide helpful suggestions based on error
                if (sqlEx.Number == 2714) // Object already exists
                {
                    LogMessage("");
                    LogMessage("💡 Suggestion: The procedure already exists. Use ALTER PROCEDURE instead of CREATE PROCEDURE.");
                }
                else if (sqlEx.Number == 3701 || sqlEx.Number == 208) // Cannot find object
                {
                    LogMessage("");
                    LogMessage("💡 Suggestion: The procedure doesn't exist. Use CREATE PROCEDURE instead of ALTER PROCEDURE.");
                }
                
                throw;
            }
            catch (Exception ex)
            {
                LogMessage($"✗ Error: {ex.Message}");
                throw;
            }
        }

        private void Database_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateExecuteButtonState();
        }

        private void UpdateExecuteButtonState()
        {
            var hasSelectedDatabases = _targetDatabases?.Any(db => db.IsSelected) == true;
            
            btnExecute.IsEnabled = !string.IsNullOrWhiteSpace(_currentScript) &&
                                  !string.IsNullOrWhiteSpace(cmbServer.Text) &&
                                  hasSelectedDatabases;
        }

        private void LogMessage(string message)
        {
            txtExecutionLog.AppendText($"{message}\n");
            txtExecutionLog.ScrollToEnd();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private class ScriptValidation
        {
            public bool IsValid { get; set; }
            public string ScriptType { get; set; } = string.Empty;
            public string ProcedureName { get; set; } = string.Empty;
            public string ErrorMessage { get; set; } = string.Empty;
        }
    }
}

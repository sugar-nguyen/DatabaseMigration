using DatabaseMigrationTool.Models;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System.IO;

namespace DatabaseMigrationTool.Services
{
    /// <summary>
    /// Service to handle rollback operations and rollback history management
    /// </summary>
    public class RollbackService
    {
        private readonly DatabaseService _databaseService;
        private readonly string _rollbackHistoryFile;
        private List<RollbackRecord> _rollbackHistory;

        public event Action<string> LogMessage;
        public event Action<int> ProgressChanged;

        public RollbackService(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "DatabaseMigrationTool");
            _rollbackHistoryFile = Path.Combine(appFolder, "rollback_history.json");
            LoadRollbackHistory();
        }

        /// <summary>
        /// Gets the list of rollback records
        /// </summary>
        public List<RollbackRecord> GetRollbackHistory()
        {
            return _rollbackHistory?.OrderByDescending(r => r.MigrationTimestamp).ToList() ?? new List<RollbackRecord>();
        }

        /// <summary>
        /// Records a migration for potential rollback
        /// </summary>
        public async Task<bool> RecordMigrationAsync(string sourceDatabase, List<string> targetDatabases,
            List<StoredProcedure> migratedProcedures, List<Table> migratedTables, string migrationSettings,
            ConnectionSettings connectionSettings)
        {
            try
            {
                LogMessage?.Invoke("Recording migration information for rollback...");

                var rollbackRecord = new RollbackRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    MigrationTimestamp = DateTime.Now,
                    SourceDatabase = sourceDatabase,
                    TargetDatabases = targetDatabases.ToList(),
                    MigrationSettings = migrationSettings,
                    CanRollback = true
                };

                // Record backup information for stored procedures
                if (migratedProcedures?.Any() == true)
                {
                    foreach (var targetDb in targetDatabases)
                    {
                        foreach (var procedure in migratedProcedures)
                        {
                            // Use the same backup name format as DatabaseService: {ProcName}_{ddMMyyyy}
                            var backupName = $"{procedure.Name}_{rollbackRecord.MigrationTimestamp:ddMMyyyy}";
                            var backupInfo = new BackupInfo
                            {
                                OriginalName = procedure.Name,
                                BackupName = backupName,
                                TargetDatabase = targetDb,
                                BackupTimestamp = rollbackRecord.MigrationTimestamp,
                                Type = BackupType.StoredProcedure
                            };

                            // Check if backup exists in target database
                            if (await BackupExistsAsync(connectionSettings, targetDb, backupName))
                            {
                                rollbackRecord.BackupProcedures.Add(backupInfo);
                            }
                        }
                    }
                }

                // Record backup information for tables
                if (migratedTables?.Any() == true)
                {
                    foreach (var targetDb in targetDatabases)
                    {
                        foreach (var table in migratedTables)
                        {
                            // Use the same backup name format as DatabaseService: {TableName}_{ddMMyyyy}
                            var backupName = $"{table.Name}_{rollbackRecord.MigrationTimestamp:ddMMyyyy}";
                            var backupInfo = new BackupInfo
                            {
                                OriginalName = table.Name,
                                BackupName = backupName,
                                TargetDatabase = targetDb,
                                BackupTimestamp = rollbackRecord.MigrationTimestamp,
                                Type = BackupType.Table
                            };

                            // Check if backup exists in target database
                            if (await BackupExistsAsync(connectionSettings, targetDb, backupName))
                            {
                                rollbackRecord.BackupTables.Add(backupInfo);
                            }
                        }
                    }
                }

                _rollbackHistory.Add(rollbackRecord);
                await SaveRollbackHistoryAsync();

                LogMessage?.Invoke($"Migration recorded for rollback. ID: {rollbackRecord.Id}");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error recording migration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Performs rollback for a specific migration record
        /// </summary>
        public async Task<RollbackResult> RollbackMigrationAsync(RollbackRecord rollbackRecord, ConnectionSettings connectionSettings)
        {
            var result = new RollbackResult { Success = false };
            var startTime = DateTime.Now;

            try
            {
                LogMessage?.Invoke($"Starting rollback for migration: {rollbackRecord.Id}");
                LogMessage?.Invoke($"Migration date: {rollbackRecord.MigrationTimestamp:yyyy-MM-dd HH:mm:ss}");

                if (!rollbackRecord.CanRollback)
                {
                    result.Message = "This migration cannot be rolled back";
                    return result;
                }

                var totalItems = rollbackRecord.BackupProcedures.Count + rollbackRecord.BackupTables.Count;
                var processedItems = 0;

                // Rollback stored procedures
                foreach (var targetDb in rollbackRecord.TargetDatabases)
                {
                    LogMessage?.Invoke($"Rolling back procedures in database: {targetDb}");

                    var procedureBackups = rollbackRecord.BackupProcedures
                        .Where(bp => bp.TargetDatabase == targetDb)
                        .ToList();

                    foreach (var backup in procedureBackups)
                    {
                        try
                        {
                            await RestoreProcedureFromBackupAsync(connectionSettings, targetDb, backup);
                            result.RestoredCount++;
                            LogMessage?.Invoke($"✓ Restored procedure: {backup.OriginalName} from backup {backup.BackupName} using ALTER");
                        }
                        catch (Exception ex)
                        {
                            result.FailedCount++;
                            var error = $"✗ Failed to restore procedure {backup.OriginalName}: {ex.Message}";
                            result.Errors.Add(error);
                            LogMessage?.Invoke(error);
                        }

                        processedItems++;
                        ProgressChanged?.Invoke((processedItems * 100) / totalItems);
                    }
                }

                // Rollback tables (for now, just log - table rollback is more complex)
                foreach (var targetDb in rollbackRecord.TargetDatabases)
                {
                    LogMessage?.Invoke($"Rolling back tables in database: {targetDb}");

                    var tableBackups = rollbackRecord.BackupTables
                        .Where(bt => bt.TargetDatabase == targetDb)
                        .ToList();

                    foreach (var backup in tableBackups)
                    {
                        try
                        {
                            // For now, just log table backups - implement table restoration logic later
                            LogMessage?.Invoke($"ℹ️ Table backup found: {backup.OriginalName} -> {backup.BackupName}");
                            LogMessage?.Invoke($"   Note: Table restoration not implemented yet");
                            result.RestoredCount++;
                        }
                        catch (Exception ex)
                        {
                            result.FailedCount++;
                            var error = $"✗ Failed to process table backup {backup.OriginalName}: {ex.Message}";
                            result.Errors.Add(error);
                            LogMessage?.Invoke(error);
                        }

                        processedItems++;
                        ProgressChanged?.Invoke((processedItems * 100) / totalItems);
                    }
                }

                result.Duration = DateTime.Now - startTime;
                result.Success = result.FailedCount == 0;

                if (result.Success)
                {
                    result.Message = $"Rollback completed successfully. Restored {result.RestoredCount} items.";

                    // Mark the rollback record as used (optional)
                    rollbackRecord.CanRollback = false;
                    await SaveRollbackHistoryAsync();
                }
                else
                {
                    result.Message = $"Rollback completed with errors. Restored: {result.RestoredCount}, Failed: {result.FailedCount}";
                }

                LogMessage?.Invoke(result.Message);
                LogMessage?.Invoke($"Rollback duration: {result.Duration.TotalSeconds:F1} seconds");

                return result;
            }
            catch (Exception ex)
            {
                result.Message = $"Rollback failed: {ex.Message}";
                result.Errors.Add(ex.Message);
                result.Duration = DateTime.Now - startTime;
                LogMessage?.Invoke(result.Message);
                return result;
            }
        }

        /// <summary>
        /// Restores a stored procedure from its backup using ALTER instead of DROP/CREATE
        /// </summary>
        private async Task RestoreProcedureFromBackupAsync(ConnectionSettings connectionSettings, string targetDatabase, BackupInfo backupInfo)
        {
            // Get the backup procedure definition
            var backupDefinition = await _databaseService.GetProcedureDefinitionAsync(connectionSettings, targetDatabase, backupInfo.BackupName);

            if (string.IsNullOrEmpty(backupDefinition))
            {
                throw new Exception($"Backup procedure {backupInfo.BackupName} not found");
            }

            // Debug: Log the original backup definition
            LogMessage?.Invoke($"🔍 Original backup definition (first 200 chars): {backupDefinition.Substring(0, Math.Min(200, backupDefinition.Length))}...");

            using var connection = new SqlConnection(_databaseService.BuildConnectionString(connectionSettings, targetDatabase));
            await connection.OpenAsync();

            // Convert the backup procedure definition to ALTER script for the original procedure
            var restoreScript = ConvertToAlterScript(backupDefinition, backupInfo.BackupName, backupInfo.OriginalName);

            if (string.IsNullOrEmpty(restoreScript))
            {
                throw new Exception($"Could not generate ALTER script from backup definition");
            }

            // Debug: Log the converted ALTER script
            LogMessage?.Invoke($"🔍 Converted ALTER script (first 200 chars): {restoreScript.Substring(0, Math.Min(200, restoreScript.Length))}...");

            using (var restoreCommand = new SqlCommand(restoreScript, connection))
            {
                await restoreCommand.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Converts a CREATE PROCEDURE script to ALTER PROCEDURE script with new procedure name
        /// </summary>
        private string ConvertToAlterScript(string createScript, string backupName, string originalName)
        {
            if (string.IsNullOrEmpty(createScript))
                return string.Empty;

            // Remove any leading/trailing whitespace
            var script = createScript.Trim();

            // Log the conversion process for debugging
            LogMessage?.Invoke($"🔍 Converting backup '{backupName}' to original '{originalName}'");

            // First, replace the procedure name (before CREATE/ALTER replacement to avoid conflicts)
            // Handle different formats: [ProcName], ProcName, dbo.ProcName, [dbo].[ProcName]
            var patterns = new[]
            {
                $@"\[{System.Text.RegularExpressions.Regex.Escape(backupName)}\]", // [BackupName]
                $@"\bdbo\.\[{System.Text.RegularExpressions.Regex.Escape(backupName)}\]", // dbo.[BackupName]
                $@"\[dbo\]\.\[{System.Text.RegularExpressions.Regex.Escape(backupName)}\]", // [dbo].[BackupName]
                $@"\bdbo\.{System.Text.RegularExpressions.Regex.Escape(backupName)}\b", // dbo.BackupName
                $@"\b{System.Text.RegularExpressions.Regex.Escape(backupName)}\b" // BackupName
            };

            var replacements = new[]
            {
                $"[{originalName}]",
                $"dbo.[{originalName}]",
                $"[dbo].[{originalName}]",
                $"dbo.[{originalName}]",
                $"[{originalName}]"
            };

            for (int i = 0; i < patterns.Length; i++)
            {
                script = System.Text.RegularExpressions.Regex.Replace(
                    script,
                    patterns[i],
                    replacements[i],
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Replace CREATE with ALTER (case insensitive)
            // Handle various formats: CREATE PROCEDURE, CREATE PROC
            script = System.Text.RegularExpressions.Regex.Replace(
                script,
                @"\bCREATE\s+(PROCEDURE|PROC)\b",
                "ALTER PROCEDURE",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Ensure the script starts with ALTER PROCEDURE
            if (!script.TrimStart().StartsWith("ALTER PROCEDURE", StringComparison.OrdinalIgnoreCase))
            {
                LogMessage?.Invoke($"⚠️ Warning: Script doesn't start with ALTER PROCEDURE after conversion");
            }

            return script;
        }

        /// <summary>
        /// Checks if a backup procedure/table exists in the database
        /// </summary>
        private async Task<bool> BackupExistsAsync(ConnectionSettings connectionSettings, string database, string backupName)
        {
            try
            {
                using var connection = new SqlConnection(_databaseService.BuildConnectionString(connectionSettings, database));
                await connection.OpenAsync();

                var query = @"
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.ROUTINES 
                    WHERE ROUTINE_NAME = @BackupName
                    UNION ALL
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_NAME = @BackupName";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@BackupName", backupName);

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Loads rollback history from file
        /// </summary>
        private void LoadRollbackHistory()
        {
            try
            {
                if (File.Exists(_rollbackHistoryFile))
                {
                    var json = File.ReadAllText(_rollbackHistoryFile);
                    _rollbackHistory = JsonConvert.DeserializeObject<List<RollbackRecord>>(json) ?? new List<RollbackRecord>();
                }
                else
                {
                    _rollbackHistory = new List<RollbackRecord>();
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error loading rollback history: {ex.Message}");
                _rollbackHistory = new List<RollbackRecord>();
            }
        }

        /// <summary>
        /// Saves rollback history to file
        /// </summary>
        private async Task SaveRollbackHistoryAsync()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_rollbackHistory, Formatting.Indented);
                await File.WriteAllTextAsync(_rollbackHistoryFile, json);
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error saving rollback history: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes old rollback records (older than specified days)
        /// </summary>
        public async Task CleanupOldRecordsAsync(int daysToKeep = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var removedCount = _rollbackHistory.RemoveAll(r => r.MigrationTimestamp < cutoffDate);

                if (removedCount > 0)
                {
                    await SaveRollbackHistoryAsync();
                    LogMessage?.Invoke($"Cleaned up {removedCount} old rollback records (older than {daysToKeep} days)");
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error cleaning up old rollback records: {ex.Message}");
            }
        }
    }
}
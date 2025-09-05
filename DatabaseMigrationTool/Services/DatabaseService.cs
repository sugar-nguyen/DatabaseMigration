using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DatabaseMigrationTool.Models;

namespace DatabaseMigrationTool.Services
{
    public class DatabaseService
    {
        public async Task<bool> TestConnectionAsync(ConnectionSettings settings)
        {
            try
            {
                using var connection = new SqlConnection(settings.GetConnectionString());
                await connection.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<string>> GetDatabasesAsync(ConnectionSettings settings)
        {
            var databases = new List<string>();

            try
            {
                var connectionString = settings.GetConnectionString().Replace($"Database={settings.DatabaseName};", "Database=master;");
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var command = new SqlCommand("SELECT name FROM sys.databases WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb') ORDER BY name", connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    databases.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to retrieve databases: {ex.Message}");
            }

            return databases;
        }

        public async Task<List<StoredProcedure>> GetStoredProceduresAsync(ConnectionSettings settings)
        {
            var storedProcedures = new List<StoredProcedure>();

            try
            {
                using var connection = new SqlConnection(settings.GetConnectionString());
                await connection.OpenAsync();

                var command = new SqlCommand(@"
                    SELECT 
                        s.name as schema_name,
                        p.name as procedure_name,
                        m.definition
                    FROM sys.procedures p
                    INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
                    INNER JOIN sys.sql_modules m ON p.object_id = m.object_id
                    WHERE p.type = 'P'
                    ORDER BY s.name, p.name", connection);

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    storedProcedures.Add(new StoredProcedure
                    {
                        Schema = reader.GetString("schema_name"),
                        Name = reader.GetString("procedure_name"),
                        Definition = reader.GetString("definition")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to retrieve stored procedures: {ex.Message}");
            }

            return storedProcedures;
        }

        public async Task<bool> DatabaseExistsAsync(ConnectionSettings settings, string databaseName)
        {
            try
            {
                var connectionString = settings.GetConnectionString().Replace($"Database={settings.DatabaseName};", "Database=master;");
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var command = new SqlCommand("SELECT COUNT(*) FROM sys.databases WHERE name = @dbName", connection);
                command.Parameters.AddWithValue("@dbName", databaseName);

                var result = await command.ExecuteScalarAsync();
                var count = result != null ? (int)result : 0;
                return count > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task CreateDatabaseAsync(ConnectionSettings settings, string databaseName)
        {
            try
            {
                var connectionString = settings.GetConnectionString().Replace($"Database={settings.DatabaseName};", "Database=master;");
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var command = new SqlCommand($"CREATE DATABASE [{databaseName}]", connection);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create database '{databaseName}': {ex.Message}");
            }
        }

        public async Task<bool> StoredProcedureExistsAsync(ConnectionSettings settings, string schemaName, string procedureName)
        {
            try
            {
                using var connection = new SqlConnection(settings.GetConnectionString());
                await connection.OpenAsync();

                var command = new SqlCommand(@"
                    SELECT COUNT(*) 
                    FROM sys.procedures p
                    INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
                    WHERE s.name = @schema AND p.name = @procedure", connection);

                command.Parameters.AddWithValue("@schema", schemaName);
                command.Parameters.AddWithValue("@procedure", procedureName);

                var result = await command.ExecuteScalarAsync();
                var count = result != null ? (int)result : 0;
                return count > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task CreateOrAlterStoredProcedureAsync(ConnectionSettings settings, StoredProcedure sp, bool createBackup = false)
        {
            try
            {
                using var connection = new SqlConnection(settings.GetConnectionString());
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Check if procedure exists
                    bool procedureExists = await StoredProcedureExistsInTransactionAsync(connection, transaction, sp.Schema, sp.Name);

                    // Create backup if procedure exists and backup is requested
                    if (procedureExists && createBackup)
                    {
                        await CreateBackupStoredProcedureAsync(connection, transaction, sp);
                    }

                    // Modify the stored procedure definition to use ALTER if it exists, CREATE if it doesn't
                    var modifiedDefinition = await PrepareStoredProcedureDefinitionAsync(sp.Definition, sp.Schema, sp.Name, procedureExists);

                    // Execute the stored procedure
                    var command = new SqlCommand(modifiedDefinition, connection, transaction);
                    await command.ExecuteNonQueryAsync();

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create/alter stored procedure '{sp.FullName}': {ex.Message}");
            }
        }

        private async Task<bool> StoredProcedureExistsInTransactionAsync(SqlConnection connection, SqlTransaction transaction, string schemaName, string procedureName)
        {
            var command = new SqlCommand(@"
                SELECT COUNT(*) 
                FROM sys.procedures p
                INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
                WHERE s.name = @schema AND p.name = @procedure", connection, transaction);

            command.Parameters.AddWithValue("@schema", schemaName);
            command.Parameters.AddWithValue("@procedure", procedureName);

            var result = await command.ExecuteScalarAsync();
            var count = result != null ? (int)result : 0;
            return count > 0;
        }

        private async Task<string> PrepareStoredProcedureDefinitionAsync(string originalDefinition, string schemaName, string procedureName, bool procedureExists)
        {
            await Task.CompletedTask; // Make it async for potential future database calls

            // Define possible patterns for procedure creation
            var patterns = new[]
            {
                $"CREATE PROCEDURE [{schemaName}].[{procedureName}]",
                $"CREATE PROC [{schemaName}].[{procedureName}]",
                $"CREATE PROCEDURE {schemaName}.{procedureName}",
                $"CREATE PROC {schemaName}.{procedureName}",
                $"CREATE PROCEDURE [{procedureName}]",
                $"CREATE PROC [{procedureName}]",
                $"CREATE PROCEDURE {procedureName}",
                $"CREATE PROC {procedureName}"
            };

            var alterPatterns = new[]
            {
                $"ALTER PROCEDURE [{schemaName}].[{procedureName}]",
                $"ALTER PROC [{schemaName}].[{procedureName}]",
                $"ALTER PROCEDURE {schemaName}.{procedureName}",
                $"ALTER PROC {schemaName}.{procedureName}",
                $"ALTER PROCEDURE [{procedureName}]",
                $"ALTER PROC [{procedureName}]",
                $"ALTER PROCEDURE {procedureName}",
                $"ALTER PROC {procedureName}"
            };

            string modifiedDefinition = originalDefinition;

            if (procedureExists)
            {
                // Replace CREATE with ALTER if procedure exists
                foreach (var pattern in patterns)
                {
                    var alterPattern = pattern.Replace("CREATE", "ALTER");
                    modifiedDefinition = modifiedDefinition.Replace(pattern, alterPattern, StringComparison.OrdinalIgnoreCase);
                }
            }
            else
            {
                // Replace ALTER with CREATE if procedure doesn't exist
                foreach (var pattern in alterPatterns)
                {
                    var createPattern = pattern.Replace("ALTER", "CREATE");
                    modifiedDefinition = modifiedDefinition.Replace(pattern, createPattern, StringComparison.OrdinalIgnoreCase);
                }
            }

            return modifiedDefinition;
        }

        private async Task CreateBackupStoredProcedureAsync(SqlConnection connection, SqlTransaction transaction, StoredProcedure sp)
        {
            // Get current procedure definition
            var command = new SqlCommand(@"
                SELECT m.definition
                FROM sys.procedures p
                INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
                INNER JOIN sys.sql_modules m ON p.object_id = m.object_id
                WHERE s.name = @schema AND p.name = @procedure", connection, transaction);

            command.Parameters.AddWithValue("@schema", sp.Schema);
            command.Parameters.AddWithValue("@procedure", sp.Name);

            var result = await command.ExecuteScalarAsync();
            var currentDefinition = result?.ToString() ?? string.Empty;

            if (!string.IsNullOrEmpty(currentDefinition))
            {
                var backupName = $"{sp.Name}_{DateTime.Now:ddMMyyyy}";
                var backupDefinition = currentDefinition.Replace($"CREATE PROCEDURE [{sp.Schema}].[{sp.Name}]", $"CREATE PROCEDURE [{sp.Schema}].[{backupName}]", StringComparison.OrdinalIgnoreCase)
                                                      .Replace($"CREATE PROC [{sp.Schema}].[{sp.Name}]", $"CREATE PROC [{sp.Schema}].[{backupName}]", StringComparison.OrdinalIgnoreCase)
                                                      .Replace($"ALTER PROCEDURE [{sp.Schema}].[{sp.Name}]", $"CREATE PROCEDURE [{sp.Schema}].[{backupName}]", StringComparison.OrdinalIgnoreCase)
                                                      .Replace($"ALTER PROC [{sp.Schema}].[{sp.Name}]", $"CREATE PROC [{sp.Schema}].[{backupName}]", StringComparison.OrdinalIgnoreCase);

                var backupCommand = new SqlCommand(backupDefinition, connection, transaction);
                await backupCommand.ExecuteNonQueryAsync();
            }
        }

        public async Task<List<Table>> GetTablesAsync(ConnectionSettings settings)
        {
            var tables = new List<Table>();

            try
            {
                using var connection = new SqlConnection(settings.GetConnectionString());
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        t.TABLE_SCHEMA as SchemaName,
                        t.TABLE_NAME as TableName
                    FROM INFORMATION_SCHEMA.TABLES t
                    WHERE t.TABLE_TYPE = 'BASE TABLE'
                    AND t.TABLE_SCHEMA NOT IN ('sys', 'INFORMATION_SCHEMA')
                    ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME";

                var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                // First, collect all table information
                var tableInfos = new List<(string Schema, string Name)>();
                while (await reader.ReadAsync())
                {
                    var schemaName = reader.GetString("SchemaName");
                    var tableName = reader.GetString("TableName");
                    tableInfos.Add((schemaName, tableName));
                }

                // Close the reader before making new queries
                reader.Close();

                // Now get definitions for each table
                foreach (var (schema, name) in tableInfos)
                {
                    var table = new Table
                    {
                        Schema = schema,
                        Name = name,
                        Definition = await GetTableDefinitionAsync(settings, schema, name)
                    };
                    tables.Add(table);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error retrieving tables: {ex.Message}", ex);
            }

            return tables;
        }

        public async Task<bool> TableExistsAsync(ConnectionSettings settings, string schemaName, string tableName)
        {
            try
            {
                using var connection = new SqlConnection(settings.GetConnectionString());
                await connection.OpenAsync();

                var command = new SqlCommand(@"
                    SELECT COUNT(*) 
                    FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table AND TABLE_TYPE = 'BASE TABLE'", connection);

                command.Parameters.AddWithValue("@schema", schemaName);
                command.Parameters.AddWithValue("@table", tableName);

                var result = await command.ExecuteScalarAsync();
                var count = result != null ? (int)result : 0;
                return count > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetTableScriptAsync(ConnectionSettings settings, string schemaName, string tableName)
        {
            try
            {
                using var connection = new SqlConnection(settings.GetConnectionString());
                await connection.OpenAsync();

                var script = new System.Text.StringBuilder();
                script.AppendLine($"CREATE TABLE [{schemaName}].[{tableName}] (");

                // Get columns
                var columnQuery = @"
                    SELECT 
                        c.COLUMN_NAME,
                        c.DATA_TYPE,
                        c.CHARACTER_MAXIMUM_LENGTH,
                        c.NUMERIC_PRECISION,
                        c.NUMERIC_SCALE,
                        c.IS_NULLABLE,
                        c.COLUMN_DEFAULT,
                        c.ORDINAL_POSITION,
                        CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PRIMARY_KEY
                    FROM INFORMATION_SCHEMA.COLUMNS c
                    LEFT JOIN (
                        SELECT kc.COLUMN_NAME
                        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kc
                        INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc ON kc.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                        WHERE tc.TABLE_SCHEMA = @schema AND tc.TABLE_NAME = @table 
                        AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                    ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
                    WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
                    ORDER BY c.ORDINAL_POSITION";

                var command = new SqlCommand(columnQuery, connection);
                command.Parameters.AddWithValue("@schema", schemaName);
                command.Parameters.AddWithValue("@table", tableName);

                var columns = new List<string>();
                var primaryKeyColumns = new List<string>();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var columnName = reader.GetString("COLUMN_NAME");
                    var dataType = reader.GetString("DATA_TYPE");
                    var maxLength = reader.IsDBNull("CHARACTER_MAXIMUM_LENGTH") ? (int?)null : reader.GetInt32("CHARACTER_MAXIMUM_LENGTH");
                    var precision = reader.IsDBNull("NUMERIC_PRECISION") ? (byte?)null : reader.GetByte("NUMERIC_PRECISION");
                    var scale = reader.IsDBNull("NUMERIC_SCALE") ? (int?)null : reader.GetInt32("NUMERIC_SCALE");
                    var isNullable = reader.GetString("IS_NULLABLE") == "YES";
                    var defaultValue = reader.IsDBNull("COLUMN_DEFAULT") ? null : reader.GetString("COLUMN_DEFAULT");
                    var isPrimaryKey = reader.GetInt32("IS_PRIMARY_KEY") == 1;

                    // Build column definition
                    var columnDef = $"    [{columnName}] ";

                    // Add data type with proper sizing
                    switch (dataType.ToLower())
                    {
                        case "varchar":
                        case "nvarchar":
                        case "char":
                        case "nchar":
                            columnDef += maxLength == -1 ? $"{dataType}(MAX)" : $"{dataType}({maxLength})";
                            break;
                        case "decimal":
                        case "numeric":
                            columnDef += $"{dataType}({precision},{scale})";
                            break;
                        case "float":
                            columnDef += precision.HasValue ? $"{dataType}({precision})" : dataType;
                            break;
                        default:
                            columnDef += dataType;
                            break;
                    }

                    // Add nullability
                    columnDef += isNullable ? " NULL" : " NOT NULL";

                    // Add default value if exists
                    if (!string.IsNullOrEmpty(defaultValue))
                    {
                        columnDef += $" DEFAULT {defaultValue}";
                    }

                    columns.Add(columnDef);

                    if (isPrimaryKey)
                    {
                        primaryKeyColumns.Add(columnName);
                    }
                }

                // Add columns to script
                script.AppendLine(string.Join(",\n", columns));

                // Add primary key constraint if exists
                if (primaryKeyColumns.Count > 0)
                {
                    var pkName = $"PK_{tableName}";
                    var pkColumns = string.Join(", ", primaryKeyColumns.Select(col => $"[{col}]"));
                    script.AppendLine($",    CONSTRAINT [{pkName}] PRIMARY KEY ({pkColumns})");
                }

                script.AppendLine(")");

                return script.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to generate script for table {schemaName}.{tableName}: {ex.Message}");
            }
        }

        public async Task<List<TableColumn>> GetTableColumnsAsync(ConnectionSettings settings, string schemaName, string tableName)
        {
            var columns = new List<TableColumn>();

            try
            {
                using var connection = new SqlConnection(settings.GetConnectionString());
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        c.COLUMN_NAME,
                        c.DATA_TYPE,
                        c.CHARACTER_MAXIMUM_LENGTH,
                        c.NUMERIC_PRECISION,
                        c.NUMERIC_SCALE,
                        c.IS_NULLABLE,
                        c.COLUMN_DEFAULT,
                        c.ORDINAL_POSITION,
                        CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PRIMARY_KEY
                    FROM INFORMATION_SCHEMA.COLUMNS c
                    LEFT JOIN (
                        SELECT kc.COLUMN_NAME
                        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kc
                        INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc ON kc.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                        WHERE tc.TABLE_SCHEMA = @schema AND tc.TABLE_NAME = @table 
                        AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                    ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
                    WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
                    ORDER BY c.ORDINAL_POSITION";

                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@schema", schemaName);
                command.Parameters.AddWithValue("@table", tableName);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var column = new TableColumn
                    {
                        Name = reader.GetString("COLUMN_NAME"),
                        DataType = reader.GetString("DATA_TYPE"),
                        MaxLength = reader.IsDBNull("CHARACTER_MAXIMUM_LENGTH") ? (int?)null : reader.GetInt32("CHARACTER_MAXIMUM_LENGTH"),
                        Precision = reader.IsDBNull("NUMERIC_PRECISION") ? (byte?)null : reader.GetByte("NUMERIC_PRECISION"),
                        Scale = reader.IsDBNull("NUMERIC_SCALE") ? (int?)null : reader.GetInt32("NUMERIC_SCALE"),
                        IsNullable = reader.GetString("IS_NULLABLE") == "YES",
                        DefaultValue = reader.IsDBNull("COLUMN_DEFAULT") ? null : reader.GetString("COLUMN_DEFAULT"),
                        OrdinalPosition = reader.GetInt32("ORDINAL_POSITION"),
                        IsPrimaryKey = reader.GetInt32("IS_PRIMARY_KEY") == 1
                    };
                    columns.Add(column);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get columns for table {schemaName}.{tableName}: {ex.Message}");
            }

            return columns;
        }

        public async Task<string> GenerateAlterTableScriptAsync(ConnectionSettings sourceSettings, ConnectionSettings targetSettings, string schemaName, string tableName)
        {
            try
            {
                // Get source and target table structures
                var sourceColumns = await GetTableColumnsAsync(sourceSettings, schemaName, tableName);
                var targetColumns = await GetTableColumnsAsync(targetSettings, schemaName, tableName);

                var script = new StringBuilder();
                script.AppendLine($"-- ALTER TABLE script for {schemaName}.{tableName}");
                script.AppendLine();

                // Find columns to add (exist in source but not in target)
                var columnsToAdd = sourceColumns.Where(sc => !targetColumns.Any(tc => tc.Name.Equals(sc.Name, StringComparison.OrdinalIgnoreCase))).ToList();
                
                // Find columns to modify (exist in both but with different definitions)
                var columnsToModify = sourceColumns.Where(sc => targetColumns.Any(tc => 
                    tc.Name.Equals(sc.Name, StringComparison.OrdinalIgnoreCase) && 
                    !tc.HasSameDefinition(sc))).ToList();

                // Find columns to drop (exist in target but not in source)
                var columnsToDrop = targetColumns.Where(tc => !sourceColumns.Any(sc => sc.Name.Equals(tc.Name, StringComparison.OrdinalIgnoreCase))).ToList();

                bool hasChanges = columnsToAdd.Any() || columnsToModify.Any() || columnsToDrop.Any();

                if (!hasChanges)
                {
                    script.AppendLine("-- No changes needed - table structures are identical");
                    return script.ToString();
                }

                // Add new columns
                foreach (var column in columnsToAdd)
                {
                    script.AppendLine($"ALTER TABLE [{schemaName}].[{tableName}] ADD {column.GetColumnDefinition()};");
                }

                // Modify existing columns
                foreach (var column in columnsToModify)
                {
                    script.AppendLine($"ALTER TABLE [{schemaName}].[{tableName}] ALTER COLUMN {column.GetColumnDefinition()};");
                }

                // Drop columns (commented out by default for safety)
                if (columnsToDrop.Any())
                {
                    script.AppendLine();
                    script.AppendLine("-- WARNING: The following columns exist in target but not in source.");
                    script.AppendLine("-- Uncomment the lines below if you want to drop them:");
                    foreach (var column in columnsToDrop)
                    {
                        script.AppendLine($"-- ALTER TABLE [{schemaName}].[{tableName}] DROP COLUMN [{column.Name}];");
                    }
                }

                return script.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to generate ALTER script for table {schemaName}.{tableName}: {ex.Message}");
            }
        }

        public async Task CreateOrAlterTableAsync(ConnectionSettings sourceSettings, ConnectionSettings targetSettings, Table table, bool replaceIfExists = false)
        {
            using var connection = new SqlConnection(targetSettings.GetConnectionString());
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                var tableExists = await TableExistsAsync(targetSettings, table.Schema, table.Name);

                if (tableExists)
                {
                    if (replaceIfExists)
                    {
                        // Drop the existing table and recreate it
                        var dropCommand = new SqlCommand($"DROP TABLE [{table.Schema}].[{table.Name}]", connection, transaction);
                        await dropCommand.ExecuteNonQueryAsync();

                        // Create the table using the definition
                        if (!string.IsNullOrEmpty(table.Definition))
                        {
                            var command = new SqlCommand(table.Definition, connection, transaction);
                            await command.ExecuteNonQueryAsync();
                        }
                        else
                        {
                            throw new Exception($"No table definition available for {table.FullName}");
                        }
                    }
                    else
                    {
                        // Generate and execute ALTER TABLE statements
                        var alterScript = await GenerateAlterTableScriptAsync(sourceSettings, targetSettings, table.Schema, table.Name);
                        
                        // Debug: Check what the script contains
                        System.Diagnostics.Debug.WriteLine($"Generated ALTER script for {table.FullName}:");
                        System.Diagnostics.Debug.WriteLine(alterScript);
                        
                        // Execute ALTER statements directly by regenerating them individually
                        var sourceColumns = await GetTableColumnsAsync(sourceSettings, table.Schema, table.Name);
                        var targetColumns = await GetTableColumnsAsync(targetSettings, table.Schema, table.Name);

                        // Find columns to add (exist in source but not in target)
                        var columnsToAdd = sourceColumns.Where(sc => !targetColumns.Any(tc => tc.Name.Equals(sc.Name, StringComparison.OrdinalIgnoreCase))).ToList();
                        
                        // Find columns to modify (exist in both but with different definitions)
                        var columnsToModify = sourceColumns.Where(sc => targetColumns.Any(tc => 
                            tc.Name.Equals(sc.Name, StringComparison.OrdinalIgnoreCase) && 
                            !tc.HasSameDefinition(sc))).ToList();

                        // Execute ADD COLUMN statements
                        foreach (var column in columnsToAdd)
                        {
                            try
                            {
                                var addStatement = $"ALTER TABLE [{table.Schema}].[{table.Name}] ADD {column.GetColumnDefinition()}";
                                System.Diagnostics.Debug.WriteLine($"Executing ADD: {addStatement}");
                                var command = new SqlCommand(addStatement, connection, transaction);
                                await command.ExecuteNonQueryAsync();
                                System.Diagnostics.Debug.WriteLine($"Successfully added column: {column.Name}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error adding column {column.Name}: {ex.Message}");
                                throw new Exception($"Failed to add column {column.Name}: {ex.Message}");
                            }
                        }

                        // Execute ALTER COLUMN statements
                        foreach (var column in columnsToModify)
                        {
                            try
                            {
                                var alterStatement = $"ALTER TABLE [{table.Schema}].[{table.Name}] ALTER COLUMN {column.GetColumnDefinition()}";
                                System.Diagnostics.Debug.WriteLine($"Executing ALTER: {alterStatement}");
                                var command = new SqlCommand(alterStatement, connection, transaction);
                                await command.ExecuteNonQueryAsync();
                                System.Diagnostics.Debug.WriteLine($"Successfully modified column: {column.Name}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error modifying column {column.Name}: {ex.Message}");
                                throw new Exception($"Failed to modify column {column.Name}: {ex.Message}");
                            }
                        }

                        // Verify columns were actually added
                        if (columnsToAdd.Any())
                        {
                            System.Diagnostics.Debug.WriteLine($"Verifying {columnsToAdd.Count} columns were added to {table.Schema}.{table.Name}...");
                            
                            // Check columns using the same connection and transaction
                            var checkCommand = new SqlCommand(@"
                                SELECT c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE, c.COLUMN_DEFAULT, c.CHARACTER_MAXIMUM_LENGTH, c.NUMERIC_PRECISION, c.NUMERIC_SCALE
                                FROM INFORMATION_SCHEMA.COLUMNS c
                                WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
                                ORDER BY c.ORDINAL_POSITION", connection, transaction);
                            
                            checkCommand.Parameters.AddWithValue("@schema", table.Schema);
                            checkCommand.Parameters.AddWithValue("@table", table.Name);
                            
                            var updatedColumns = new List<string>();
                            using var reader = await checkCommand.ExecuteReaderAsync();
                            while (await reader.ReadAsync())
                            {
                                updatedColumns.Add(reader.GetString("COLUMN_NAME"));
                            }
                            reader.Close();
                            
                            foreach (var addedColumn in columnsToAdd)
                            {
                                var exists = updatedColumns.Any(c => c.Equals(addedColumn.Name, StringComparison.OrdinalIgnoreCase));
                                System.Diagnostics.Debug.WriteLine($"Column {addedColumn.Name} exists after ADD: {exists}");
                                if (!exists)
                                {
                                    throw new Exception($"Column {addedColumn.Name} was not found in table after ADD operation");
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Create the table using the definition
                    if (!string.IsNullOrEmpty(table.Definition))
                    {
                        var command = new SqlCommand(table.Definition, connection, transaction);
                        await command.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        throw new Exception($"No table definition available for {table.FullName}");
                    }
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private async Task<string> GetTableDefinitionAsync(ConnectionSettings settings, string schemaName, string tableName)
        {
            using var connection = new SqlConnection(settings.GetConnectionString());
            await connection.OpenAsync();

            var createTableScript = new StringBuilder();
            createTableScript.AppendLine($"CREATE TABLE [{schemaName}].[{tableName}] (");

            // Get columns
            var columnQuery = @"
                SELECT 
                    c.COLUMN_NAME,
                    c.DATA_TYPE,
                    c.CHARACTER_MAXIMUM_LENGTH,
                    c.NUMERIC_PRECISION,
                    c.NUMERIC_SCALE,
                    c.IS_NULLABLE,
                    c.COLUMN_DEFAULT,
                    COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') as IS_IDENTITY
                FROM INFORMATION_SCHEMA.COLUMNS c
                WHERE c.TABLE_SCHEMA = @schemaName AND c.TABLE_NAME = @tableName
                ORDER BY c.ORDINAL_POSITION";

            var columns = new List<string>();
            var command = new SqlCommand(columnQuery, connection);
            command.Parameters.AddWithValue("@schemaName", schemaName);
            command.Parameters.AddWithValue("@tableName", tableName);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var columnName = reader.GetString("COLUMN_NAME");
                    var dataType = reader.GetString("DATA_TYPE").ToUpper();
                    var maxLength = reader.IsDBNull("CHARACTER_MAXIMUM_LENGTH") ? (int?)null : reader.GetInt32("CHARACTER_MAXIMUM_LENGTH");
                    var precision = reader.IsDBNull("NUMERIC_PRECISION") ? (byte?)null : reader.GetByte("NUMERIC_PRECISION");
                    var scale = reader.IsDBNull("NUMERIC_SCALE") ? (int?)null : reader.GetInt32("NUMERIC_SCALE");
                    var isNullable = reader.GetString("IS_NULLABLE") == "YES";
                    var defaultValue = reader.IsDBNull("COLUMN_DEFAULT") ? null : reader.GetString("COLUMN_DEFAULT");
                    var isIdentity = reader.GetInt32("IS_IDENTITY") == 1;

                    var columnDef = new StringBuilder();
                    columnDef.Append($"    [{columnName}] {dataType}");

                    // Add length/precision/scale
                    if (dataType == "VARCHAR" || dataType == "NVARCHAR" || dataType == "CHAR" || dataType == "NCHAR")
                    {
                        if (maxLength == -1)
                            columnDef.Append("(MAX)");
                        else if (maxLength.HasValue)
                            columnDef.Append($"({maxLength})");
                    }
                    else if (dataType == "DECIMAL" || dataType == "NUMERIC")
                    {
                        if (precision.HasValue && scale.HasValue)
                            columnDef.Append($"({precision},{scale})");
                    }

                    // Add IDENTITY
                    if (isIdentity)
                        columnDef.Append(" IDENTITY(1,1)");

                    // Add NULL/NOT NULL
                    columnDef.Append(isNullable ? " NULL" : " NOT NULL");

                    // Add DEFAULT
                    if (!string.IsNullOrEmpty(defaultValue))
                        columnDef.Append($" DEFAULT {defaultValue}");

                    columns.Add(columnDef.ToString());
                }
            }

            createTableScript.AppendLine(string.Join(",\n", columns));

            // Get primary key
            var pkQuery = @"
                SELECT c.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE cu ON tc.CONSTRAINT_NAME = cu.CONSTRAINT_NAME
                JOIN INFORMATION_SCHEMA.COLUMNS c ON cu.COLUMN_NAME = c.COLUMN_NAME AND cu.TABLE_NAME = c.TABLE_NAME
                WHERE tc.TABLE_SCHEMA = @schemaName AND tc.TABLE_NAME = @tableName 
                AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                ORDER BY c.ORDINAL_POSITION";

            var pkColumns = new List<string>();
            command = new SqlCommand(pkQuery, connection);
            command.Parameters.AddWithValue("@schemaName", schemaName);
            command.Parameters.AddWithValue("@tableName", tableName);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    pkColumns.Add($"[{reader.GetString("COLUMN_NAME")}]");
                }
            }

            if (pkColumns.Count > 0)
            {
                createTableScript.AppendLine($",    CONSTRAINT [PK_{tableName}] PRIMARY KEY CLUSTERED ({string.Join(", ", pkColumns)})");
            }

            createTableScript.AppendLine(");");

            return createTableScript.ToString();
        }
    }
}

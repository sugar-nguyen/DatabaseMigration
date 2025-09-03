# Database Migration Tool

A C# WPF application for migrating stored procedures between SQL Server databases on the same server.

## Features

- **Single Server Connection**: Connect to one SQL Server instance using Windows Authentication or SQL Authentication
- **Source Database Selection**: Choose a source database to read stored procedures from
- **Multiple Target Databases**: Select multiple target databases to migrate stored procedures to
- **Stored Procedure Selection**: Browse, search, and select specific stored procedures to migrate
- **Backup Creation**: Automatically create backup copies of existing procedures with date suffix
- **Progress Tracking**: Real-time progress bar and detailed migration log
- **Connection Management**: Remembers connection settings for future use

## How to Use

### 1. Server Connection
1. Enter your SQL Server name in the Server field
2. Choose authentication method (Windows or SQL Authentication)
3. If using SQL Authentication, enter username and password
4. Click "Test" to verify connection
5. Click "Refresh Databases" to load all databases on the server

### 2. Source Database
1. Select the source database from the dropdown
2. Click "Load Stored Procedures" to view all procedures in the selected database
3. Use the search box to filter procedures by name or schema
4. Select specific procedures or use "Select All"

### 3. Target Databases
1. Select one or more target databases from the list
2. Add new database names using the text box and "Add New" button
3. New databases will be created automatically if they don't exist

### 4. Migration
1. Ensure you have selected both procedures and target databases
2. Choose whether to create backups of existing procedures (checked by default)
3. Click "Migrate" to start the process
4. Monitor progress in the migration log

**Backup Behavior:**
- When enabled and a stored procedure already exists in the target database, a backup copy is created with the naming format: `procedurename_ddMMyyyy`
- The existing procedure is then updated using `ALTER PROCEDURE` with the new code
- New procedures are always created using `CREATE PROCEDURE`
- All operations are performed within database transactions for safety

## Technical Details

- **Framework**: .NET 8.0 Windows
- **Database**: Microsoft SQL Server (via Microsoft.Data.SqlClient)
- **Settings Storage**: JSON format in user's AppData folder
- **Backup Naming**: Original procedure name + "_ddMMyyyy" suffix (e.g., `GetCustomers_19082025`)

## Migration Process

When migrating stored procedures:

1. **Database Creation**: If target database doesn't exist, it will be created
2. **Optional Backup**: If enabled and procedure exists in target database, creates backup with format `procedurename_ddMMyyyy`
3. **Smart Migration**: 
   - Uses `ALTER PROCEDURE` for existing procedures (after optional backup)
   - Uses `CREATE PROCEDURE` for new procedures
4. **Error Handling**: Continues migration even if individual procedures fail
5. **Transaction Safety**: Each procedure migration is wrapped in a database transaction
6. **Logging**: Detailed log of all operations with success/error indicators

## Connection Settings

Connection settings are automatically saved and include:
- Server name
- Authentication method
- Username (for SQL Authentication)
- Last used timestamp

Settings are stored in: `%APPDATA%\DatabaseMigrationTool\connections.json`

## Requirements

- .NET 8.0 Runtime
- Access to SQL Server instance
- Appropriate permissions to create databases and stored procedures

## Building

```bash
dotnet build
dotnet run
```

## Project Structure

```
DatabaseMigrationTool/
├── Models/
│   ├── ConnectionSettings.cs    # Database connection configuration
│   ├── StoredProcedure.cs      # Stored procedure data model
│   └── TargetDatabase.cs       # Target database selection model
├── Services/
│   ├── DatabaseService.cs      # SQL Server operations
│   └── ConnectionSettingsService.cs # Settings persistence
├── MainWindow.xaml             # Main UI layout
├── MainWindow.xaml.cs          # UI event handlers and logic
└── App.xaml                    # Application configuration
```

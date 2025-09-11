# Database Migration Tool - Portable Build

## 📦 Portable Version

This is a portable, self-contained version of the Database Migration Tool that doesn't require .NET installation on the target machine.

### 🚀 How to Build Portable Version

**Option 1: Using Versioned Build Scripts (Recommended)**
```bash
# Use version from config file (version.json)
.\build-portable-versioned.ps1

# Use custom version
.\build-portable-versioned.ps1 -CustomVersion "2.1.0"

# Interactive mode (asks for version input)
.\build-portable-versioned.ps1 -Interactive

# Or use batch file
.\build-versioned.bat          # Interactive mode
.\build-versioned.bat "1.5.0"  # Custom version
```

**Option 2: Quick Helpers**
```bash
# Load helper functions
. .\build-helpers.ps1

# Then use:
Build-Default              # Use config version
Build-Version "2.0.0"      # Custom version  
Build-Interactive          # Ask for input
```

**Option 3: Manual Command**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "./publish-portable"
```

### 📁 Output

- **File**: `DatabaseMigrationTool_v{version}.exe` (~164MB)
- **Location**: `./publish-portable/`
- **Requirements**: Windows x64 (no .NET installation needed)
- **Icon**: Custom application icon (converted from app-icon.png to app-icon.ico)
- **Versioning**: Automatic version embedding in filename

### ✨ Features

- ✅ **Self-contained**: Includes .NET 8 runtime
- ✅ **Single file**: Everything packed into one executable
- ✅ **Portable**: Copy and run anywhere on Windows x64
- ✅ **Connection history**: Automatically saves server connections
- ✅ **Auto-fill credentials**: Smart credential management
- ✅ **Cross-server migration**: Support for different target servers

### 🎯 Usage

1. Copy `DatabaseMigrationTool.exe` to any Windows machine
2. Double-click to run (no installation required)
3. Configure source and target connections
4. Select stored procedures/tables to migrate
5. Start migration process

### 📝 Notes

- First run may take a few seconds as it extracts embedded files
- Connection settings are saved locally in user profile
- Supports both Windows Authentication and SQL Server Authentication
- Backup options available for existing objects

### 🔧 Build Variants

For different platforms, use these commands:

```bash
# Windows x64 (recommended)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "./publish-win-x64"

# Windows x86
dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -o "./publish-win-x86"

# Framework-dependent (requires .NET 8 installed)
dotnet publish -c Release --self-contained false -p:PublishSingleFile=true -o "./publish-framework-dependent"
```

---
*Built with .NET 8 and WPF*

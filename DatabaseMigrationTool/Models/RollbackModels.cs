using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DatabaseMigrationTool.Models
{
    /// <summary>
    /// Represents a rollback record that tracks information needed to rollback a migration
    /// </summary>
    public class RollbackRecord : INotifyPropertyChanged
    {
        private string _id;
        private DateTime _migrationTimestamp;
        private string _sourceDatabase;
        private List<string> _targetDatabases;
        private List<BackupInfo> _backupProcedures;
        private List<BackupInfo> _backupTables;
        private bool _canRollback;
        private string _migrationSettings;

        public string Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        public DateTime MigrationTimestamp
        {
            get => _migrationTimestamp;
            set
            {
                _migrationTimestamp = value;
                OnPropertyChanged(nameof(MigrationTimestamp));
            }
        }

        public string SourceDatabase
        {
            get => _sourceDatabase;
            set
            {
                _sourceDatabase = value;
                OnPropertyChanged(nameof(SourceDatabase));
            }
        }

        public List<string> TargetDatabases
        {
            get => _targetDatabases ??= new List<string>();
            set
            {
                _targetDatabases = value;
                OnPropertyChanged(nameof(TargetDatabases));
                OnPropertyChanged(nameof(TargetDatabasesCount));
                OnPropertyChanged(nameof(TargetDatabasesDisplay));
            }
        }

        public List<BackupInfo> BackupProcedures
        {
            get => _backupProcedures ??= new List<BackupInfo>();
            set
            {
                _backupProcedures = value;
                OnPropertyChanged(nameof(BackupProcedures));
                OnPropertyChanged(nameof(BackupCount));
            }
        }

        public List<BackupInfo> BackupTables
        {
            get => _backupTables ??= new List<BackupInfo>();
            set
            {
                _backupTables = value;
                OnPropertyChanged(nameof(BackupTables));
                OnPropertyChanged(nameof(BackupCount));
            }
        }

        public bool CanRollback
        {
            get => _canRollback;
            set
            {
                _canRollback = value;
                OnPropertyChanged(nameof(CanRollback));
            }
        }

        public string MigrationSettings
        {
            get => _migrationSettings;
            set
            {
                _migrationSettings = value;
                OnPropertyChanged(nameof(MigrationSettings));
            }
        }

        // Computed properties for UI binding
        public int TargetDatabasesCount => TargetDatabases?.Count ?? 0;
        
        public int BackupCount => (BackupProcedures?.Count ?? 0) + (BackupTables?.Count ?? 0);
        
        public string TargetDatabasesDisplay => 
            TargetDatabases != null && TargetDatabases.Count > 0 
                ? string.Join(", ", TargetDatabases) 
                : "No targets";

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents backup information for a stored procedure or table
    /// </summary>
    public class BackupInfo : INotifyPropertyChanged
    {
        private string _originalName;
        private string _backupName;
        private string _targetDatabase;
        private DateTime _backupTimestamp;
        private BackupType _type;
        private string _originalDefinition;

        public string OriginalName
        {
            get => _originalName;
            set
            {
                _originalName = value;
                OnPropertyChanged(nameof(OriginalName));
            }
        }

        public string BackupName
        {
            get => _backupName;
            set
            {
                _backupName = value;
                OnPropertyChanged(nameof(BackupName));
            }
        }

        public string TargetDatabase
        {
            get => _targetDatabase;
            set
            {
                _targetDatabase = value;
                OnPropertyChanged(nameof(TargetDatabase));
            }
        }

        public DateTime BackupTimestamp
        {
            get => _backupTimestamp;
            set
            {
                _backupTimestamp = value;
                OnPropertyChanged(nameof(BackupTimestamp));
            }
        }

        public BackupType Type
        {
            get => _type;
            set
            {
                _type = value;
                OnPropertyChanged(nameof(Type));
            }
        }

        public string OriginalDefinition
        {
            get => _originalDefinition;
            set
            {
                _originalDefinition = value;
                OnPropertyChanged(nameof(OriginalDefinition));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents the type of backup (stored procedure or table)
    /// </summary>
    public enum BackupType
    {
        StoredProcedure,
        Table
    }

    /// <summary>
    /// Represents the result of a rollback operation
    /// </summary>
    public class RollbackResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int RestoredCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public TimeSpan Duration { get; set; }
    }
}
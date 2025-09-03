using System;

namespace DatabaseMigrationTool.Models
{
    public class TableColumn
    {
        public string Name { get; set; } = "";
        public string DataType { get; set; } = "";
        public int? MaxLength { get; set; }
        public byte? Precision { get; set; }
        public int? Scale { get; set; }
        public bool IsNullable { get; set; }
        public string? DefaultValue { get; set; }
        public int OrdinalPosition { get; set; }
        public bool IsPrimaryKey { get; set; }

        public string GetColumnDefinition()
        {
            var definition = $"[{Name}] ";

            // Add data type with proper sizing
            switch (DataType.ToLower())
            {
                case "varchar":
                case "nvarchar":
                case "char":
                case "nchar":
                    definition += MaxLength == -1 ? $"{DataType}(MAX)" : $"{DataType}({MaxLength})";
                    break;
                case "decimal":
                case "numeric":
                    definition += $"{DataType}({Precision},{Scale})";
                    break;
                case "float":
                    definition += Precision.HasValue ? $"{DataType}({Precision})" : DataType;
                    break;
                default:
                    definition += DataType;
                    break;
            }

            // Add nullability
            definition += IsNullable ? " NULL" : " NOT NULL";

            // Add default value if exists
            if (!string.IsNullOrEmpty(DefaultValue))
            {
                definition += $" DEFAULT {DefaultValue}";
            }

            return definition;
        }

        public bool HasSameDefinition(TableColumn other)
        {
            return DataType.Equals(other.DataType, StringComparison.OrdinalIgnoreCase) &&
                   MaxLength == other.MaxLength &&
                   Precision == other.Precision &&
                   Scale == other.Scale &&
                   IsNullable == other.IsNullable &&
                   string.Equals(DefaultValue, other.DefaultValue, StringComparison.OrdinalIgnoreCase);
        }
    }
}

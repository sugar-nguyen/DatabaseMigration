using System.ComponentModel;

namespace DatabaseMigrationTool.Models
{
    public class StoredProcedure : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Name { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public string FullName => $"{Schema}.{Name}";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return FullName;
        }
    }
}

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DatabaseMigrationTool.Models;

namespace DatabaseMigrationTool
{
    public partial class DatabaseSelectorWindow : Window
    {
        private ObservableCollection<TargetDatabase> _allDatabases;
        private ObservableCollection<TargetDatabase> _filteredDatabases;
        public ObservableCollection<TargetDatabase> SelectedDatabases { get; private set; }

        public DatabaseSelectorWindow(ObservableCollection<TargetDatabase> databases)
        {
            InitializeComponent();
            
            // Create copies of the databases
            _allDatabases = new ObservableCollection<TargetDatabase>();
            foreach (var db in databases)
            {
                _allDatabases.Add(new TargetDatabase 
                { 
                    Name = db.Name, 
                    IsSelected = db.IsSelected 
                });
            }

            // Initialize filtered databases
            _filteredDatabases = new ObservableCollection<TargetDatabase>(_allDatabases);
            
            // Set up the ListBox
            lstDatabases.ItemsSource = _filteredDatabases;
            
            SelectedDatabases = new ObservableCollection<TargetDatabase>();
            
            // Initialize selection count
            UpdateSelectionCount();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox searchBox)
            {
                var searchTerm = searchBox.Text?.ToLower() ?? string.Empty;
                
                // Clear and repopulate filtered databases
                _filteredDatabases.Clear();
                
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    // Show all databases when search is empty
                    foreach (var db in _allDatabases)
                    {
                        _filteredDatabases.Add(db);
                    }
                }
                else
                {
                    // Filter databases based on search term
                    foreach (var db in _allDatabases)
                    {
                        if (db.Name.ToLower().Contains(searchTerm))
                        {
                            _filteredDatabases.Add(db);
                        }
                    }
                }
            }
            
            // Update selection count display
            UpdateSelectionCount();
        }

        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            // Select all databases in the current filtered view
            foreach (TargetDatabase db in _filteredDatabases)
            {
                db.IsSelected = true;
            }
            
            UpdateSelectionCount();
        }

        private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            // Unselect all databases in the current filtered view
            foreach (TargetDatabase db in _filteredDatabases)
            {
                db.IsSelected = false;
            }
            
            UpdateSelectionCount();
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            // Clear all selections (both visible and hidden)
            foreach (TargetDatabase db in _allDatabases)
            {
                db.IsSelected = false;
            }
            
            // Uncheck the select all checkbox
            chkSelectAll.IsChecked = false;
            UpdateSelectionCount();
        }

        private void UpdateSelectionCount()
        {
            var selectedCount = _allDatabases.Count(db => db.IsSelected);
            txtSelectionCount.Text = $"{selectedCount} selected";
        }

        private void DatabaseCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // Update selection count when individual checkboxes are clicked
            UpdateSelectionCount();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Update selected databases based on checkbox states
            UpdateSelectedDatabases();
            DialogResult = true;
            Close();
        }

        private void UpdateSelectedDatabases()
        {
            SelectedDatabases.Clear();
            
            // Add all selected databases from the complete list
            foreach (TargetDatabase db in _allDatabases)
            {
                if (db.IsSelected)
                {
                    SelectedDatabases.Add(db);
                }
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T)
                    return (T)child;
                
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }
    }
}

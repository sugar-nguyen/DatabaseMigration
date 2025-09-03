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

            // Set up the ListBox
            lstDatabases.ItemsSource = _allDatabases;
            
            SelectedDatabases = new ObservableCollection<TargetDatabase>();
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
            
            // Get all checked databases from the ListBox
            foreach (TargetDatabase db in _allDatabases)
            {
                // Find the corresponding CheckBox in the ListBox
                var listBoxItem = lstDatabases.ItemContainerGenerator.ContainerFromItem(db) as ListBoxItem;
                if (listBoxItem != null)
                {
                    var checkBox = FindVisualChild<CheckBox>(listBoxItem);
                    if (checkBox != null && checkBox.IsChecked == true)
                    {
                        db.IsSelected = true;
                        SelectedDatabases.Add(db);
                    }
                    else
                    {
                        db.IsSelected = false;
                    }
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

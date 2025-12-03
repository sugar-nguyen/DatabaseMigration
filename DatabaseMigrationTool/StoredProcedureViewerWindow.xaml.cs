using System;
using System.IO;
using System.Windows;
using DatabaseMigrationTool.Models;
using Microsoft.Win32;
using Syncfusion.Windows.Shared;

namespace DatabaseMigrationTool
{
    public partial class StoredProcedureViewerWindow : ChromelessWindow
    {
        private readonly StoredProcedure _storedProcedure;

        public StoredProcedureViewerWindow(StoredProcedure storedProcedure)
        {
            InitializeComponent();
            _storedProcedure = storedProcedure;

            LoadProcedureDefinition();
        }

        private void LoadProcedureDefinition()
        {
            if (_storedProcedure != null)
            {
                // Set header information
                txtProcedureName.Text = _storedProcedure.FullName;
                txtProcedureInfo.Text = $"Schema: {_storedProcedure.Schema} | Procedure: {_storedProcedure.Name}";

                // Set definition
                txtDefinition.Text = _storedProcedure.Definition;

                // Update statistics
                UpdateStatistics();
            }
        }

        private void UpdateStatistics()
        {
            if (!string.IsNullOrEmpty(txtDefinition.Text))
            {
                var lines = txtDefinition.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                txtLineCount.Text = $"{lines.Length} lines";
                txtCharCount.Text = $"{txtDefinition.Text.Length:N0} chars";
            }
            else
            {
                txtLineCount.Text = "0 lines";
                txtCharCount.Text = "0 chars";
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(txtDefinition.Text))
                {
                    Clipboard.SetText(txtDefinition.Text);
                    MessageBox.Show("✅ Stored procedure definition copied to clipboard!",
                                  "Copy Successful",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("⚠️ No definition to copy.",
                                  "Copy Failed",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error copying to clipboard: {ex.Message}",
                              "Copy Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "SQL Files (*.sql)|*.sql|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    DefaultExt = ".sql",
                    FileName = $"{_storedProcedure.Schema}.{_storedProcedure.Name}.sql",
                    Title = "Export Stored Procedure Definition"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveFileDialog.FileName, txtDefinition.Text);

                    var result = MessageBox.Show(
                        $"✅ Stored procedure definition exported successfully!\n\nFile: {saveFileDialog.FileName}\n\nWould you like to open the file location?",
                        "Export Successful",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        var directoryPath = Path.GetDirectoryName(saveFileDialog.FileName);
                        if (!string.IsNullOrEmpty(directoryPath))
                        {
                            System.Diagnostics.Process.Start("explorer.exe", directoryPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error exporting file: {ex.Message}",
                              "Export Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

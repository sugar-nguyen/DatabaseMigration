using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using DatabaseMigrationTool.Models;

namespace DatabaseMigrationTool
{
    public partial class ScriptPreviewWindow : Window
    {
        public ScriptPreviewWindow()
        {
            InitializeComponent();
        }

        public void SetScripts(List<StoredProcedure> procedures, List<Table> tables, List<string> procedureScripts, List<string> tableScripts)
        {
            // Set stored procedure scripts
            var spScriptBuilder = new StringBuilder();
            spScriptBuilder.AppendLine("-- ================================================");
            spScriptBuilder.AppendLine("-- STORED PROCEDURE MIGRATION SCRIPTS");
            spScriptBuilder.AppendLine("-- ================================================");
            spScriptBuilder.AppendLine();

            if (procedures.Count == 0)
            {
                spScriptBuilder.AppendLine("-- No stored procedures selected for migration");
            }
            else
            {
                for (int i = 0; i < procedures.Count && i < procedureScripts.Count; i++)
                {
                    var procedure = procedures[i];
                    var script = procedureScripts[i];

                    spScriptBuilder.AppendLine($"-- ================================================");
                    spScriptBuilder.AppendLine($"-- Stored Procedure: {procedure.FullName}");
                    spScriptBuilder.AppendLine($"-- ================================================");
                    spScriptBuilder.AppendLine();
                    spScriptBuilder.AppendLine(script);
                    spScriptBuilder.AppendLine();
                    spScriptBuilder.AppendLine("GO");
                    spScriptBuilder.AppendLine();
                }
            }

            txtStoredProcedureScripts.Text = spScriptBuilder.ToString();

            // Set table scripts
            var tableScriptBuilder = new StringBuilder();
            tableScriptBuilder.AppendLine("-- ================================================");
            tableScriptBuilder.AppendLine("-- TABLE CREATION SCRIPTS");
            tableScriptBuilder.AppendLine("-- ================================================");
            tableScriptBuilder.AppendLine();

            if (tables.Count == 0)
            {
                tableScriptBuilder.AppendLine("-- No tables selected for migration");
            }
            else
            {
                for (int i = 0; i < tables.Count && i < tableScripts.Count; i++)
                {
                    var table = tables[i];
                    var script = tableScripts[i];

                    tableScriptBuilder.AppendLine($"-- ================================================");
                    tableScriptBuilder.AppendLine($"-- Table: {table.FullName}");
                    tableScriptBuilder.AppendLine($"-- ================================================");
                    tableScriptBuilder.AppendLine();
                    tableScriptBuilder.AppendLine(script);
                    tableScriptBuilder.AppendLine();
                    tableScriptBuilder.AppendLine("GO");
                    tableScriptBuilder.AppendLine();
                }
            }

            txtTableScripts.Text = tableScriptBuilder.ToString();

            // Select appropriate tab based on what has content
            if (procedures.Count > 0)
            {
                tabStoredProcedures.IsSelected = true;
            }
            else if (tables.Count > 0)
            {
                tabTables.IsSelected = true;
            }
        }

        private void CopyScripts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var allScripts = new StringBuilder();
                
                // Add stored procedure scripts
                if (!string.IsNullOrWhiteSpace(txtStoredProcedureScripts.Text))
                {
                    allScripts.AppendLine(txtStoredProcedureScripts.Text);
                    allScripts.AppendLine();
                }

                // Add table scripts
                if (!string.IsNullOrWhiteSpace(txtTableScripts.Text))
                {
                    allScripts.AppendLine(txtTableScripts.Text);
                }

                if (allScripts.Length > 0)
                {
                    Clipboard.SetText(allScripts.ToString());
                    MessageBox.Show("All scripts have been copied to clipboard!", "Scripts Copied", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("No scripts to copy.", "No Scripts", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy scripts to clipboard: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

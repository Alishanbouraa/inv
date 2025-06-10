using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows;
using System.Threading.Tasks;
using System;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class DrawerViewModel
    {
        private async Task PrintReportAsync()
        {
            try
            {
                IsProcessing = true;
                if (CurrentDrawer == null) return;

                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var document = CreateDrawerReport();
                    printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Drawer Report");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync($"Error printing report: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private FlowDocument CreateDrawerReport(bool includeTransactions = true, bool includeFinancial = true, bool cashierCopy = false)
        {
            // Your existing report creation code - modify to use the parameters
            var document = new FlowDocument();

            // Add report content (header, summary, transactions, etc.)
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Bold(new Run("Drawer Report\n")) { FontSize = 18 });
            paragraph.Inlines.Add(new Run($"Generated: {DateTime.Now:g}\n\n"));

            if (cashierCopy)
            {
                paragraph.Inlines.Add(new Bold(new Run("CASHIER COPY\n")) { FontSize = 14 });
            }

            if (CurrentDrawer != null && includeFinancial)
            {
                paragraph.Inlines.Add(new Bold(new Run("Summary:\n")));
                paragraph.Inlines.Add(new Run($"Opening Balance: {CurrentDrawer.OpeningBalance:C2}\n"));
                paragraph.Inlines.Add(new Run($"Current Balance: {CurrentDrawer.CurrentBalance:C2}\n"));
                paragraph.Inlines.Add(new Run($"Cash In: {CurrentDrawer.CashIn:C2}\n"));
                paragraph.Inlines.Add(new Run($"Cash Out: {CurrentDrawer.CashOut:C2}\n"));
                paragraph.Inlines.Add(new Run($"Difference: {CurrentDrawer.Difference:C2}\n\n"));
            }

            document.Blocks.Add(paragraph);

            // Add transaction history if requested
            if (includeTransactions && DrawerHistory.Any())
            {
                var table = new Table();

                // Create table columns
                var columns = new[] { 150.0, 100.0, 100.0, 100.0, 100.0 };
                foreach (var width in columns)
                {
                    table.Columns.Add(new TableColumn { Width = new GridLength(width) });
                }

                // Create header row
                var headerRow = new TableRow();
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Timestamp"))));
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Type"))));
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Amount"))));
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Balance"))));
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Notes"))));
                table.RowGroups.Add(new TableRowGroup());
                table.RowGroups[0].Rows.Add(headerRow);

                // Add transaction rows
                foreach (var transaction in DrawerHistory)
                {
                    var row = new TableRow();
                    row.Cells.Add(new TableCell(new Paragraph(new Run(transaction.Timestamp.ToString("g")))));
                    row.Cells.Add(new TableCell(new Paragraph(new Run(transaction.Type))));
                    row.Cells.Add(new TableCell(new Paragraph(new Run(transaction.Amount.ToString("C2")))));
                    row.Cells.Add(new TableCell(new Paragraph(new Run(transaction.Balance.ToString("C2")))));
                    row.Cells.Add(new TableCell(new Paragraph(new Run(transaction.Notes ?? string.Empty))));
                    table.RowGroups[0].Rows.Add(row);
                }

                document.Blocks.Add(table);
            }

            return document;
        }
    }
}
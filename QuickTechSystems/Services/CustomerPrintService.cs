using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Printing;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Services.Interfaces;

namespace QuickTechSystems.WPF.Services
{
    public class CustomerPrintService : ICustomerPrintService
    {
        private readonly Dictionary<string, TableColumnConfiguration> _tableColumns;
        private readonly Dictionary<string, DocumentStyleConfiguration> _documentStyles;

        public CustomerPrintService()
        {
            _tableColumns = new Dictionary<string, TableColumnConfiguration>
            {
                ["Date"] = new TableColumnConfiguration(120, "Date", TextAlignment.Left),
                ["TransactionId"] = new TableColumnConfiguration(100, "Trx #", TextAlignment.Left),
                ["Amount"] = new TableColumnConfiguration(100, "Paid Amount", TextAlignment.Right)
            };

            _documentStyles = new Dictionary<string, DocumentStyleConfiguration>
            {
                ["HeaderTitle"] = new DocumentStyleConfiguration(18, FontWeights.Bold, TextAlignment.Center, new Thickness(0, 0, 0, 10)),
                ["HeaderBalance"] = new DocumentStyleConfiguration(14, FontWeights.SemiBold, TextAlignment.Center, new Thickness(0, 0, 0, 20)),
                ["DateRange"] = new DocumentStyleConfiguration(12, FontWeights.Normal, TextAlignment.Center, new Thickness(0, 0, 0, 20)),
                ["Summary"] = new DocumentStyleConfiguration(14, FontWeights.Bold, TextAlignment.Right, new Thickness(0, 20, 0, 0)),
                ["Footer"] = new DocumentStyleConfiguration(10, FontWeights.Normal, TextAlignment.Right, new Thickness(0, 40, 0, 0))
            };
        }

        public async Task<bool> PrintPaymentHistoryAsync(CustomerDTO customer,
            IEnumerable<TransactionDTO> paymentHistory,
            bool useDateFilter,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            if (customer == null || paymentHistory == null)
                return false;

            try
            {
                return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var printDialog = new PrintDialog();
                    if (printDialog.ShowDialog() != true)
                        return false;

                    var document = CreatePrintDocument(customer, paymentHistory, useDateFilter, startDate, endDate);
                    printDialog.PrintDocument((document as IDocumentPaginatorSource).DocumentPaginator, "Payment History");
                    return true;
                });
            }
            catch
            {
                return false;
            }
        }

        private FlowDocument CreatePrintDocument(CustomerDTO customer,
            IEnumerable<TransactionDTO> paymentHistory,
            bool useDateFilter,
            DateTime? startDate,
            DateTime? endDate)
        {
            var document = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                PagePadding = new Thickness(50)
            };

            var headerBlocks = CreateDocumentHeaders(customer, useDateFilter, startDate, endDate);
            var footerBlocks = CreateDocumentFooters(paymentHistory);

            foreach (var block in headerBlocks)
                document.Blocks.Add(block);

            document.Blocks.Add(CreatePaymentTable(paymentHistory));

            foreach (var block in footerBlocks)
                document.Blocks.Add(block);

            return document;
        }

        private List<Block> CreateDocumentHeaders(CustomerDTO customer, bool useDateFilter, DateTime? startDate, DateTime? endDate)
        {
            var blocks = new List<Block>();
            var headerData = new Dictionary<string, string>
            {
                ["HeaderTitle"] = $"Payment History for {customer.Name}",
                ["HeaderBalance"] = $"Current Balance: {customer.Balance:C2}"
            };

            foreach (var kvp in headerData)
            {
                var style = _documentStyles[kvp.Key];
                blocks.Add(CreateStyledParagraph(kvp.Value, style));
            }

            if (useDateFilter && startDate.HasValue && endDate.HasValue)
            {
                var dateRangeStyle = _documentStyles["DateRange"];
                var dateText = $"Period: {startDate.Value:MM/dd/yyyy} - {endDate.Value:MM/dd/yyyy}";
                blocks.Add(CreateStyledParagraph(dateText, dateRangeStyle));
            }

            return blocks;
        }

        private List<Block> CreateDocumentFooters(IEnumerable<TransactionDTO> paymentHistory)
        {
            var blocks = new List<Block>();
            var paymentList = paymentHistory.ToList();
            var summaryText = paymentList.Count == 0 ? "No transactions found" : $"Total Paid: {paymentList.Sum(t => t.PaidAmount):C2}";

            var footerData = new Dictionary<string, string>
            {
                ["Summary"] = summaryText,
                ["Footer"] = $"Printed on {DateTime.Now:MM/dd/yyyy HH:mm:ss}"
            };

            foreach (var kvp in footerData)
            {
                var style = _documentStyles[kvp.Key];
                blocks.Add(CreateStyledParagraph(kvp.Value, style));
            }

            return blocks;
        }

        private Paragraph CreateStyledParagraph(string text, DocumentStyleConfiguration style)
        {
            return new Paragraph(new Run(text))
            {
                FontSize = style.FontSize,
                FontWeight = style.FontWeight,
                TextAlignment = style.TextAlignment,
                Margin = style.Margin
            };
        }

        private Table CreatePaymentTable(IEnumerable<TransactionDTO> paymentHistory)
        {
            var table = new Table
            {
                CellSpacing = 0,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1)
            };

            SetupTableColumns(table);
            AddTableHeaders(table);
            AddTableData(table, paymentHistory);

            return table;
        }

        private void SetupTableColumns(Table table)
        {
            foreach (var column in _tableColumns.Values)
            {
                table.Columns.Add(new TableColumn { Width = new GridLength(column.Width) });
            }
        }

        private void AddTableHeaders(Table table)
        {
            var headerRowGroup = new TableRowGroup();
            var headerRow = new TableRow { Background = new SolidColorBrush(Colors.LightGray) };

            foreach (var column in _tableColumns.Values)
                headerRow.Cells.Add(CreateTableCell(column.Header, true, column.Alignment));

            headerRowGroup.Rows.Add(headerRow);
            table.RowGroups.Add(headerRowGroup);
        }

        private void AddTableData(Table table, IEnumerable<TransactionDTO> paymentHistory)
        {
            var dataRowGroup = new TableRowGroup();
            var transactionList = paymentHistory.ToList();

            foreach (var transaction in transactionList)
            {
                var row = new TableRow();
                var cellData = new Dictionary<string, CellConfiguration>
                {
                    ["Date"] = new CellConfiguration(transaction.TransactionDate.ToString("MM/dd/yyyy HH:mm"), TextAlignment.Left),
                    ["TransactionId"] = new CellConfiguration(transaction.TransactionId.ToString(), TextAlignment.Left),
                    ["Amount"] = new CellConfiguration(transaction.PaidAmount.ToString("C2"), TextAlignment.Right)
                };

                foreach (var cell in cellData.Values)
                    row.Cells.Add(CreateTableCell(cell.Text, false, cell.Alignment));

                dataRowGroup.Rows.Add(row);
            }

            table.RowGroups.Add(dataRowGroup);
        }

        private TableCell CreateTableCell(string text, bool isHeader = false, TextAlignment alignment = TextAlignment.Left)
        {
            var cell = new TableCell
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5)
            };

            var paragraph = new Paragraph(new Run(text)) { TextAlignment = alignment };
            if (isHeader)
                paragraph.FontWeight = FontWeights.Bold;

            cell.Blocks.Add(paragraph);
            return cell;
        }

        private readonly struct TableColumnConfiguration
        {
            public double Width { get; }
            public string Header { get; }
            public TextAlignment Alignment { get; }

            public TableColumnConfiguration(double width, string header, TextAlignment alignment)
            {
                Width = width;
                Header = header;
                Alignment = alignment;
            }
        }

        private readonly struct DocumentStyleConfiguration
        {
            public int FontSize { get; }
            public FontWeight FontWeight { get; }
            public TextAlignment TextAlignment { get; }
            public Thickness Margin { get; }

            public DocumentStyleConfiguration(int fontSize, FontWeight fontWeight, TextAlignment textAlignment, Thickness margin)
            {
                FontSize = fontSize;
                FontWeight = fontWeight;
                TextAlignment = textAlignment;
                Margin = margin;
            }
        }

        private readonly struct CellConfiguration
        {
            public string Text { get; }
            public TextAlignment Alignment { get; }

            public CellConfiguration(string text, TextAlignment alignment)
            {
                Text = text;
                Alignment = alignment;
            }
        }
    }
}
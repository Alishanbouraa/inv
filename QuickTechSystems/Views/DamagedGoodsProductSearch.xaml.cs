// Path: QuickTechSystems.WPF.Views/DamagedGoodsProductSearch.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class DamagedGoodsProductSearch : UserControl
    {
        public DamagedGoodsProductSearch()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is DamagedGoodsViewModel viewModel)
            {
                viewModel.IsProductSearchPopupOpen = false;
            }
        }

        private void ProductsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView &&
                listView.SelectedItem is ProductDTO product &&
                DataContext is DamagedGoodsViewModel viewModel)
            {
                viewModel.SelectProductCommand.Execute(product);
            }
        }

        private void SelectProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.DataContext is ProductDTO product &&
                DataContext is DamagedGoodsViewModel viewModel)
            {
                viewModel.SelectProductCommand.Execute(product);
            }
        }
    }
}
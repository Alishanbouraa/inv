// Path: QuickTechSystems.WPF.Views/DamagedGoodsView.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class DamagedGoodsView : UserControl
    {
        public DamagedGoodsView()
        {
            InitializeComponent();
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid grid &&
                grid.SelectedItem is DamagedGoodsDTO damagedItem &&
                DataContext is DamagedGoodsViewModel viewModel)
            {
                viewModel.EditDamagedItemCommand.Execute(damagedItem);
            }
        }
    }
}
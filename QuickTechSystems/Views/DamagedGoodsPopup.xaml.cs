// Path: QuickTechSystems.WPF.Views/DamagedGoodsPopup.xaml.cs
using System.Windows;
using System.Windows.Controls;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class DamagedGoodsPopup : UserControl
    {
        public DamagedGoodsPopup()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is DamagedGoodsViewModel viewModel)
            {
                viewModel.IsDamagedItemPopupOpen = false;
            }
        }

        private void SearchProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is DamagedGoodsViewModel viewModel)
            {
                viewModel.OpenSearchProductCommand.Execute(null);
            }
        }
    }
}
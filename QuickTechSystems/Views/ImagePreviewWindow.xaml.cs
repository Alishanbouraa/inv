// Path: QuickTechSystems.WPF.Views/ImagePreviewWindow.xaml.cs
using System.Windows;
using System.Windows.Media.Imaging;

namespace QuickTechSystems.WPF.Views
{
    public partial class ImagePreviewWindow : Window
    {
        public ImagePreviewWindow(BitmapImage image, string title = "Image Preview")
        {
            InitializeComponent();

            this.Title = title;
            this.PreviewImage.Source = image;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
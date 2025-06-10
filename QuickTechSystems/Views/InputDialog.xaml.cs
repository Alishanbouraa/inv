using System.Windows;
using System.Windows.Threading;
namespace QuickTechSystems.WPF.Views
{
    public partial class InputDialog : Window
    {
        public new string Title { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string Input { get; set; } = string.Empty;

        public InputDialog(string title, string prompt)
        {
            InitializeComponent();
            Title = title;
            Prompt = prompt;
            DataContext = this;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
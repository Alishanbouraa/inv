using System.Windows;

using System.Windows.Controls;



namespace QuickTechSystems.WPF.Views

{

    public partial class TransactionPopup : UserControl

    {

        public event RoutedEventHandler CloseRequested;

        public event RoutedEventHandler SaveCompleted;



        public TransactionPopup()

        {

            InitializeComponent();

        }



        private void CloseButton_Click(object sender, RoutedEventArgs e)

        {

            CloseRequested?.Invoke(this, new RoutedEventArgs());

        }



        private void CancelButton_Click(object sender, RoutedEventArgs e)

        {

            CloseRequested?.Invoke(this, new RoutedEventArgs());

        }



        private void SaveButton_Click(object sender, RoutedEventArgs e)

        {

            // This will be called after the Save command is executed

            SaveCompleted?.Invoke(this, new RoutedEventArgs());

        }

    }

}
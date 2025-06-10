using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace QuickTechSystems.WPF.ViewModels
{
    public static class WindowManager
    {
        private static Window MainWindow => System.Windows.Application.Current.MainWindow;

        private static Dispatcher AppDispatcher => System.Windows.Application.Current.Dispatcher;

        public static Task<T> InvokeAsync<T>(Func<T> action)
        {
            return AppDispatcher.InvokeAsync(action).Task;
        }

        public static Task InvokeAsync(Action action)
        {
            return AppDispatcher.InvokeAsync(action).Task;
        }

        public static void ShowMessage(string message, string title = "Message", MessageBoxImage icon = MessageBoxImage.Information)
        {
            MessageBox.Show(MainWindow, message, title, MessageBoxButton.OK, icon);
        }

        public static bool ShowQuestion(string message, string title = "Question")
        {
            return MessageBox.Show(MainWindow, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public static void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(MainWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static void ShowWarning(string message, string title = "Warning")
        {
            MessageBox.Show(MainWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
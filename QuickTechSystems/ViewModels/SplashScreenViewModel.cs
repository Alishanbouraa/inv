// QuickTechSystems.WPF/ViewModels/SplashScreenViewModel.cs
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace QuickTechSystems.WPF.ViewModels
{
    public class SplashScreenViewModel : INotifyPropertyChanged
    {
        private string _statusMessage = "Loading application...";

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void UpdateStatus(string message)
        {
            StatusMessage = message;
        }
    }
}
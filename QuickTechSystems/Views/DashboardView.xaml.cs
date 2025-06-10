using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Diagnostics;

namespace QuickTechSystems.WPF.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly DispatcherTimer _timer;

        public DashboardView()
        {
            Debug.WriteLine("DashboardView: Constructor called");
            InitializeComponent();
            Debug.WriteLine("DashboardView: InitializeComponent completed");

            // Initialize and configure timer
            Debug.WriteLine("DashboardView: Initializing timer");
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            Debug.WriteLine("DashboardView: Timer initialized and started");

            // Initial time update
            Debug.WriteLine("DashboardView: Performing initial date/time update");
            UpdateDateTime();

            // Register for cleanup
            Debug.WriteLine("DashboardView: Registering event handlers");
            this.Unloaded += UserControl_Unloaded;
            this.Loaded += UserControl_Loaded;
            this.SizeChanged += OnControlSizeChanged;
            Debug.WriteLine("DashboardView: Constructor completed");
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // Debug.WriteLine("DashboardView: Timer tick"); // Commented to avoid log spam
            UpdateDateTime();
        }

        private void UpdateDateTime()
        {
            try
            {
                var now = DateTime.Now;
                DateDisplay.Text = now.ToString("dddd, MMMM d, yyyy");
                TimeDisplay.Text = now.ToString("HH:mm:ss");
                // Debug.WriteLine($"DashboardView: DateTime updated to {now}"); // Commented to avoid log spam
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DashboardView: Error updating date/time: {ex.Message}");
                Debug.WriteLine($"DashboardView: Exception details: {ex}");
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("DashboardView: Loaded event triggered");
            if (!_timer.IsEnabled)
            {
                Debug.WriteLine("DashboardView: Timer was stopped, restarting");
                _timer.Start();
            }
            else
            {
                Debug.WriteLine("DashboardView: Timer already running");
            }

            AdjustLayoutForSize();
            Debug.WriteLine("DashboardView: Layout adjusted for current size");
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("DashboardView: Unloaded event triggered");
            if (_timer.IsEnabled)
            {
                Debug.WriteLine("DashboardView: Stopping timer");
                _timer.Stop();
            }
            Debug.WriteLine("DashboardView: Unsubscribing from timer tick event");
            _timer.Tick -= Timer_Tick;
            Debug.WriteLine("DashboardView: Control unloaded successfully");
        }

        private void OnControlSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Debug.WriteLine($"DashboardView: Size changed - Old: {e.PreviousSize}, New: {e.NewSize}");
            AdjustLayoutForSize();
        }

        private void AdjustLayoutForSize()
        {
            var parentWindow = Window.GetWindow(this);
            if (parentWindow == null)
            {
                Debug.WriteLine("DashboardView: Cannot adjust layout - parent window is null");
                return;
            }

            // Get actual window dimensions
            double windowWidth = parentWindow.ActualWidth;
            Debug.WriteLine($"DashboardView: Window width detected: {windowWidth}px");

            if (windowWidth >= 1920) // Large screens
            {
                Debug.WriteLine("DashboardView: Applying large screen layout (1920+px)");
                RootGrid.Margin = new Thickness(24);
            }
            else if (windowWidth >= 1366) // Medium screens
            {
                Debug.WriteLine("DashboardView: Applying medium screen layout (1366-1919px)");
                RootGrid.Margin = new Thickness(16);
            }
            else if (windowWidth >= 800) // Small screens
            {
                Debug.WriteLine("DashboardView: Applying small screen layout (800-1365px)");
                RootGrid.Margin = new Thickness(8);
            }
            else // Very small screens
            {
                Debug.WriteLine("DashboardView: Applying very small screen layout (<800px)");
                RootGrid.Margin = new Thickness(4);
            }
            Debug.WriteLine($"DashboardView: Applied margin: {RootGrid.Margin}");
        }
    }
}
using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using QuickTechSystems.WPF.ViewModels;
using System.Windows.Controls;
using System.Linq;
using System.Windows.Input;

namespace QuickTechSystems.WPF
{
    public partial class MainWindow : Window
    {
        private const double EXPANDED_NAV_WIDTH = 280;
        private const double COLLAPSED_NAV_WIDTH = 0;
        private const double ANIMATION_DURATION = 0.3;
        private bool isSidebarVisible = true;
        private DateTime lastButtonClickTime = DateTime.MinValue;
        private const int BUTTON_COOLDOWN_MS = 300;
        private Grid? mainGrid;
        private Button? showSidebarButton;
        private Button? hideSidebarButton;
        private Border? sidebarHoverArea;
        private Border? navigationPanel;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = ((App)System.Windows.Application.Current).ServiceProvider.GetRequiredService<MainViewModel>();
            InitializeControls();
            SetupWindow();
        }

        private void InitializeControls()
        {
            mainGrid = this.FindName("MainGrid") as Grid;
            showSidebarButton = this.FindName("ShowSidebarButton") as Button;
            hideSidebarButton = this.FindName("HideSidebarButton") as Button;
            sidebarHoverArea = this.FindName("SidebarHoverArea") as Border;
            navigationPanel = this.FindName("NavigationPanel") as Border;

            if (mainGrid == null || showSidebarButton == null || hideSidebarButton == null ||
                sidebarHoverArea == null || navigationPanel == null)
            {
                throw new Exception("Critical UI elements not found");
            }
        }

        private void SetupWindow()
        {
            // Set window size based on screen resolution
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            // Adjust for different screen sizes
            if (screenWidth >= 1920) // Large screens
            {
                Width = 1400;
                Height = 900;
            }
            else if (screenWidth >= 1366) // Medium screens
            {
                Width = 1200;
                Height = 800;
            }
            else // Small screens
            {
                Width = 1000;
                Height = 700;
            }

            // If the screen is small, adjust window to fit screen
            if (SystemParameters.PrimaryScreenHeight < 800)
            {
                Height = SystemParameters.PrimaryScreenHeight * 0.9;
                Width = SystemParameters.PrimaryScreenWidth * 0.9;
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (mainGrid?.ColumnDefinitions.Count > 0)
            {
                var navColumn = mainGrid.ColumnDefinitions[0];
                navColumn.Width = new GridLength(EXPANDED_NAV_WIDTH);
                UpdateLayout();
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (mainGrid != null)
            {
                mainGrid.Margin = WindowState == WindowState.Maximized ?
                    new Thickness(8) : new Thickness(0);
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (WindowState != WindowState.Maximized)
            {
                if (ActualWidth < MinWidth) Width = MinWidth;
                if (ActualHeight < MinHeight) Height = MinHeight;
            }
        }

        private void HideSidebarButton_Click(object sender, RoutedEventArgs e)
        {
            if ((DateTime.Now - lastButtonClickTime).TotalMilliseconds < BUTTON_COOLDOWN_MS)
                return;

            lastButtonClickTime = DateTime.Now;
            HideSidebar();
        }

        private void ShowSidebarButton_Click(object sender, RoutedEventArgs e)
        {
            if ((DateTime.Now - lastButtonClickTime).TotalMilliseconds < BUTTON_COOLDOWN_MS)
                return;

            lastButtonClickTime = DateTime.Now;
            ShowSidebar();
        }

        private void SidebarHoverArea_MouseEnter(object sender, MouseEventArgs e)
        {
            // Only show the button when hovering over the area
            if (!isSidebarVisible)
            {
                showSidebarButton.Visibility = Visibility.Visible;
            }
        }

        private void MainGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Hide show button when clicking elsewhere (if it's visible)
            if (showSidebarButton.Visibility == Visibility.Visible &&
                !IsMouseOverElement(showSidebarButton, e.GetPosition(this)))
            {
                showSidebarButton.Visibility = Visibility.Collapsed;
            }

            // Only process if sidebar is visible and we're not clicking inside the sidebar
            if (isSidebarVisible && navigationPanel != null)
            {
                Point clickPoint = e.GetPosition(this);

                // Get sidebar bounds
                Rect sidebarBounds = new Rect(
                    navigationPanel.TranslatePoint(new Point(0, 0), this),
                    new Size(navigationPanel.ActualWidth, navigationPanel.ActualHeight));

                // If clicking outside the sidebar, hide it
                if (!sidebarBounds.Contains(clickPoint))
                {
                    HideSidebar();
                }
            }
        }

        private bool IsMouseOverElement(UIElement element, Point mousePosition)
        {
            if (element == null) return false;

            // Get element bounds
            Point elementPosition = element.TranslatePoint(new Point(0, 0), this);
            Rect elementBounds = new Rect(
                elementPosition,
                new Size(element.RenderSize.Width, element.RenderSize.Height));

            return elementBounds.Contains(mousePosition);
        }

        private void HideSidebar()
        {
            try
            {
                if (mainGrid?.ColumnDefinitions.Count == 0) return;

                var navColumn = mainGrid.ColumnDefinitions[0];

                // Hiding sidebar
                var hideAnimation = new GridLengthAnimation
                {
                    From = navColumn.Width,
                    To = new GridLength(COLLAPSED_NAV_WIDTH),
                    Duration = TimeSpan.FromSeconds(ANIMATION_DURATION)
                };

                hideAnimation.Completed += (s, e) =>
                {
                    navigationPanel.Visibility = Visibility.Collapsed;
                    // Make the hover area visible in the content area
                    sidebarHoverArea.Visibility = Visibility.Visible;
                    // Hide the show button initially - will appear on hover
                    showSidebarButton.Visibility = Visibility.Collapsed;
                };

                navColumn.BeginAnimation(ColumnDefinition.WidthProperty, hideAnimation);
                isSidebarVisible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error hiding sidebar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowSidebar()
        {
            try
            {
                if (mainGrid?.ColumnDefinitions.Count == 0) return;

                var navColumn = mainGrid.ColumnDefinitions[0];

                // Showing sidebar
                navigationPanel.Visibility = Visibility.Visible;

                var showAnimation = new GridLengthAnimation
                {
                    From = navColumn.Width,
                    To = new GridLength(EXPANDED_NAV_WIDTH),
                    Duration = TimeSpan.FromSeconds(ANIMATION_DURATION)
                };

                showAnimation.Completed += (s, e) =>
                {
                    sidebarHoverArea.Visibility = Visibility.Collapsed;
                    showSidebarButton.Visibility = Visibility.Collapsed;
                };

                navColumn.BeginAnimation(ColumnDefinition.WidthProperty, showAnimation);
                isSidebarVisible = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing sidebar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }

    public class GridLengthAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(GridLength);

        protected override Freezable CreateInstanceCore()
        {
            return new GridLengthAnimation();
        }

        public GridLength From
        {
            get => (GridLength)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }

        public GridLength To
        {
            get => (GridLength)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register("From", typeof(GridLength), typeof(GridLengthAnimation));

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register("To", typeof(GridLength), typeof(GridLengthAnimation));

        public override object GetCurrentValue(object defaultOriginValue,
            object defaultDestinationValue,
            AnimationClock animationClock)
        {
            if (animationClock.CurrentProgress == null)
                return new GridLength(0);

            var fromValue = From.Value;
            var toValue = To.Value;
            var progress = animationClock.CurrentProgress.Value;

            var currentValue = fromValue + ((toValue - fromValue) * progress);
            return new GridLength(currentValue);
        }
    }
}
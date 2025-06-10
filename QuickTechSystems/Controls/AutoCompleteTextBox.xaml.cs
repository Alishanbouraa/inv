using System.Collections;
using System.Windows.Controls;

namespace QuickTechSystems.Controls
{
    public partial class AutoCompleteTextBox : UserControl
    {
        private bool _suppressTextChanged;

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                "ItemsSource",
                typeof(IEnumerable),
                typeof(AutoCompleteTextBox));

        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.Register(
                "SearchText",
                typeof(string),
                typeof(AutoCompleteTextBox),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(
                "SelectedItem",
                typeof(object),
                typeof(AutoCompleteTextBox),
                new PropertyMetadata(null));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public AutoCompleteTextBox()
        {
            InitializeComponent();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextChanged) return;

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                PART_Popup.IsOpen = false;
                return;
            }

            PART_ListBox.ItemsSource = ItemsSource;
            PART_Popup.IsOpen = true;
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PART_ListBox.SelectedItem == null) return;

            _suppressTextChanged = true;
            SelectedItem = PART_ListBox.SelectedItem;
            SearchText = PART_ListBox.SelectedItem.ToString();
            PART_Popup.IsOpen = false;
            _suppressTextChanged = false;
        }
    }
}
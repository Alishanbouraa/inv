// New file: QuickTechSystems.WPF.Utilities.UIHelpers.cs
using System.Windows.Media;

public static class UIHelpers
{
    public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;

        T childElement = null;
        int childrenCount = VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T t)
            {
                childElement = t;
                break;
            }
            else
            {
                childElement = FindVisualChild<T>(child);
                if (childElement != null)
                    break;
            }
        }

        return childElement;
    }

    public static Button AddNumericButton(Grid grid, string text, int row, int col, Action<string> onClickAction)
    {
        var button = new Button
        {
            Content = text,
            FontSize = 20,
            Margin = new Thickness(3)
        };
        Grid.SetRow(button, row);
        Grid.SetColumn(button, col);
        grid.Children.Add(button);

        if (text != "C")
        {
            button.Click += (s, e) => onClickAction(text);
        }

        return button;
    }
}
using System.Windows;

namespace WPT.Wpf.Controls;

public static class HintProperties
{
    public static readonly DependencyProperty HintProperty = DependencyProperty.RegisterAttached(
        "Hint",
        typeof(string),
        typeof(HintProperties),
        new PropertyMetadata(string.Empty));

    public static string GetHint(DependencyObject obj) => (string)obj.GetValue(HintProperty);

    public static void SetHint(DependencyObject obj, string value) => obj.SetValue(HintProperty, value);
}

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using Type4Me.ViewModels;
using Type4Me.Views.Converters;

namespace Type4Me.Views.Debug;

/// <summary>
/// Real-time debug console window showing all pipeline events.
/// </summary>
public partial class DebugLogWindow : Window
{
    public DebugLogWindow()
    {
        // Add NullToVisibility converter before InitializeComponent
        Resources["NullToVis"] = new NullToVisibilityConverter();
        InitializeComponent();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // Wire up auto-scroll
        if (DataContext is DebugLogViewModel vm)
        {
            ((INotifyCollectionChanged)vm.Entries).CollectionChanged += (_, _) =>
            {
                if (AutoScrollCheck.IsChecked == true && LogListView.Items.Count > 0)
                {
                    LogListView.ScrollIntoView(LogListView.Items[^1]);
                }
            };
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DebugLogViewModel vm)
            vm.Clear();
    }
}

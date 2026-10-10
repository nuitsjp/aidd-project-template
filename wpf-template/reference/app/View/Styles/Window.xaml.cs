using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace WpfNotesSample.View.Styles;

// 独自のタイトルバーに置いたウィンドウ操作ボタンの処理。
[SuppressMessage("Minor Code Smell", "S2325", Justification = "XAML から結び付けるイベントハンドラーはインスタンスメソッドが必要。")]
internal sealed partial class WindowStyles : ResourceDictionary
{
    internal WindowStyles() => InitializeComponent();

    private void OnMinimize(object sender, RoutedEventArgs args) => SystemCommands.MinimizeWindow(OwnerOf(sender));

    private void OnMaximizeRestore(object sender, RoutedEventArgs args)
    {
        var window = OwnerOf(sender);
        if (window.WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(window);
        }
        else
        {
            SystemCommands.MaximizeWindow(window);
        }
    }

    private void OnClose(object sender, RoutedEventArgs args) => SystemCommands.CloseWindow(OwnerOf(sender));

    private static Window OwnerOf(object sender) => Window.GetWindow((DependencyObject)sender);
}

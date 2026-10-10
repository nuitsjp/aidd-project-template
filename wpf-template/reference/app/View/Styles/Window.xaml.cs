using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace WpfNotesSample.View.Styles;

// 独自のタイトルバーに置いたテーマ切り替えとウィンドウ操作ボタンの処理。
[SuppressMessage("Minor Code Smell", "S2325", Justification = "XAML から結び付けるイベントハンドラーはインスタンスメソッドが必要。")]
internal sealed partial class WindowStyles : ResourceDictionary
{
    private static readonly Uri _lightColors = new("pack://application:,,,/View/Styles/Colors.Light.xaml");
    private static readonly Uri _darkColors = new("pack://application:,,,/View/Styles/Colors.Dark.xaml");
    private bool _isDark;

    internal WindowStyles() => InitializeComponent();

    // 配色は App.xaml の先頭に読み込んだ辞書で、画面は DynamicResource で参照するため、辞書を差し替えると全画面に反映される。
    private void OnToggleTheme(object sender, RoutedEventArgs args)
    {
        _isDark = !_isDark;
        Application.Current.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = _isDark ? _darkColors : _lightColors };
    }

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

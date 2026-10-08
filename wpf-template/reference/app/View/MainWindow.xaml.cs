using System.Windows;
using WpfNotesSample.ViewModel;

namespace WpfNotesSample.View;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
    private async void OnLoaded(object sender, RoutedEventArgs args)
        => await ((MainViewModel)DataContext).ShowNotesCommand.ExecuteAsync(null);
}

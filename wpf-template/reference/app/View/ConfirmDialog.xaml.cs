using System.Windows;

namespace WpfNotesSample.View;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void OnOk(object sender, RoutedEventArgs args) => DialogResult = true;
}

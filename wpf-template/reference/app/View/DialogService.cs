using System.Windows;
using WpfNotesSample.ViewModel;

namespace WpfNotesSample.View;

public sealed class DialogService : IDialogService
{
    public bool Confirm(string title, string message)
    {
        var owner = Application.Current.MainWindow;
        // Material Design 3 の dialog と同じく、表示中は親ウィンドウ全体を scrim で覆い、その中央に表示する。
        var mask = owner.Template.FindName("PART_Mask", owner) as UIElement;
        if (mask != null)
        {
            mask.Visibility = Visibility.Visible;
        }
        var dialog = new ConfirmDialog(title, message) { Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        try
        {
            return dialog.ShowDialog() == true;
        }
        finally
        {
            if (mask != null)
            {
                mask.Visibility = Visibility.Collapsed;
            }
        }
    }
}

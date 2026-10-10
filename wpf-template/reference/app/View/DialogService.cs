using System.Windows;
using WpfNotesSample.ViewModel;

namespace WpfNotesSample.View;

public sealed class DialogService : IDialogService
{
    // Ant Design の Modal は親の上端から 100 の位置に、左右中央で表示する。
    private const double ModalTop = 100;
    private const double CenterRatio = 0.5;

    public bool Confirm(string title, string message)
    {
        var owner = Application.Current.MainWindow;
        // Ant Design の Modal と同じく、表示中は親ウィンドウ全体をマスクで覆う。
        var mask = owner.Template.FindName("PART_Mask", owner) as UIElement;
        if (mask != null)
        {
            mask.Visibility = Visibility.Visible;
        }
        var dialog = new ConfirmDialog(title, message) { Owner = owner, WindowStartupLocation = WindowStartupLocation.Manual };
        dialog.Left = owner.Left + (owner.ActualWidth - dialog.Width) * CenterRatio;
        dialog.Top = owner.Top + ModalTop;
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

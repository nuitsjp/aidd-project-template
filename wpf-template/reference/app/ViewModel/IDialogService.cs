namespace WpfNotesSample.ViewModel;

// 利用者に確認を求める。承認した場合だけ true を返す。
public interface IDialogService
{
    bool Confirm(string title, string message);
}

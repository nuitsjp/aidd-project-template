using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;

namespace WpfNotesSample.ViewModel;

public partial class MainViewModel
{
    private readonly IPresentationService presentation;
    public MainViewModel(IPresentationService presentation, NavigationState navigation, ApplicationOptions options)
    {
        this.presentation = presentation;
        Navigation = navigation;
        Title = ApplicationOptions.AppId + " — メモ";
        Mode = options.IsMock ? "モック：変更は終了時に破棄されます" : "実処理：SQLiteに保存";
    }

    public NavigationState Navigation { get; }
    public string Title { get; }
    public string Mode { get; }

    [RelayCommand]
    private async Task ShowNotesAsync() => await presentation.NavigateToNoteListAsync();

    [RelayCommand]
    private async Task ShowImportAsync() => await presentation.NavigateToImportNotesAsync();
}

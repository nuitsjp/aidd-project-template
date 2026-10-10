using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;

namespace WpfNotesSample.ViewModel;

public partial class MainViewModel
{
    private readonly IPresentationService _presentation;
    public MainViewModel(IPresentationService presentation, NavigationState navigation, ApplicationOptions options)
    {
        _presentation = presentation;
        Navigation = navigation;
        Title = ApplicationOptions.AppId;
        Mode = options.IsMock ? "モック：変更は終了時に破棄されます" : "";
    }

    public NavigationState Navigation { get; }
    public string Title { get; }
    public string Mode { get; }

    [RelayCommand]
    private async Task ShowNotesAsync() => await _presentation.NavigateToNoteListAsync();
}

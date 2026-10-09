using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfNotesSample.ViewModel;

public partial class NavigationState : ObservableObject
{
    [ObservableProperty]
    private PageViewModel? _currentPage;
}

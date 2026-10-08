using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Kamishibai;

namespace WpfNotesSample.ViewModel;

public abstract partial class PageViewModel : ObservableObject, INavigatedAsyncAware, IPausingAware, IDisposingAware
{
    protected readonly IPresentationService Presentation;
    private readonly NavigationState navigation;

    protected PageViewModel(IPresentationService presentation, NavigationState navigation)
    {
        Presentation = presentation;
        this.navigation = navigation;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    private bool isBusy;

    [ObservableProperty]
    private bool isDirty;

    [ObservableProperty]
    private string errorMessage = "";

    [ObservableProperty]
    private string statusMessage = "";

    public bool CanInteract => !IsBusy;

    public async Task OnNavigatedAsync(PostForwardEventArgs args)
    {
        navigation.CurrentPage = this;
        await LoadAsync();
    }

    public void OnPausing(PreForwardEventArgs args) => args.Cancel = !CanLeave();
    public void OnDisposing(PreBackwardEventArgs args) => args.Cancel = !CanLeave();

    public bool CanLeave()
    {
        if (IsBusy) return false;
        return !IsDirty || Presentation.ShowMessage(
            "保存していない変更を破棄しますか？", "変更の破棄",
            MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.OK;
    }

    protected virtual Task LoadAsync() => Task.CompletedTask;

    protected async Task RunOperationAsync(Func<Task> operation)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = "";
        StatusMessage = "";
        try { await operation(); }
        catch (Exception error) { ErrorMessage = error.Message; }
        finally { IsBusy = false; }
    }
}

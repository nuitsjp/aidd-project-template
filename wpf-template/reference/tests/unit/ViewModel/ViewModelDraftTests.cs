using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Shouldly;
using WpfNotesSample.Model.Domain.Notes;
using WpfNotesSample.Model.UseCase;
using WpfNotesSample.ViewModel;
using Xunit;

namespace WpfNotesSample.UnitTests.ViewModel;

public sealed class ViewModelDraftTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedSavePreservesTheDraftAndDisplaysTheError(bool conflict)
    {
        Exception error = conflict ? new NoteConflictException() : new NoteValidationException("入力を確認してください。");
        var service = new StubNotesService { Save = (_, _, _, _) => Task.FromException<Note>(error) };
        var saved = new Note(Guid.NewGuid(), "保存済みタイトル", "保存済み本文", 3, DateTime.UtcNow);
        var editor = new NoteEditViewModel(saved, service, null!, new NavigationState())
        {
            NoteTitle = "作業中のタイトル",
            Body = "作業中の本文",
        };

        await editor.SaveCommand.ExecuteAsync(null);

        editor.NoteTitle.ShouldBe("作業中のタイトル");
        editor.Body.ShouldBe("作業中の本文");
        editor.IsDirty.ShouldBeTrue();
        editor.ErrorMessage.ShouldBe(error.Message);
        editor.CanInteract.ShouldBeTrue();
        editor.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task SuccessfulSaveUsesTheReturnedIdentityForTheNextSaveAndDiscardRestoresTheLatestSavedContent()
    {
        var id = Guid.NewGuid();
        var ids = new List<Guid?>();
        var versions = new List<int?>();
        var service = new StubNotesService
        {
            Save = (noteId, version, title, body) =>
            {
                ids.Add(noteId);
                versions.Add(version);
                return Task.FromResult(new Note(id, title.Trim(), body, ids.Count, DateTime.UtcNow));
            },
        };
        var editor = new NoteEditViewModel(service, null!, new NavigationState())
        {
            NoteTitle = "  最初のタイトル  ",
            Body = "最初の本文",
        };

        await editor.SaveCommand.ExecuteAsync(null);
        editor.NoteTitle.ShouldBe("最初のタイトル");
        editor.IsDirty.ShouldBeFalse();
        editor.NoteTitle = "  更新したタイトル  ";
        editor.Body = "更新した本文";
        await editor.SaveCommand.ExecuteAsync(null);

        ids.ShouldBe(new Guid?[] { null, id });
        versions.ShouldBe(new int?[] { null, 1 });
        editor.IsDirty.ShouldBeFalse();
        editor.NoteTitle = "破棄するタイトル";
        editor.Body = "破棄する本文";
        editor.IsDirty.ShouldBeTrue();
        editor.DiscardCommand.Execute(null);

        editor.NoteTitle.ShouldBe("更新したタイトル");
        editor.Body.ShouldBe("更新した本文");
        editor.IsDirty.ShouldBeFalse();
        editor.ErrorMessage.ShouldBeEmpty();
    }

    [Fact]
    public void ChangingImportInputClearsThePreviewAndRequiresConfirmationAgain()
    {
        var importer = CreateImporter(new StubNotesService());
        importer.Input = "タイトル1\t本文1\nタイトル2\t本文2";
        importer.PreviewCommand.Execute(null);
        importer.IsConfirmed.ShouldBeTrue();
        importer.PreviewItems.Count.ShouldBe(2);

        importer.Input += "\nタイトル3\t本文3";

        importer.IsConfirmed.ShouldBeFalse();
        importer.PreviewItems.ShouldBeEmpty();
        importer.IsDirty.ShouldBeTrue();
        importer.StatusMessage.ShouldBeEmpty();
    }

    [Fact]
    public async Task FailedImportPreservesInputAndTheConfirmedPreviewForRetry()
    {
        var error = new SqliteException("保存先に書き込めません。", 10);
        var service = new StubNotesService
        {
            Import = _ => Task.FromException<IReadOnlyList<Note>>(error),
        };
        var importer = CreateImporter(service);
        const string input = "タイトル1\t本文1\nタイトル2\t本文2";
        importer.Input = input;
        importer.PreviewCommand.Execute(null);

        await importer.ConfirmCommand.ExecuteAsync(null);

        importer.Input.ShouldBe(input);
        importer.PreviewItems.Count.ShouldBe(2);
        importer.IsConfirmed.ShouldBeTrue();
        importer.IsDirty.ShouldBeTrue();
        importer.ErrorMessage.ShouldBe(error.Message);
        importer.CanInteract.ShouldBeTrue();
        importer.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task SuccessfulImportClearsTheDraftAndDisplaysTheSavedCount()
    {
        var service = new StubNotesService
        {
            Import = inputs => Task.FromResult<IReadOnlyList<Note>>(inputs.Select(input =>
                new Note(Guid.NewGuid(), input.Title, input.Body, 1, DateTime.UtcNow)).ToList()),
        };
        var importer = CreateImporter(service);
        importer.Input = "タイトル1\t本文1\nタイトル2\t本文2";
        importer.PreviewCommand.Execute(null);

        await importer.ConfirmCommand.ExecuteAsync(null);

        importer.Input.ShouldBeEmpty();
        importer.PreviewItems.ShouldBeEmpty();
        importer.IsConfirmed.ShouldBeFalse();
        importer.IsDirty.ShouldBeFalse();
        importer.ErrorMessage.ShouldBeEmpty();
        importer.StatusMessage.ShouldBe("2 件を登録しました。");
    }

    [Fact]
    public async Task FailedRefreshPreservesThePreviouslyLoadedNotesAndDisplaysTheError()
    {
        var saved = new[]
        {
            new Note(Guid.NewGuid(), "表示済みメモ1", "本文1", 1, DateTime.UtcNow),
            new Note(Guid.NewGuid(), "表示済みメモ2", "本文2", 2, DateTime.UtcNow),
        };
        var service = new StubNotesService
        {
            List = () => Task.FromResult<IReadOnlyList<Note>>(saved),
        };
        var list = new NoteListViewModel(service, null!, new NavigationState());
        await list.RefreshCommand.ExecuteAsync(null);
        list.Notes.ShouldBe(saved);
        var error = new SqliteException("一覧を読み込めません。", 10);
        service.List = () => Task.FromException<IReadOnlyList<Note>>(error);

        await list.RefreshCommand.ExecuteAsync(null);

        list.Notes.ShouldBe(saved);
        list.ErrorMessage.ShouldBe(error.Message);
        list.CanInteract.ShouldBeTrue();
        list.IsBusy.ShouldBeFalse();
    }

    private static ImportNotesViewModel CreateImporter(INotesService service) =>
        new ImportNotesViewModel(new PreviewNotesUseCase(service), null!, new NavigationState());

    private sealed class StubNotesService : INotesService
    {
        public Func<Guid?, int?, string, string, Task<Note>>? Save { get; set; }
        public Func<IReadOnlyList<NoteInput>, Task<IReadOnlyList<Note>>>? Import { get; set; }
        public Func<Task<IReadOnlyList<Note>>>? List { get; set; }

        public Task<Note> SaveAsync(Guid? id, int? expectedVersion, string title, string body) =>
            Save?.Invoke(id, expectedVersion, title, body) ?? throw new NotSupportedException();

        public Task<IReadOnlyList<Note>> ImportAsync(IReadOnlyList<NoteInput> inputs) =>
            Import?.Invoke(inputs) ?? throw new NotSupportedException();

        public Task<IReadOnlyList<Note>> ListAsync() =>
            List?.Invoke() ?? throw new NotSupportedException();
        public Task RemoveAsync(Guid id, int expectedVersion) => throw new NotSupportedException();
    }
}

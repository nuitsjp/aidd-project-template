using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using WpfNotesSample.Domain.Notes;
using WpfNotesSample.ViewModel;
using Xunit;

namespace WpfNotesSample.UnitTests.ViewModel;

public sealed class ViewModelDraftTests
{
    // 組み合わせの多い分岐: 保存・再保存・破棄を続けたときに、保存結果の ID と版を引き継ぎ、破棄で最新の保存内容へ戻ることを確認する。
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
        var editor = new NoteEditViewModel(service, null!, null!, new NavigationState())
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

    private sealed class StubNotesService : INotesService
    {
        public Func<Guid?, int?, string, string, Task<Note>>? Save { get; set; }

        public Task<Note> SaveAsync(Guid? id, int? expectedVersion, string title, string body) =>
            Save?.Invoke(id, expectedVersion, title, body) ?? throw new NotSupportedException();

        public Task<Note?> GetAsync(Guid id) => throw new NotSupportedException();

        public Task<IReadOnlyList<Note>> ListAsync() => throw new NotSupportedException();
        public Task RemoveAsync(Guid id, int expectedVersion) => throw new NotSupportedException();
    }
}

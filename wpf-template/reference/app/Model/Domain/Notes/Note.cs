using System;

namespace WpfNotesSample.Model.Domain.Notes;

public sealed class Note
{
    public Note(Guid id, string title, string body, int version, DateTime updatedAt)
    {
        Id = id;
        Title = title;
        Body = body;
        Version = version;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Body { get; }
    public int Version { get; }
    public DateTime UpdatedAt { get; }
}

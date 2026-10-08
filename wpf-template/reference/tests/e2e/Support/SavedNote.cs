using System;

namespace WpfNotesSample.E2eTests.Support;

internal sealed class SavedNote : IEquatable<SavedNote>
{
    public SavedNote(string title, string body, int version)
    {
        Title = title;
        Body = body;
        Version = version;
    }

    public string Title { get; }
    public string Body { get; }
    public int Version { get; }

    public bool Equals(SavedNote? other)
        => other != null && Title == other.Title && Body == other.Body && Version == other.Version;

    public override bool Equals(object? obj) => obj is SavedNote other && Equals(other);
    public override int GetHashCode() => Title.GetHashCode() ^ Body.GetHashCode() ^ Version;
    public override string ToString() => $"{Title}, {Body}, version={Version}";
}

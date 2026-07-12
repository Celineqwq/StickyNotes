using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StickyNotes.Models;

public enum NoteType
{
    Text,
    Image,
    File
}

public class NoteItem : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public NoteType Type { get; set; } = NoteType.Text;
    public string Content { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string TemplateName { get; set; } = "Yellow";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
    public bool IsPinned { get; set; }
    public long SortOrder { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public NoteItem Clone() => new()
    {
        Id = Id,
        Type = Type,
        Content = Content,
        FileName = FileName,
        TemplateName = TemplateName,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        IsPinned = IsPinned,
        SortOrder = SortOrder
    };
}
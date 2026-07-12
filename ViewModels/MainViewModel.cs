using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StickyNotes.Models;
using StickyNotes.Services;

namespace StickyNotes.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DataService _dataService;
    private readonly AutoStartService _autoStartService;

    public MainViewModel(DataService dataService, AutoStartService autoStartService)
    {
        _dataService = dataService;
        _autoStartService = autoStartService;
    }

    [ObservableProperty]
    private ObservableCollection<NoteItem> _notes = new();

    [ObservableProperty]
    private AppSettings _settings = new();

    [ObservableProperty]
    private bool _isLoading;

    // ─── Initialize ─────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        IsLoading = true;

        await _dataService.InitializeAsync();

        // Auto-cleanup on startup
        var deleted = await _dataService.CleanupOldNotesAsync(7);
        if (deleted > 0)
        {
            System.Diagnostics.Debug.WriteLine($"Cleaned up {deleted} old notes.");
        }

        Settings = await _dataService.LoadSettingsAsync();
        var notes = await _dataService.GetAllNotesAsync();

        Notes.Clear();
        foreach (var note in notes)
        {
            Notes.Add(note);
        }

        IsLoading = false;
    }

    // ─── Note Operations ───────────────────────────────────────

    public NoteItem CreateBlankNote()
    {
        var sortBase = Notes.Count > 0 ? Notes.Max(n => n.SortOrder) : 0;
        var note = new NoteItem
        {
            Type = NoteType.Text,
            Content = string.Empty,
            TemplateName = Settings.DefaultTemplate,
            SortOrder = sortBase + 1000
        };

        Notes.Insert(0, note);
        return note;
    }

    /// <summary>
    /// Saves a note (creates or updates) after editing is complete.
    /// Called by NoteCard.ContentSaved event.
    /// Auto-detects whether the note already exists in the database.
    /// </summary>
    public async Task SaveNote(NoteItem note)
    {
        note.UpdatedAt = DateTime.Now;

        var existing = await _dataService.GetNoteByIdAsync(note.Id);
        if (existing != null)
            await _dataService.UpdateNoteAsync(note);
        else
            await _dataService.InsertNoteAsync(note);
    }

    [RelayCommand]
    public async Task AddTextNote(string? text = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var sortBase = Notes.Count > 0 ? Notes.Max(n => n.SortOrder) : 0;
        var note = new NoteItem
        {
            Type = NoteType.Text,
            Content = text,
            TemplateName = Settings.DefaultTemplate,
            SortOrder = sortBase + 1000
        };

        Notes.Insert(0, note);
        await _dataService.InsertNoteAsync(note);
    }

    public async Task PinToTop(NoteItem note)
    {
        var wasPinned = note.IsPinned;
        if (!wasPinned)
        {
            note.IsPinned = true;
            if (Notes.Remove(note))
                Notes.Insert(0, note);
        }
        else
        {
            note.IsPinned = false;
            if (Notes.Remove(note))
            {
                int insertAt = 0;
                for (int i = 0; i < Notes.Count; i++)
                    if (!Notes[i].IsPinned && Notes[i].CreatedAt <= note.CreatedAt)
                        insertAt = i + 1;
                if (insertAt >= Notes.Count)
                    Notes.Add(note);
                else
                    Notes.Insert(insertAt, note);
            }
        }
        await _dataService.UpdateNoteAsync(note);
    }

    [RelayCommand]
    public async Task AddImageNote(string? filePath = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            filePath = await PickFileAsync("图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico;*.webp");
            if (string.IsNullOrEmpty(filePath))
                return;
        }

        var sortBase = Notes.Count > 0 ? Notes.Max(n => n.SortOrder) : 0;
        var note = new NoteItem
        {
            Type = NoteType.Image,
            Content = filePath,
            FileName = Path.GetFileName(filePath),
            TemplateName = Settings.DefaultTemplate,
            SortOrder = sortBase + 1000
        };

        Notes.Insert(0, note);
        await _dataService.InsertNoteAsync(note);
    }

    [RelayCommand]
    public async Task AddFileNote(string? filePath = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            filePath = await PickFileAsync("所有文件|*.*");
            if (string.IsNullOrEmpty(filePath))
                return;
        }

        var sortBase = Notes.Count > 0 ? Notes.Max(n => n.SortOrder) : 0;
        var note = new NoteItem
        {
            Type = NoteType.File,
            Content = filePath,
            FileName = Path.GetFileName(filePath),
            TemplateName = Settings.DefaultTemplate,
            SortOrder = sortBase + 1000
        };

        Notes.Insert(0, note);
        await _dataService.InsertNoteAsync(note);
    }

    /// <summary>
    /// Manual trigger from UI (tray menu, etc.). Inspects clipboard directly.
    /// </summary>
    [RelayCommand]
    public async Task AddClipboardContent()
    {
        var clip = new ClipboardWatcher.ClipboardContent { Type = ClipboardWatcher.ClipboardContentType.None };
        try
        {
            if (Clipboard.ContainsImage()) clip.Type = ClipboardWatcher.ClipboardContentType.Image;
            else if (Clipboard.ContainsFileDropList()) clip.Type = ClipboardWatcher.ClipboardContentType.File;
            else if (Clipboard.ContainsText()) clip.Type = ClipboardWatcher.ClipboardContentType.Text;
            else return;
        }
        catch { return; }
        await AddClipboardContentInternal(clip);
    }

    private async Task AddClipboardContentInternal(ClipboardWatcher.ClipboardContent clip)
    {
        try
        {
            if (clip.Type == ClipboardWatcher.ClipboardContentType.Image)
            {
                if (!Clipboard.ContainsImage()) return;
                var imagePath = Path.Combine(
                    Path.GetTempPath(),
                    $"StickyNotes_img_{Guid.NewGuid():N}.png");

                var bitmap = Clipboard.GetImage();
                if (bitmap != null)
                {
                    using var stream = File.Create(imagePath);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    encoder.Save(stream);
                }

                await AddImageNote(imagePath);
            }
            else if (clip.Type == ClipboardWatcher.ClipboardContentType.File)
            {
                if (!Clipboard.ContainsFileDropList()) return;
                var files = Clipboard.GetFileDropList();
                if (files.Count > 0)
                {
                    var filePath = files[0];
                    if (IsImageFile(filePath))
                        await AddImageNote(filePath);
                    else
                        await AddFileNote(filePath);
                }
            }
            else if (clip.Type == ClipboardWatcher.ClipboardContentType.Text)
            {
                if (!Clipboard.ContainsText()) return;
                var text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text)) return;

                var sortBase = Notes.Count > 0 ? Notes.Max(n => n.SortOrder) : 0;
                var note = new NoteItem
                {
                    Type = NoteType.Text,
                    Content = text,
                    TemplateName = Settings.DefaultTemplate,
                    SortOrder = sortBase + 1000
                };

                // --- Attach metadata from the smart watcher ---
                var prefix = "";
                if (!string.IsNullOrEmpty(clip.SourceWindow))
                    prefix = $"[{clip.SourceWindow}]\n";
                note.Content = prefix + text;

                if (!string.IsNullOrEmpty(clip.SuggestedFileName))
                    note.FileName = clip.SuggestedFileName;

                Notes.Insert(0, note);
                await _dataService.InsertNoteAsync(note);
            }
        }
        catch { }
    }

    [RelayCommand]
    public async Task DeleteNote(NoteItem note)
    {
        Notes.Remove(note);
        await _dataService.DeleteNoteAsync(note.Id);
    }

    [RelayCommand]
    public async Task UpdateNoteTemplate(NoteItem note)
    {
        note.UpdatedAt = DateTime.Now;
        await _dataService.UpdateNoteAsync(note);
    }

    public async Task UpdateNoteContent(NoteItem note, string newContent)
    {
        note.Content = newContent;
        note.UpdatedAt = DateTime.Now;
        await _dataService.UpdateNoteAsync(note);
    }

    // ─── Settings Operations ────────────────────────────────────

    [RelayCommand]
    public async Task SaveSettings()
    {
        await _dataService.SaveSettingsAsync(Settings);

        if (Settings.AutoStart)
            _autoStartService.EnableAutoStart();
        else
            _autoStartService.DisableAutoStart();
    }

    [RelayCommand]
    public async Task CleanupOldNotes()
    {
        var deleted = await _dataService.CleanupOldNotesAsync(7);
        if (deleted > 0)
        {
            // Refresh the list
            var notes = await _dataService.GetAllNotesAsync();
            Notes.Clear();
            foreach (var note in notes)
                Notes.Add(note);
        }
    }

    public async Task<int> GetOldNotesCountAsync()
    {
        return await _dataService.GetOldNotesCountAsync(7);
    }

    // ─── Clipboard from external source ────────────────────────

    public async Task OnClipboardContentDetected(ClipboardWatcher.ClipboardContent clip)
    {
        if (!Settings.TrackClipboard) return;

        await AddClipboardContentInternal(clip);
    }

    // ─── Reorder ─────────────────────────────────────────────────

    public async Task RefreshSortOrdersAsync()
    {
        await _dataService.UpdateSortOrdersAsync(Notes.ToList());
    }

    // ─── Helpers ────────────────────────────────────────────────

    private static async Task<string?> PickFileAsync(string filter)
    {
        return await Task.Run(() =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = filter };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        });
    }

    private static bool IsImageFile(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".ico" or ".webp";
    }
}
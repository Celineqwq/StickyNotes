using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StickyNotes.Models;
using StickyNotes.Services;

namespace StickyNotes.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly DataService _dataService;
    private readonly AutoStartService _autoStartService;

    [ObservableProperty]
    private bool _alwaysOnTop = true;

    [ObservableProperty]
    private bool _isDraggable = true;

    [ObservableProperty]
    private bool _trackClipboard;

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private bool _miniMode;

    [ObservableProperty]
    private string _defaultTemplate = "Yellow";

    [ObservableProperty]
    private int _totalNotes;

    [ObservableProperty]
    private string _earliestDate = "";

    [ObservableProperty]
    private int _oldNotesCount;

    public SettingsViewModel(DataService dataService, AutoStartService autoStartService)
    {
        _dataService = dataService;
        _autoStartService = autoStartService;
    }

    public async Task LoadFromAsync(AppSettings settings)
    {
        AlwaysOnTop = settings.AlwaysOnTop;
        IsDraggable = settings.IsDraggable;
        TrackClipboard = settings.TrackClipboard;
        AutoStart = settings.AutoStart || _autoStartService.IsAutoStartEnabled();
        MiniMode = settings.MiniMode;
        DefaultTemplate = settings.DefaultTemplate;

        // Load stats
        var notes = await _dataService.GetAllNotesAsync();
        TotalNotes = notes.Count;
        if (notes.Count > 0)
            EarliestDate = notes.Min(n => n.CreatedAt).ToString("yyyy-MM-dd");

        OldNotesCount = await _dataService.GetOldNotesCountAsync(7);
    }

    public AppSettings ApplyTo(AppSettings settings)
    {
        settings.AlwaysOnTop = AlwaysOnTop;
        settings.IsDraggable = IsDraggable;
        settings.TrackClipboard = TrackClipboard;
        settings.AutoStart = AutoStart;
        settings.MiniMode = MiniMode;
        settings.DefaultTemplate = DefaultTemplate;
        return settings;
    }

    [RelayCommand]
    public async Task CleanupOldNotes()
    {
        var deleted = await _dataService.CleanupOldNotesAsync(7);
        if (deleted > 0)
        {
            OldNotesCount = 0;
            var notes = await _dataService.GetAllNotesAsync();
            TotalNotes = notes.Count;
            if (notes.Count > 0)
                EarliestDate = notes.Min(n => n.CreatedAt).ToString("yyyy-MM-dd");
            else
                EarliestDate = "";
        }
    }
}
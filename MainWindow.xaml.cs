using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32;
using StickyNotes.Models;
using StickyNotes.Services;
using StickyNotes.ViewModels;

namespace StickyNotes;

public partial class MainWindow : Window
{
    private MainViewModel _viewModel;
    private DataService _dataService;
    private AutoStartService _autoStartService;
    private ClipboardWatcher _clipboardWatcher;
    private bool _isRestoring;

    public MainWindow()
    {
        _dataService = new DataService();
        _autoStartService = new AutoStartService();
        _viewModel = new MainViewModel(_dataService, _autoStartService);
        _clipboardWatcher = new ClipboardWatcher();

        // --- Load settings synchronously so window size/position is
        //     correct BEFORE the first render. This prevents the
        //     default-size flicker and ensures the saved size sticks. ---
        try
        {
            var saved = _dataService.LoadSettings();
            _viewModel.Settings = saved;
            if (saved.WindowWidth > 100 && saved.WindowHeight > 100)
            {
                Width = saved.WindowWidth;
                Height = saved.WindowHeight;
            }
            if (saved.WindowLeft >= -10000 && saved.WindowTop >= -10000)
            {
                Left = saved.WindowLeft;
                Top = saved.WindowTop;
            }
        }
        catch { }

        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    public MainWindow(DataService dataService, AutoStartService autoStartService,
                      MainViewModel viewModel, ClipboardWatcher clipboardWatcher)
    {
        _dataService = dataService;
        _autoStartService = autoStartService;
        _viewModel = viewModel;
        _clipboardWatcher = clipboardWatcher;

        // Load settings synchronously
        try
        {
            var saved = _dataService.LoadSettings();
            if (saved.WindowWidth > 100 && saved.WindowHeight > 100)
            {
                Width = saved.WindowWidth;
                Height = saved.WindowHeight;
            }
            if (saved.WindowLeft >= -10000 && saved.WindowTop >= -10000)
            {
                Left = saved.WindowLeft;
                Top = saved.WindowTop;
            }
        }
        catch { }

        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isRestoring = true;
        await _viewModel.InitializeAsync();

        var hwnd = new WindowInteropHelper(this).Handle;
        _clipboardWatcher.ContentChanged += OnClipboardContentChanged;
        _clipboardWatcher.Start(hwnd);

        UpdatePinIcon();
        _isRestoring = false;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        // Persist window geometry synchronously
        _viewModel.Settings.WindowLeft = Left;
        _viewModel.Settings.WindowTop = Top;
        _viewModel.Settings.WindowWidth = Width;
        _viewModel.Settings.WindowHeight = Height;
        _dataService.SaveSettings(_viewModel.Settings);

        if (!_viewModel.Settings.MiniMode)
            _clipboardWatcher?.Dispose();
    }

    private async void OnClipboardContentChanged(object? sender, ClipboardWatcher.ClipboardContent clip)
    {
        await Dispatcher.InvokeAsync(() => _viewModel.OnClipboardContentDetected(clip));
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isRestoring || _viewModel.IsLoading) return;
        _viewModel.Settings.WindowWidth = Width;
        _viewModel.Settings.WindowHeight = Height;
    }

    private void TitleText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.Settings.IsDraggable) return;
        DragMove();
    }

    private void PinButton_Checked(object sender, RoutedEventArgs e)
    {
        _viewModel.Settings.AlwaysOnTop = true; UpdatePinIcon();
        _ = _viewModel.SaveSettings();
    }

    private void PinButton_Unchecked(object sender, RoutedEventArgs e)
    {
        _viewModel.Settings.AlwaysOnTop = false; UpdatePinIcon();
        _ = _viewModel.SaveSettings();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var w = new SettingsWindow(_dataService, _autoStartService, _viewModel);
        w.Owner = this; w.Topmost = _viewModel.Settings.AlwaysOnTop; w.ShowDialog();
    }

    private void MiniModeButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Settings.WindowWidth = Width;
        _viewModel.Settings.WindowHeight = Height;
        _viewModel.Settings.WindowLeft = Left;
        _viewModel.Settings.WindowTop = Top;
        _viewModel.Settings.MiniMode = true;
        _ = _viewModel.SaveSettings();
        Hide();
        Application.Current.MainWindow = null;
        new MiniBallWindow(_dataService, _autoStartService, _viewModel, _clipboardWatcher).Show();
        Close();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) { Hide(); }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _clipboardWatcher.Dispose();
        Application.Current.Shutdown();
    }

    private void UpdatePinIcon()
    {
        if (_viewModel.Settings.AlwaysOnTop)
        {
            PinIcon.Text = "●";
            PinIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        }
        else
        {
            PinIcon.Text = "○";
            PinIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
        }
    }

    private async void NoteCard_DeleteRequested(object? sender, NoteItem note) => await _viewModel.DeleteNote(note);
    private async void NoteCard_TemplateChanged(object? sender, NoteItem note) => await _viewModel.UpdateNoteTemplate(note);
    private void AddNoteBorder_Click(object sender, MouseButtonEventArgs e) => _viewModel.CreateBlankNote();
    private void TitleAddButton_Click(object sender, RoutedEventArgs e) => _viewModel.CreateBlankNote();
    private async void NoteCard_ContentSaved(object? sender, NoteItem note) => await _viewModel.SaveNote(note);
    private async void NoteCard_PinToTopRequested(object? sender, NoteItem note) => await _viewModel.PinToTop(note);

    public List<NoteItem> GetSelectedNotes() =>
        _viewModel.Notes.Where(n => n.IsSelected).ToList();

    public void UpdateBatchBar()
    {
        var count = _viewModel.Notes.Count(n => n.IsSelected);
        if (count > 0) { BatchBar.Visibility = Visibility.Visible; BatchCountText.Text = $"已选择 {count} 项"; }
        else { BatchBar.Visibility = Visibility.Collapsed; if (Controls.NoteCard.IsMultiSelectMode) Controls.NoteCard.IsMultiSelectMode = false; }
    }

    private void NotesListBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        { foreach (var n in _viewModel.Notes) n.IsSelected = true; UpdateBatchBar(); e.Handled = true; }
        else if (e.Key == Key.Delete) { _ = DeleteSelectedAsync(); }
    }

    private async Task DeleteSelectedAsync()
    {
        var sel = _viewModel.Notes.Where(n => n.IsSelected).ToList();
        if (sel.Count == 0) return;
        foreach (var n in sel) await _viewModel.DeleteNote(n);
        Controls.NoteCard.IsMultiSelectMode = false; UpdateBatchBar();
    }

    private async void DeleteSelected_Click(object sender, RoutedEventArgs e) => await DeleteSelectedAsync();

    private void ExportSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedNotes();
        if (selected.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "JSON 文件 (*.json)|*.json",
            FileName = $"StickyNotes_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = selected.Count == 1
                ? JsonSerializer.Serialize(selected[0], options)
                : JsonSerializer.Serialize(selected, options);
            File.WriteAllText(dialog.FileName, json);
        }
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var n in _viewModel.Notes) n.IsSelected = false;
        Controls.NoteCard.IsMultiSelectMode = false; UpdateBatchBar();
    }

    private void NotesList_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effects = DragDropEffects.Copy;
        else if (e.Data.GetDataPresent(Controls.NoteCard.ReorderFormat)) e.Effects = DragDropEffects.Move;
        else e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void NotesList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            var path = files[0];
            if (IsImageFile(path)) await _viewModel.AddImageNote(path);
            else await _viewModel.AddFileNote(path);
            return;
        }
        if (e.Data.GetDataPresent(Controls.NoteCard.ReorderFormat) && e.Data.GetData(Controls.NoteCard.ReorderFormat) is NoteItem dn)
        {
            var pos = e.GetPosition(NotesListBox);
            var t = GetNoteAtPosition(pos);
            if (t != null && t != dn) { int o = _viewModel.Notes.IndexOf(dn), n = _viewModel.Notes.IndexOf(t); if (o >= 0 && n >= 0 && o != n) { _viewModel.Notes.Move(o, n); await _viewModel.RefreshSortOrdersAsync(); } }
        }
    }

    private NoteItem? GetNoteAtPosition(Point position)
    {
        var el = NotesListBox.InputHitTest(position) as DependencyObject;
        while (el != null && el != NotesListBox) { if (el is ListBoxItem li && li.Content is NoteItem note) return note; el = VisualTreeHelper.GetParent(el); }
        return null;
    }

    private static bool IsImageFile(string path) => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".ico" or ".webp";
}

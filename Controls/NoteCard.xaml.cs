using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StickyNotes.Models;
using Microsoft.Win32;

namespace StickyNotes.Controls;

public partial class NoteCard : UserControl
{
    public static readonly DependencyProperty NoteProperty =
        DependencyProperty.Register(nameof(Note), typeof(NoteItem), typeof(NoteCard),
            new PropertyMetadata(null, OnNoteChanged));

    public NoteItem? Note
    {
        get => (NoteItem?)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    public event EventHandler<NoteItem>? DeleteRequested;
    public event EventHandler<NoteItem>? ContentSaved;
    public event EventHandler<NoteItem>? TemplateChanged;
    public event EventHandler<NoteItem>? PinToTopRequested;

    private static readonly Dictionary<string, (Color bg, Color header, Color text)> Templates = new()
    {
        ["Yellow"] = (Color.FromRgb(255, 249, 196), Color.FromRgb(255, 241, 118), Color.FromRgb(93, 64, 55)),
        ["Pink"] = (Color.FromRgb(248, 187, 208), Color.FromRgb(244, 143, 177), Color.FromRgb(136, 14, 79)),
        ["Green"] = (Color.FromRgb(200, 230, 201), Color.FromRgb(165, 214, 167), Color.FromRgb(27, 94, 32)),
        ["Blue"] = (Color.FromRgb(187, 222, 251), Color.FromRgb(144, 202, 249), Color.FromRgb(13, 71, 161)),
        ["Orange"] = (Color.FromRgb(255, 224, 178), Color.FromRgb(255, 204, 128), Color.FromRgb(191, 54, 12)),
        ["Purple"] = (Color.FromRgb(225, 190, 231), Color.FromRgb(206, 147, 216), Color.FromRgb(74, 20, 140)),
    };

    private Point _dragStartPoint;
    private bool _isEditing;
    private bool _isCollapsed = true;

    /// <summary>
    /// Static multi-select mode. When set, all cards are notified via this event.
    /// </summary>
    private static bool _isMultiSelectMode;
    internal static bool IsMultiSelectMode
    {
        get => _isMultiSelectMode;
        set
        {
            _isMultiSelectMode = value;
            OnMultiSelectModeChanged?.Invoke();
        }
    }
    internal static event Action? OnMultiSelectModeChanged;

    public NoteCard()
    {
        InitializeComponent();
        OnMultiSelectModeChanged += UpdateCheckboxVisibility;
        DataContextChanged += (_, _) =>
        {
            ApplyNoteTemplate();
            AutoEnterEditMode();
            UpdateCollapseState();
            UpdatePinMenuItem();
            UpdateCheckboxVisibility();
        };
    }

    internal void UpdateCheckboxVisibility()
    {
        if (SelectCheckBox != null)
            SelectCheckBox.Visibility = IsMultiSelectMode ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void OnNoteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteCard card)
            card.ApplyNoteTemplate();
    }

    private void ApplyNoteTemplate()
    {
        var note = Note ?? DataContext as NoteItem;
        if (note == null) return;

        if (Templates.TryGetValue(note.TemplateName, out var colors))
            CardBorder.Background = new SolidColorBrush(colors.bg);
        else
            CardBorder.Background = new SolidColorBrush(Templates["Yellow"].bg);
    }

    // ─── Auto-enter edit for blank notes ─────────────────────

    private void AutoEnterEditMode()
    {
        var note = Note ?? DataContext as NoteItem;
        if (note?.Type == NoteType.Text && string.IsNullOrEmpty(note.Content))
        {
            _isEditing = true;
            ContentBox.IsReadOnly = false;
            _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                ContentBox.Focus();
                ContentBox.SelectAll();
            });
        }
    }

    // ─── Large text collapse ─────────────────────────────────

    private const int CollapseThreshold = 3;
    private const double CollapsedMaxHeight = 64;

    private void UpdateCollapseState()
    {
        var note = Note ?? DataContext as NoteItem;
        if (note?.Type != NoteType.Text || string.IsNullOrEmpty(note.Content))
        {
            ExpandButton.Visibility = Visibility.Collapsed;
            return;
        }

        int lineCount = note.Content.Count(c => c == '\n') + 1;
        bool isLong = lineCount > CollapseThreshold || note.Content.Length > 120;

        if (!isLong)
        {
            ExpandButton.Visibility = Visibility.Collapsed;
            ContentBox.MaxHeight = double.MaxValue;
            return;
        }

        ExpandButton.Visibility = Visibility.Visible;
        if (_isCollapsed)
        {
            ContentBox.MaxHeight = CollapsedMaxHeight;
            ExpandButton.Text = "… 展开";
        }
        else
        {
            ContentBox.MaxHeight = double.MaxValue;
            ExpandButton.Text = "▲ 收起";
        }
    }

    private void ExpandButton_Click(object sender, MouseButtonEventArgs e)
    {
        _isCollapsed = !_isCollapsed;
        var note = Note ?? DataContext as NoteItem;
        if (note == null) return;

        if (_isCollapsed)
        {
            ContentBox.MaxHeight = CollapsedMaxHeight;
            ExpandButton.Text = "… 展开";
        }
        else
        {
            ContentBox.MaxHeight = double.MaxValue;
            ExpandButton.Text = "▲ 收起";
        }
        UpdateLayout();
        InvalidateVisual();
    }

    // ─── Enter/Exit Edit Mode ─────────────────────────────────

    public void EnterEditMode()
    {
        var note = Note ?? DataContext as NoteItem;
        if (note?.Type != NoteType.Text) return;

        _isEditing = true;
        ContentBox.IsReadOnly = false;
        ContentBox.MaxHeight = double.MaxValue;
        ExpandButton.Visibility = Visibility.Collapsed;
        _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            ContentBox.Focus();
            ContentBox.SelectAll();
        });
    }

    private void ExitEditMode()
    {
        if (!_isEditing) return;
        _isEditing = false;

        var note = Note ?? DataContext as NoteItem;
        if (note == null) return;

        ContentBox.IsReadOnly = true;
        UpdateCollapseState();

        if (string.IsNullOrWhiteSpace(note.Content))
        {
            DeleteRequested?.Invoke(this, note);
        }
        else
        {
            ContentSaved?.Invoke(this, note);
        }
    }

    // ─── TextBox Interaction ──────────────────────────────────

    private void ContentBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var note = Note ?? DataContext as NoteItem;
        if (note?.Type != NoteType.Text) return;

        // Ctrl+Click → toggle multi-select
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            if (note != null)
            {
                if (!IsMultiSelectMode)
                {
                    IsMultiSelectMode = true;
                }
                note.IsSelected = !note.IsSelected;
                var parent = GetMainWindow();
                parent?.UpdateBatchBar();
            }
            return;
        }

        if (!_isEditing)
        {
            ContentBox.IsReadOnly = false;
            _isEditing = true;
            _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                ContentBox.Focus();
                ContentBox.SelectAll();
            });
        }
    }

    private void ContentBox_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (!_isEditing)
            EnterEditMode();
    }

    private void ContentBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isEditing)
            ExitEditMode();
    }

    private void ContentBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isEditing) return;

        var note = Note ?? DataContext as NoteItem;
        if (note == null) return;

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _isEditing = false;
            ContentBox.IsReadOnly = true;
            UpdateCollapseState();
            if (string.IsNullOrWhiteSpace(note.Content))
                DeleteRequested?.Invoke(this, note);
            _ = Dispatcher.BeginInvoke(() => Focus());
        }
        else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            ExitEditMode();
        }
    }

    // ─── Context Menu ─────────────────────────────────────────

    private void PinToTopMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var note = Note ?? DataContext as NoteItem;
        if (note != null)
            PinToTopRequested?.Invoke(this, note);
    }

    private void MultiSelectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        IsMultiSelectMode = true; // static event auto-shows all checkboxes

        var note = Note ?? DataContext as NoteItem;
        if (note != null)
        {
            note.IsSelected = true;
        }

        GetMainWindow()?.UpdateBatchBar();
    }

    private void SelectCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        GetMainWindow()?.UpdateBatchBar();
    }

    private void UpdatePinMenuItem()
    {
        var note = Note ?? DataContext as NoteItem;
        if (note != null && PinMenuItem != null)
            PinMenuItem.Header = note.IsPinned ? "取消置顶" : "置顶";
    }

    private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var note = Note ?? DataContext as NoteItem;
        if (note == null) return;

        try
        {
            switch (note.Type)
            {
                case NoteType.Text:
                    Clipboard.SetText(note.Content);
                    break;
                case NoteType.Image:
                case NoteType.File:
                    if (File.Exists(note.Content))
                    {
                        var collection = new System.Collections.Specialized.StringCollection();
                        collection.Add(note.Content);
                        Clipboard.SetFileDropList(collection);
                    }
                    break;
            }
        }
        catch { }
    }

    private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
    {
        var note = Note ?? DataContext as NoteItem;
        if (note?.Content != null && File.Exists(note.Content))
            Process.Start("explorer.exe", $"/select,\"{note.Content}\"");
    }

    private void TemplateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string templateName)
        {
            var note = Note ?? DataContext as NoteItem;
            if (note != null)
            {
                note.TemplateName = templateName;
                ApplyNoteTemplate();
                TemplateChanged?.Invoke(this, note);
            }
        }
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var note = Note ?? DataContext as NoteItem;
        if (note != null)
            DeleteRequested?.Invoke(this, note);
    }

    private void ExportJsonMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var note = Note ?? DataContext as NoteItem;
        if (note == null) return;

        var mainWindow = GetMainWindow();
        if (mainWindow == null) return;

        List<NoteItem> notesToExport;
        if (IsMultiSelectMode)
        {
            notesToExport = mainWindow.GetSelectedNotes();
            if (!notesToExport.Any(n => n.Id == note.Id))
                notesToExport.Add(note);
        }
        else
        {
            notesToExport = new List<NoteItem> { note };
        }

        var dialog = new SaveFileDialog
        {
            Filter = "JSON 文件 (*.json)|*.json",
            FileName = $"StickyNotes_Export_{DateTime.Now:yyyyMMdd_HHmmss}.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = notesToExport.Count == 1
                ? JsonSerializer.Serialize(notesToExport[0], options)
                : JsonSerializer.Serialize(notesToExport, options);
            File.WriteAllText(dialog.FileName, json);
        }
    }

    // ─── Drag & Drop (Reorder + external) ────────────────────

    internal const string ReorderFormat = "StickyNotesReorder";

    private void Card_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Don't record drag start for text notes — allows text selection
        var note = Note ?? DataContext as NoteItem;
        if (note?.Type == NoteType.Text) return;

        _dragStartPoint = e.GetPosition(this);
    }

    private void Card_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var note = Note ?? DataContext as NoteItem;
        if (note == null || note.Type == NoteType.Text) return;

        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - _dragStartPoint.X) < 8 &&
            Math.Abs(pos.Y - _dragStartPoint.Y) < 8)
            return;

        try
        {
            var data = new DataObject();
            data.SetData(ReorderFormat, note);

            switch (note.Type)
            {
                case NoteType.Image:
                case NoteType.File:
                    if (File.Exists(note.Content))
                        data.SetData(DataFormats.FileDrop, new[] { note.Content });
                    break;
            }

            DragDrop.DoDragDrop(this, data, DragDropEffects.Copy | DragDropEffects.Move);
        }
        catch { }
    }

    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Click (no drag) → enter edit mode for text, or toggle select for others
        var note = Note ?? DataContext as NoteItem;
        if (note == null) return;

        // Check if this was a click (not a drag)
        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - _dragStartPoint.X) > 5 ||
            Math.Abs(pos.Y - _dragStartPoint.Y) > 5)
            return;

        // Ctrl+Click → toggle multi-select
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (!IsMultiSelectMode)
            {
                IsMultiSelectMode = true;
            }
            note.IsSelected = !note.IsSelected;
            var parent = GetMainWindow();
            parent?.UpdateBatchBar();
            return;
        }
    }

    // ─── Helpers ─────────────────────────────────────────────

    private MainWindow? GetMainWindow() => Window.GetWindow(this) as MainWindow;
}
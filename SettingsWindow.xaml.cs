using System.Text.Json;
using System.Windows;
using StickyNotes.Models;
using StickyNotes.Services;
using StickyNotes.ViewModels;
using Microsoft.Win32;

namespace StickyNotes;

public partial class SettingsWindow : Window
{
    private readonly DataService _dataService;
    private readonly AutoStartService _autoStartService;
    private readonly MainViewModel _mainViewModel;
    private readonly SettingsViewModel _settingsViewModel;

    public SettingsWindow(
        DataService dataService,
        AutoStartService autoStartService,
        MainViewModel mainViewModel)
    {
        InitializeComponent();

        _dataService = dataService;
        _autoStartService = autoStartService;
        _mainViewModel = mainViewModel;
        _settingsViewModel = new SettingsViewModel(dataService, autoStartService);

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _settingsViewModel.AlwaysOnTop = _mainViewModel.Settings.AlwaysOnTop;
            _settingsViewModel.IsDraggable = _mainViewModel.Settings.IsDraggable;
            _settingsViewModel.TrackClipboard = _mainViewModel.Settings.TrackClipboard;
            _settingsViewModel.AutoStart = _mainViewModel.Settings.AutoStart || _autoStartService.IsAutoStartEnabled();
            _settingsViewModel.MiniMode = _mainViewModel.Settings.MiniMode;
            _settingsViewModel.DefaultTemplate = _mainViewModel.Settings.DefaultTemplate;

            AlwaysOnTopCheckBox.IsChecked = _settingsViewModel.AlwaysOnTop;
            DraggableCheckBox.IsChecked = _settingsViewModel.IsDraggable;
            TrackClipboardCheckBox.IsChecked = _settingsViewModel.TrackClipboard;
            AutoStartCheckBox.IsChecked = _settingsViewModel.AutoStart;
            WindowModeCombo.SelectedIndex = _settingsViewModel.MiniMode ? 1 : 0;

            for (int i = 0; i < TemplateCombo.Items.Count; i++)
            {
                if (TemplateCombo.Items[i] is ComboBoxItem item &&
                    item.Tag?.ToString() == _settingsViewModel.DefaultTemplate)
                {
                    TemplateCombo.SelectedIndex = i;
                    break;
                }
            }

            var total = _mainViewModel.Notes.Count;
            var earliest = total > 0 ? _mainViewModel.Notes.Min(n => n.CreatedAt).ToString("yyyy-MM-dd") : "";
            StatsText.Text = $"当前存储: {total} 条笔记";
            if (!string.IsNullOrEmpty(earliest))
                StatsText.Text += $"\n最早记录: {earliest}";
        }
        catch (Exception ex)
        {
            StatsText.Text = $"加载出错: {ex.Message}";
        }
    }

    // ─── Event Handlers ─────────────────────────────────────────

    private void AlwaysOnTop_Checked(object sender, RoutedEventArgs e)
        => _settingsViewModel.AlwaysOnTop = true;

    private void AlwaysOnTop_Unchecked(object sender, RoutedEventArgs e)
        => _settingsViewModel.AlwaysOnTop = false;

    private void WindowModeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_settingsViewModel != null)
            _settingsViewModel.MiniMode = WindowModeCombo.SelectedIndex == 1;
    }

    private void TemplateCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_settingsViewModel != null && TemplateCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            _settingsViewModel.DefaultTemplate = tag;
    }

    private async void CleanupButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "确定要删除7天前的所有记录吗？此操作不可撤销。",
            "清理记录", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            await _settingsViewModel.CleanupOldNotes();
            // Refresh stats in main VM
            await _mainViewModel.CleanupOldNotes();
            CleanupButton.IsEnabled = false;
            CleanupButton.Content = "🗑️ 已清理";
            StatsText.Text = $"当前存储: {_settingsViewModel.TotalNotes} 条笔记";
        }
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var openDialog = new OpenFileDialog
        {
            Filter = "JSON 文件 (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (openDialog.ShowDialog() == true)
        {
            try
            {
                var json = await File.ReadAllTextAsync(openDialog.FileName);
                var notes = new List<NoteItem>();

                try
                {
                    notes = JsonSerializer.Deserialize<List<NoteItem>>(json) ?? new();
                }
                catch
                {
                    var single = JsonSerializer.Deserialize<NoteItem>(json);
                    if (single != null) notes.Add(single);
                }

                if (notes.Count == 0)
                {
                    MessageBox.Show("未找到有效的笔记数据。", "导入提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                int imported = 0;
                foreach (var note in notes)
                {
                    note.Id = Guid.NewGuid().ToString("N");
                    note.UpdatedAt = null;
                    await _dataService.InsertNoteAsync(note);
                    imported++;
                }

                // Refresh main VM's collection
                var allNotes = await _dataService.GetAllNotesAsync();
                _mainViewModel.Notes.Clear();
                foreach (var n in allNotes)
                    _mainViewModel.Notes.Add(n);

                // Update stats display
                StatsText.Text = $"当前存储: {allNotes.Count} 条笔记";
                if (allNotes.Count > 0)
                    StatsText.Text += $"\n最早记录: {allNotes.Min(x => x.CreatedAt):yyyy-MM-dd}";

                MessageBox.Show($"成功导入 {imported} 条笔记。", "导入完成",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // Apply settings to main VM
        _settingsViewModel.AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked ?? true;
        _settingsViewModel.IsDraggable = DraggableCheckBox.IsChecked ?? true;
        _settingsViewModel.TrackClipboard = TrackClipboardCheckBox.IsChecked ?? false;
        _settingsViewModel.AutoStart = AutoStartCheckBox.IsChecked ?? false;

        _settingsViewModel.ApplyTo(_mainViewModel.Settings);
        await _mainViewModel.SaveSettings();

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
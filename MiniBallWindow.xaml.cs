using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using StickyNotes.Services;
using StickyNotes.ViewModels;

namespace StickyNotes;

public partial class MiniBallWindow : Window
{
    private readonly DataService _dataService;
    private readonly AutoStartService _autoStartService;
    private readonly MainViewModel _viewModel;
    private readonly ClipboardWatcher _clipboardWatcher;
    private Point _dragStart;
    private Point _windowStart;
    private bool _isDragging;

    public MiniBallWindow(
        DataService dataService,
        AutoStartService autoStartService,
        MainViewModel viewModel,
        ClipboardWatcher clipboardWatcher)
    {
        InitializeComponent();

        _dataService = dataService;
        _autoStartService = autoStartService;
        _viewModel = viewModel;
        _clipboardWatcher = clipboardWatcher;
        DataContext = _viewModel;

        // Position on right side of screen
        Left = SystemParameters.WorkArea.Width - 68;
        Top = SystemParameters.WorkArea.Height / 2 - 26;
    }

    // ─── Drag-and-drop (files / images) ───────────────────────

    private void BallBorder_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Effects = DragDropEffects.Copy;
        BallBorder.Opacity = 0.95;
        BallBorder.Background = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.3, 0.3),
            GradientStops = new GradientStopCollection
            {
                new(Color.FromRgb(0xE8, 0xF5, 0xE9), 0),
                new(Color.FromRgb(0xC8, 0xE6, 0xC9), 0.5),
                new(Color.FromRgb(0xA5, 0xD6, 0xA7), 1)
            }
        };
        e.Handled = true;
    }

    private void BallBorder_DragLeave(object sender, DragEventArgs e)
    {
        ResetBallAppearance();
        e.Handled = true;
    }

    private async void BallBorder_Drop(object sender, DragEventArgs e)
    {
        ResetBallAppearance();

        if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
            e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            var path = files[0];
            if (IsImageFile(path))
                await _viewModel.AddImageNote(path);
            else
                await _viewModel.AddFileNote(path);
        }
        e.Handled = true;
    }

    private void ResetBallAppearance()
    {
        BallBorder.Opacity = 0.7;
        BallBorder.Background = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.3, 0.3),
            GradientStops = new GradientStopCollection
            {
                new(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF), 0),
                new(Color.FromArgb(0xD8, 0xF2, 0xF2, 0xF2), 0.5),
                new(Color.FromArgb(0xC0, 0xE0, 0xE0, 0xE0), 1)
            }
        };
    }

    private static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".ico" or ".webp";
    }

    // ─── Mouse Handling (drag to move) ────────────────────────

    private double _dpiScale = 1.0;

    private double GetDpiScale()
    {
        var source = PresentationSource.FromVisual(this);
        return source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
    }

    private static Point GetCursorPosDips(double dpiScale)
    {
        GetCursorPos(out var p);
        return new Point(p.X / dpiScale, p.Y / dpiScale);
    }

    private void Ball_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dpiScale = GetDpiScale();
        _dragStart = GetCursorPosDips(_dpiScale);
        _windowStart = new Point(Left, Top);
        _isDragging = false;
        Mouse.Capture(BallBorder);
    }

    private void Ball_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            if (Mouse.Captured == BallBorder)
                Mouse.Capture(null);
            return;
        }

        var cursorDips = GetCursorPosDips(_dpiScale);
        var dx = cursorDips.X - _dragStart.X;
        var dy = cursorDips.Y - _dragStart.Y;

        if (!_isDragging)
        {
            if (Math.Abs(dx) < 2 && Math.Abs(dy) < 2) return;
            _isDragging = true;
            BallBorder.Opacity = 0.45;
        }

        Left = _windowStart.X + dx;
        Top = _windowStart.Y + dy;
    }

    private void Ball_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        Mouse.Capture(null);
        BallBorder.Opacity = 0.7;

        if (!_isDragging)
            OpenMainWindow();
        _isDragging = false;
    }

    private void Ball_PreviewRightDown(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();

        var addItem = new MenuItem { Header = "新建便利贴" };
        addItem.Click += (_, _) =>
        {
            _viewModel.CreateBlankNote();
            OpenMainWindow();
        };

        var settingsItem = new MenuItem { Header = "设置" };
        settingsItem.Click += (_, _) =>
        {
            var win = new SettingsWindow(_dataService, _autoStartService, _viewModel);
            win.Owner = this;
            win.Topmost = true;
            win.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            win.ShowDialog();
        };

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (_, _) => Application.Current.Shutdown();

        menu.Items.Add(addItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);

        BallBorder.ContextMenu = menu;
        BallBorder.ContextMenu.IsOpen = true;
    }

    // ─── Mode Switch ────────────────────────────────────────────

    private void OpenMainWindow()
    {
        _viewModel.Settings.MiniMode = false;
        _ = _viewModel.SaveSettings();
        _clipboardWatcher.Stop();
        var main = new MainWindow(_dataService, _autoStartService, _viewModel, _clipboardWatcher);
        main.Show();
        Close();
    }

    // ─── Win32 P/Invoke ─────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }
}

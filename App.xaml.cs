using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using StickyNotes.Models;
using StickyNotes.Services;
using StickyNotes.ViewModels;

namespace StickyNotes;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private DataService? _dataService;
    private AutoStartService? _autoStartService;
    private MainViewModel? _mainViewModel;
    private ClipboardWatcher? _clipboardWatcher;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handler
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            File.WriteAllText(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StickyNotes", "crash.log"),
                $"Unhandled: {args.ExceptionObject}");
        };
        DispatcherUnhandledException += (_, args) =>
        {
            File.WriteAllText(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StickyNotes", "crash.log"),
                $"Dispatcher: {args.Exception}");
            args.Handled = true;
        };

        // Initialize services
        _dataService = new DataService();
        _autoStartService = new AutoStartService();
        _mainViewModel = new MainViewModel(_dataService, _autoStartService);
        _clipboardWatcher = new ClipboardWatcher();

        await _mainViewModel.InitializeAsync();

        // Setup system tray icon
        SetupTrayIcon();

        // Show main window or mini ball
        if (_mainViewModel.Settings.MiniMode)
        {
            var miniBall = new MiniBallWindow(_dataService, _autoStartService, _mainViewModel, _clipboardWatcher);
            miniBall.Show();
        }
        else
        {
            var mainWindow = new MainWindow(_dataService, _autoStartService, _mainViewModel, _clipboardWatcher);
            mainWindow.Show();
        }
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "便利贴",
            Icon = CreateAppIcon(),
            Visibility = Visibility.Visible
        };

        // Context menu
        var contextMenu = new ContextMenu();

        var showItem = new MenuItem { Header = "显示主窗口" };
        showItem.Click += (_, _) => ShowMainWindow();
        contextMenu.Items.Add(showItem);

        var addNoteItem = new MenuItem { Header = "新建便利贴" };
        addNoteItem.Click += (_, _) => _ = _mainViewModel?.AddTextNote(null);
        contextMenu.Items.Add(addNoteItem);

        contextMenu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (_, _) =>
        {
            _dataService?.SaveSettings(_mainViewModel?.Settings ?? new AppSettings());
            _clipboardWatcher?.Dispose();
            _trayIcon?.Dispose();
            Shutdown();
        };
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextMenu = contextMenu;

        // Double-click to show main window
        _trayIcon.TrayBalloonTipClicked += (_, _) => ShowMainWindow();
        _trayIcon.DoubleClickCommand = new RelayCommand(() => ShowMainWindow());
    }

    private void ShowMainWindow()
    {
        foreach (Window window in Windows)
        {
            if (window is MainWindow)
            {
                window.Show();
                window.Activate();
                return;
            }
        }

        // No main window exists, create one using the shared services
        if (_dataService != null && _autoStartService != null && _mainViewModel != null && _clipboardWatcher != null)
        {
            var mainWindow = new MainWindow(_dataService, _autoStartService, _mainViewModel, _clipboardWatcher);
            mainWindow.Show();
        }
    }

    private static System.Drawing.Icon CreateAppIcon()
    {
        // Create a simple icon programmatically
        using var bitmap = new System.Drawing.Bitmap(16, 16);
        using var g = System.Drawing.Graphics.FromImage(bitmap);
        g.Clear(System.Drawing.Color.Transparent);
        g.FillEllipse(System.Drawing.Brushes.Yellow, 1, 1, 14, 14);
        g.DrawString("📌", new System.Drawing.Font("Segoe UI", 8),
            System.Drawing.Brushes.Black, new System.Drawing.PointF(1, 2));

        return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _dataService?.SaveSettings(_mainViewModel?.Settings ?? new AppSettings());
        _clipboardWatcher?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}

/// <summary>
/// Simple relay command for the tray icon.
/// </summary>
internal class RelayCommand(Action execute) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, System.EventArgs.Empty);
}
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Interop;

namespace StickyNotes.Services;

/// <summary>
/// Monitors clipboard changes using Win32 AddClipboardFormatListener API.
/// Provides intelligent deduplication, source-app tracking, and URL detection.
/// </summary>
public class ClipboardWatcher : IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private const int DebounceMs = 500;
    private const int MinTextLength = 2;
    private const int MaxRecentHashes = 30;
    private const int UrlFetchTimeoutMs = 3000;

    private nint _hwnd;
    private HwndSource? _hwndSource;
    private HwndSourceHook? _hook;
    private bool _disposed;

    private readonly Queue<string> _recentHashes = new();
    private readonly Dictionary<ClipboardContentType, DateTime> _lastFireTime = new();
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMilliseconds(UrlFetchTimeoutMs)
    };

    // URL detection pattern
    private static readonly Regex _urlRegex = new(
        @"^https?://[^\s]+$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public event EventHandler<ClipboardContent>? ContentChanged;

    /// <summary>
    /// Data class carrying clipboard content metadata.
    /// </summary>
    public class ClipboardContent : EventArgs
    {
        public ClipboardContentType Type { get; set; }
        public string? Text { get; set; }
        public string? SourceWindow { get; set; }
        public string? SuggestedFileName { get; set; }
    }

    public enum ClipboardContentType
    {
        None,
        Text,
        Image,
        File
    }

    public void Start(nint windowHandle)
    {
        _hwnd = windowHandle;
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hook = WndProc;
        _hwndSource.AddHook(_hook);
        AddClipboardFormatListener(_hwnd);
    }

    public void Stop()
    {
        if (_hwnd != nint.Zero && _hwndSource != null && _hook != null)
        {
            _hwndSource.RemoveHook(_hook);
            RemoveClipboardFormatListener(_hwnd);
        }
        _hwnd = nint.Zero;
        _hwndSource = null;
        _hook = null;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE)
        {
            _ = HandleClipboardChangeAsync();
        }
        return nint.Zero;
    }

    private async Task HandleClipboardChangeAsync()
    {
        await Task.Delay(100);

        try
        {
            // Get foreground window title for source tracking
            var sourceWindow = GetForegroundWindowTitle();

            // Try text first
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();

                // Skip very short content
                if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < MinTextLength)
                    return;

                // Dedup: check against recent hashes
                var hash = ComputeHash(text);
                if (_recentHashes.Contains(hash))
                    return;

                // Debounce: check last fire time
                if (IsDebounced(ClipboardContentType.Text))
                    return;

                _recentHashes.Enqueue(hash);
                if (_recentHashes.Count > MaxRecentHashes)
                    _recentHashes.Dequeue();
                _lastFireTime[ClipboardContentType.Text] = DateTime.UtcNow;

                // URL detection: try to fetch page title
                string? urlTitle = null;
                if (_urlRegex.IsMatch(text))
                {
                    urlTitle = await FetchPageTitleAsync(text);
                }

                ContentChanged?.Invoke(this, new ClipboardContent
                {
                    Type = ClipboardContentType.Text,
                    Text = text,
                    SourceWindow = sourceWindow,
                    SuggestedFileName = urlTitle
                });
                return;
            }

            // Try image
            if (Clipboard.ContainsImage())
            {
                if (IsDebounced(ClipboardContentType.Image))
                    return;
                _lastFireTime[ClipboardContentType.Image] = DateTime.UtcNow;

                ContentChanged?.Invoke(this, new ClipboardContent
                {
                    Type = ClipboardContentType.Image,
                    SourceWindow = sourceWindow
                });
                return;
            }

            // Try file drop
            if (Clipboard.ContainsFileDropList())
            {
                if (IsDebounced(ClipboardContentType.File))
                    return;
                _lastFireTime[ClipboardContentType.File] = DateTime.UtcNow;

                ContentChanged?.Invoke(this, new ClipboardContent
                {
                    Type = ClipboardContentType.File,
                    SourceWindow = sourceWindow
                });
            }
        }
        catch
        {
            // Silently ignore clipboard access errors
        }
    }

    private bool IsDebounced(ClipboardContentType type)
    {
        return _lastFireTime.TryGetValue(type, out var last)
            && (DateTime.UtcNow - last).TotalMilliseconds < DebounceMs;
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    private static async Task<string?> FetchPageTitleAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode) return null;

            var html = await response.Content.ReadAsStringAsync();
            var match = Regex.Match(html, @"<title>\s*(.*?)\s*</title>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
            {
                var title = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
                return title.Length > 200 ? title[..200] : title;
            }
        }
        catch { /* timeout / network error — silently ignore */ }
        return null;
    }

    /// <summary>
    /// Returns the title of the foreground window (the app the user is currently in).
    /// </summary>
    private static string? GetForegroundWindowTitle()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == nint.Zero) return null;
            var sb = new StringBuilder(256);
            GetWindowText(hwnd, sb, sb.Capacity);
            var title = sb.ToString().Trim();
            return string.IsNullOrEmpty(title) ? null : title;
        }
        catch { return null; }
    }

    // ─── Win32 P/Invoke ────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern bool AddClipboardFormatListener(nint hwnd);

    [DllImport("user32.dll")]
    private static extern bool RemoveClipboardFormatListener(nint hwnd);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(nint hWnd, StringBuilder text, int count);

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}

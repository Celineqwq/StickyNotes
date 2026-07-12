using Microsoft.Win32;

namespace StickyNotes.Services;

public class AutoStartService
{
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "StickyNotes";

    public bool IsAutoStartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
        if (key == null) return false;

        var value = key.GetValue(AppName);
        return value != null && value.ToString() == GetExecutablePath();
    }

    public void EnableAutoStart()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
        if (key == null) return;

        key.SetValue(AppName, GetExecutablePath());
    }

    public void DisableAutoStart()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
        if (key == null) return;

        if (key.GetValue(AppName) != null)
            key.DeleteValue(AppName);
    }

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "StickyNotes.exe");
    }
}
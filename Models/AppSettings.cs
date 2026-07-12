namespace StickyNotes.Models;

public class AppSettings
{
    public bool AlwaysOnTop { get; set; } = true;
    public bool IsDraggable { get; set; } = true;
    public bool TrackClipboard { get; set; } = false;
    public bool AutoStart { get; set; } = false;
    public bool MiniMode { get; set; } = false;
    public double WindowWidth { get; set; } = 420;
    public double WindowHeight { get; set; } = 600;
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public string DefaultTemplate { get; set; } = "Yellow";
}
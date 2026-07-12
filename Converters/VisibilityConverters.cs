using System.Globalization;
using System.Windows;
using System.Windows.Data;
using StickyNotes.Models;

namespace StickyNotes.Converters;

/// <summary>
/// Converts NoteType to Visibility based on the expected type.
/// </summary>
public class TypeToVisibilityConverter : IValueConverter
{
    public NoteType ExpectedType { get; set; }
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is NoteType type)
        {
            var isMatch = type == ExpectedType;
            if (Invert) isMatch = !isMatch;
            return isMatch ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Provides singleton converter instances for use in XAML via x:Static.
/// </summary>
public static class VisibilityConverters
{
    public static TypeToVisibilityConverter TextType { get; } = new() { ExpectedType = NoteType.Text };
    public static TypeToVisibilityConverter ImageType { get; } = new() { ExpectedType = NoteType.Image };
    public static TypeToVisibilityConverter FileType { get; } = new() { ExpectedType = NoteType.File };
    public static TypeToVisibilityConverter FileTypeVisibility { get; } = new() { ExpectedType = NoteType.File };
    public static TypeToVisibilityConverter TextTypeVisibility { get; } = new() { ExpectedType = NoteType.Text };
    public static TypeToVisibilityConverter ImageTypeVisibility { get; } = new() { ExpectedType = NoteType.Image };

    public static BoolToVisibilityConverter BoolToVisible { get; } = new();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return b ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
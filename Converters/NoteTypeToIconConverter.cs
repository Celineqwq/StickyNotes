using System.Globalization;
using System.Windows.Data;
using StickyNotes.Models;

namespace StickyNotes.Converters;

[ValueConversion(typeof(NoteType), typeof(string))]
public class NoteTypeToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is NoteType type)
        {
            return type switch
            {
                NoteType.Text => "📝",
                NoteType.Image => "🖼️",
                NoteType.File => "📄",
                _ => "📝"
            };
        }
        return "📝";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace StickyNotes.Converters;

/// <summary>
/// Converts an image file path to a BitmapImage for display.
/// Returns null if the file doesn't exist or can't be loaded.
/// </summary>
[ValueConversion(typeof(string), typeof(BitmapImage))]
public class FilePathToImageConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                var imageExts = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico", ".webp" };

                if (imageExts.Contains(ext))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(path);
                    bitmap.DecodePixelWidth = 200;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch
            {
                // File exists but can't be read as image — show nothing
            }
        }
        // Return null — the Image control will show nothing gracefully
        return null!;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
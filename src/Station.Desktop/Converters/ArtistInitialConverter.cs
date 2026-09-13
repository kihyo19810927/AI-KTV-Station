using System.Globalization;
using System.Windows.Data;

namespace Station.Desktop.Converters;

/// <summary>Provides a stable local avatar glyph while a remote artist image is absent or loading.</summary>
public sealed class ArtistInitialConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var name = value as string;
        return string.IsNullOrWhiteSpace(name) ? "♫" : name.Trim()[0].ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

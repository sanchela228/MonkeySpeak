using System.Globalization;
using Microsoft.Maui.Controls;

namespace NewAppMaui.Converters;

public class SelectedItemEqualsCurrentItemConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return false;

        var itemId = values[0]?.ToString();
        var selectedId = values[1]?.ToString();

        return itemId != null && itemId == selectedId;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class SelectedProfileToColorConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] == null || values[1] == null)
            return Colors.White;

        return values[0].ToString() == values[1].ToString()
            ? Colors.Green
            : Colors.White;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
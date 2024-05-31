using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Zafiro.Avalonia.Misc;
using Zafiro.DataModel;
using Zafiro.FileSystem;

namespace DotnetPackaging.Gui;

public class DataToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IData file)
        {
            var bytes = file.Bytes();
            return BitmapFactory.Load(bytes);
        }

        return BindingValue<Bitmap>.Unset;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
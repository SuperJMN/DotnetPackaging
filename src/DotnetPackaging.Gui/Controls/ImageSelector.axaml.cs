using Avalonia;
using Avalonia.Controls.Primitives;
using DotnetPackaging.Gui.ViewModels;

namespace DotnetPackaging.Gui.Controls;

public class ImageSelector : TemplatedControl
{
    public static readonly StyledProperty<ImageSelectorViewModel> ControllerProperty = AvaloniaProperty.Register<ImageSelector, ImageSelectorViewModel>(
        nameof(Controller));

    public ImageSelectorViewModel Controller
    {
        get => GetValue(ControllerProperty);
        set => SetValue(ControllerProperty, value);
    }

    public static readonly StyledProperty<double> MaxImageWidthProperty = AvaloniaProperty.Register<ImageSelector, double>(
        "MaxImageWidth");

    public double MaxImageWidth
    {
        get => GetValue(MaxImageWidthProperty);
        set => SetValue(MaxImageWidthProperty, value);
    }

    public static readonly StyledProperty<double> MaxImageHeightProperty = AvaloniaProperty.Register<ImageSelector, double>(
        "MaxImageHeight");

    public double MaxImageHeight
    {
        get => GetValue(MaxImageHeightProperty);
        set => SetValue(MaxImageHeightProperty, value);
    }
}
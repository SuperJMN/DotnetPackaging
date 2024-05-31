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
}
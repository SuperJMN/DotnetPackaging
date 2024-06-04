using System.Collections.ObjectModel;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI.Fields;
using static System.Text.RegularExpressions.Regex;

namespace DotnetPackaging.Gui.ViewModels;

public class OptionsViewModel : ReactiveValidationObject
{
    public OptionsViewModel(IFileSystemPicker fileSystemPicker)
    {
        Name.Validate(s => s.Length < 120, "Name can't be that long");
        Id.Validate(s => !Match(s, "/s+").Success, "Name can't contain whitespaces");
        Icon = new ImageSelectorViewModel(fileSystemPicker);
        this.IncludeValidationOf(Name);
    }
    
    public OptionsViewModel(IFileSystemPicker fileSystemPicker, OptionsViewModel optionsViewModel) : this(fileSystemPicker)
    {
        Name.Value = optionsViewModel.Name.Value;
        Id.Value = optionsViewModel.Id.Value;
        Version.Value = optionsViewModel.Version.Value;
        MainCategory = optionsViewModel.MainCategory;
        Comment.Value = optionsViewModel.Comment.Value;
        StartupWMClass.Value = optionsViewModel.StartupWMClass.Value;
        Homepage.Value = optionsViewModel.Homepage.Value;
        License.Value = optionsViewModel.License.Value;
        Summary.Value = optionsViewModel.Summary.Value;
        AdditionalCategories = new ObservableCollection<string>(optionsViewModel.AdditionalCategories);
    }

    public StringField Name { get; } = new StringField("");
    public StringField Comment { get; } = new StringField("");
    public StringField Id { get; } = new StringField("");
    public StringField StartupWMClass { get; } = new StringField("");
    public StringField Version { get; } = new StringField("");
    public StringField Homepage { get; } = new StringField("");
    public StringField License { get; } = new StringField("");
    public StringField Summary { get; } = new StringField("");
    public ImageSelectorViewModel Icon { get; }
    public string MainCategory { get; } = "";
    public ObservableCollection<string> AdditionalCategories { get; } = new();
}
﻿using System.Collections.ObjectModel;
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

    public bool IsTerminal { get; set; }
    public StringField Name { get; } = new StringField("");
    public StringField Comment { get; } = new StringField("");
    public StringField Id { get; } = new StringField("");
    public StringField StartupWMClass { get; } = new StringField("");
    public StringField Version { get; } = new StringField("");
    public StringField Homepage { get; } = new StringField("");
    public StringField License { get; } = new StringField("");
    public StringField Summary { get; } = new StringField("");
    public ImageSelectorViewModel Icon { get; }
    public string MainCategory { get; set; } = "";
    public ObservableCollection<string> AdditionalCategories { get; } = new();
}
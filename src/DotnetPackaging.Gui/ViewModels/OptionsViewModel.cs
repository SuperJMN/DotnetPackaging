﻿using ReactiveUI.Validation.Helpers;
using Zafiro.UI.Fields;
using static System.Text.RegularExpressions.Regex;

namespace DotnetPackaging.Gui.ViewModels;

public class OptionsViewModel : ReactiveValidationObject
{
    public OptionsViewModel()
    {
        Name.Validate(s => s.Length < 120, "Name can't be that long");
        Name.Validate(s => !Match(s, "/s+").Success, "Name can't contain whitespaces");
    }

    public StringField Name { get; } = new StringField("");
    public StringField Id { get; } = new StringField("");
    public StringField StartupWMClass { get; } = new StringField("");
    public StringField Version { get; } = new StringField("");
    public StringField Homepage { get; } = new StringField("");
    public StringField License { get; } = new StringField("");
    public StringField Summary { get; } = new StringField("");
}
﻿using System;
using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;

namespace DotnetPackaging.Gui.ViewModels;

public static class OptionsMixin
{
    public static async Task<Result<Options>> ToOptions(this OptionsViewModel optionsViewModel)
    {
        var maybeIcon = await optionsViewModel.Icon.File.AsMaybe().Map(Icon.FromData);
        
        return maybeIcon.MapMaybe(i => new Options
        {
            Icon = i,
            AppId = optionsViewModel.Id.Value.WhitespaceAsNone(),
            StartupWmClass= optionsViewModel.StartupWMClass.Value.WhitespaceAsNone(),
            Comment = optionsViewModel.Comment.Value.WhitespaceAsNone(),
            AppName = optionsViewModel.Name.Value.WhitespaceAsNone(),
            Version = optionsViewModel.Version.Value.WhitespaceAsNone(),
            Summary = optionsViewModel.Summary.Value.WhitespaceAsNone(),
            AdditionalCategories = Maybe.From(optionsViewModel.AdditionalCategories.Select(Enum.Parse<AdditionalCategory>)),
            MainCategory = optionsViewModel.MainCategory.WhitespaceAsNone().Bind(s => Maybe.From(Enum.Parse<MainCategory>(s)))
        });
    }

    public static Maybe<string> WhitespaceAsNone(this string str)
    {
        return string.IsNullOrWhiteSpace(str) ? Maybe.None : str.AsMaybe();
    }
}
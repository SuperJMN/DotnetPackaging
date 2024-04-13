using System.CommandLine;
using System.CommandLine.Binding;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;

namespace DotnetPackaging.Console;

public class SingleDirMetadataBinder : BinderBase<SingleDirMetadata>
{
    private readonly List<Option> _options;
    
    private readonly Option<string> _nameOption;
    private readonly Option<string> _startupWmClassOption;
    private readonly Option<IEnumerable<string>> _keywordsOption;
    private readonly Option<string> _commentOption;
    private readonly Option<IEnumerable<string>> _categoriesOption;
    private readonly Option<IIcon> _iconOption;

    public SingleDirMetadataBinder(
        Option<string> nameOption,
        Option<string> startupWmClassOption,
        Option<IEnumerable<string>> keywordsOption,
        Option<string> commentOption,
        Option<IEnumerable<string>> categoriesOption, 
        Option<IIcon> iconOption)
    {
        _nameOption = nameOption;
        _startupWmClassOption = startupWmClassOption;
        _keywordsOption = keywordsOption;
        _commentOption = commentOption;
        _categoriesOption = categoriesOption;
        _iconOption = iconOption;
        
        _options = new List<Option>
        {
            nameOption,
            startupWmClassOption,
            keywordsOption,
            commentOption,
            categoriesOption
        };
    }

    protected override SingleDirMetadata GetBoundValue(BindingContext bindingContext)
    {
        // If all values in the bindingContext are null, return null
        if (bindingContext.AllValuesAreNull(_options))
        {
            return null;
        }

        return new SingleDirMetadata
        {
            AppName = Maybe.From(bindingContext.ParseResult.GetValueForOption(_nameOption)),
            StartupWmClass = Maybe.From(bindingContext.ParseResult.GetValueForOption(_startupWmClassOption)),
            Keywords = Maybe.From(bindingContext.ParseResult.GetValueForOption(_keywordsOption)),
            Comment = Maybe.From(bindingContext.ParseResult.GetValueForOption(_commentOption)),
            Categories = Maybe.From(bindingContext.ParseResult.GetValueForOption(_categoriesOption)),
            Icon = Maybe<IIcon>.From(bindingContext.ParseResult.GetValueForOption(_iconOption))
        };
    }
}

public class IconBinder : BinderBase<IIcon>

{
    protected override IIcon GetBoundValue(BindingContext bindingContext) => throw new NotImplementedException();
}
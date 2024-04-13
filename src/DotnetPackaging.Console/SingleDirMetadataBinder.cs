using System.CommandLine;
using System.CommandLine.Binding;
using DotnetPackaging.AppImage.Core;

namespace DotnetPackaging.Console;

public class SingleDirMetadataBinder : BinderBase<SingleDirMetadata>
{
    private readonly List<Option> _options;
    
    private readonly Option<string> _nameOption;
    private readonly Option<string> _startupWmClassOption;
    private readonly Option<List<string>> _keywordsOption;
    private readonly Option<string> _commentOption;
    private readonly Option<List<string>> _categoriesOption;

    public SingleDirMetadataBinder(
        Option<string> nameOption,
        Option<string> startupWmClassOption,
        Option<List<string>> keywordsOption,
        Option<string> commentOption,
        Option<List<string>> categoriesOption)
    {
        _nameOption = nameOption;
        _startupWmClassOption = startupWmClassOption;
        _keywordsOption = keywordsOption;
        _commentOption = commentOption;
        _categoriesOption = categoriesOption;
        
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
            AppName = bindingContext.ParseResult.GetValueForOption(_nameOption),
            StartupWmClass = bindingContext.ParseResult.GetValueForOption(_startupWmClassOption),
            Keywords = bindingContext.ParseResult.GetValueForOption(_keywordsOption),
            Comment = bindingContext.ParseResult.GetValueForOption(_commentOption),
            Categories = bindingContext.ParseResult.GetValueForOption(_categoriesOption),
        };
    }
}
using System.CommandLine;
using System.CommandLine.Binding;
using DotnetPackaging.AppImage.Core;

namespace DotnetPackaging.Console;

public class DesktopMetadataBinder : BinderBase<SingleDirMetadata>
{
    private readonly Option<string> _nameOption;
    private readonly Option<string> _startupWmClassOption;
    private readonly Option<List<string>> _keywordsOption;
    private readonly Option<string> _commentOption;
    private readonly Option<List<string>> _categoriesOption;

    public DesktopMetadataBinder(
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
    }

    protected override SingleDirMetadata GetBoundValue(BindingContext bindingContext)
    {
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
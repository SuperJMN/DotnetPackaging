namespace DotnetPackaging.Console;

using System.CommandLine;
using System.CommandLine.Binding;
using System.Collections.Generic;
using DotnetPackaging.AppImage.Core;

using System.CommandLine;
using System.CommandLine.Binding;
using System.Collections.Generic;
using System.CommandLine.Parsing;

using System.CommandLine;
using System.CommandLine.Binding;
using System.Collections.Generic;

public class DesktopMetadataBinder : BinderBase<DesktopMetadata>
{
    private readonly Option<string> _nameOption;
    private readonly Option<string> _startupWmClassOption;
    private readonly Option<IEnumerable<string>> _keywordsOption;
    private readonly Option<string> _commentOption;
    private readonly Option<IEnumerable<string>> _categoriesOption;
    private readonly Option<string> _executablePathOption;

    public DesktopMetadataBinder(
        Option<string> nameOption,
        Option<string> startupWmClassOption,
        Option<IEnumerable<string>> keywordsOption,
        Option<string> commentOption,
        Option<IEnumerable<string>> categoriesOption,
        Option<string> executablePathOption)
    {
        _nameOption = nameOption;
        _startupWmClassOption = startupWmClassOption;
        _keywordsOption = keywordsOption;
        _commentOption = commentOption;
        _categoriesOption = categoriesOption;
        _executablePathOption = executablePathOption;
    }

    protected override DesktopMetadata GetBoundValue(BindingContext bindingContext)
    {
        // Verifica si se proporcionó alguna opción
        var anyOptionProvided = new Option[] { _nameOption, _startupWmClassOption, _keywordsOption, _commentOption, _categoriesOption, _executablePathOption }
            .Any(option => bindingContext.ParseResult.HasOption(option));

        // Si no se proporcionó ninguna opción, devuelve null
        if (!anyOptionProvided)
        {
            return null;
        }

        // Verifica si todas las opciones fueron proporcionadas
        var allOptionsPresent = new Option[] { _nameOption, _startupWmClassOption, _keywordsOption, _commentOption, _categoriesOption, _executablePathOption }
            .All(option => bindingContext.ParseResult.HasOption(option));

        // Si no todas las opciones están presentes, lanza una excepción
        if (!allOptionsPresent)
        {
            throw new InvalidOperationException("Todos los valores deben ser proporcionados para crear DesktopMetadata, o ninguno.");
        }

        // Si todas las opciones están presentes, crea y devuelve el objeto DesktopMetadata
        return new DesktopMetadata
        {
            Name = bindingContext.ParseResult.GetValueForOption(_nameOption),
            StartupWmClass = bindingContext.ParseResult.GetValueForOption(_startupWmClassOption),
            Keywords = bindingContext.ParseResult.GetValueForOption(_keywordsOption),
            Comment = bindingContext.ParseResult.GetValueForOption(_commentOption),
            Categories = bindingContext.ParseResult.GetValueForOption(_categoriesOption),
            ExecutablePath = bindingContext.ParseResult.GetValueForOption(_executablePathOption)
        };
    }
}


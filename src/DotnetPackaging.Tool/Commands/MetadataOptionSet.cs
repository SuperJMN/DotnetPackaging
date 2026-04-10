using System.CommandLine;

namespace DotnetPackaging.Tool.Commands;

public class MetadataOptionSet
{
    public Option<string> ApplicationName { get; }
    public Option<string> WmClass { get; }
    public Option<MainCategory?> MainCategory { get; }
    public Option<IEnumerable<AdditionalCategory>> AdditionalCategories { get; }
    public Option<IEnumerable<string>> Keywords { get; }
    public Option<string> Comment { get; }
    public Option<string> Version { get; }
    public Option<Uri> HomePage { get; }
    public Option<string> License { get; }
    public Option<IEnumerable<Uri>> ScreenshotUrls { get; }
    public Option<string> Summary { get; }
    public Option<string> AppId { get; }
    public Option<string> ExecutableName { get; }
    public Option<bool> IsTerminal { get; }
    public Option<IIcon?> Icon { get; }

    public MetadataOptionSet()
    {
        ApplicationName = new Option<string>("--application-name") { Description = "Application name", Required = false };
        ApplicationName.Aliases.Add("--productName");
        ApplicationName.Aliases.Add("--appName");
        WmClass = new Option<string>("--wm-class") { Description = "Startup WM Class", Required = false };
        MainCategory = new Option<MainCategory?>("--main-category") { Description = "Main category", Required = false, Arity = ArgumentArity.ZeroOrOne };
        AdditionalCategories = new Option<IEnumerable<AdditionalCategory>>("--additional-categories") { Description = "Additional categories", Required = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        Keywords = new Option<IEnumerable<string>>("--keywords") { Description = "Keywords", Required = false, Arity = ArgumentArity.ZeroOrMore, AllowMultipleArgumentsPerToken = true };
        Comment = new Option<string>("--comment") { Description = "Comment", Required = false };
        Version = new Option<string>("--version") { Description = "Version", Required = false };
        HomePage = new Option<Uri>("--homepage") { Description = "Home page of the application", Required = false };
        HomePage.CustomParser = OptionsBinder.GetUri;
        License = new Option<string>("--license") { Description = "License of the application", Required = false };
        ScreenshotUrls = new Option<IEnumerable<Uri>>("--screenshot-urls") { Description = "Screenshot URLs", Required = false };
        ScreenshotUrls.CustomParser = OptionsBinder.GetUris;
        Summary = new Option<string>("--summary") { Description = "Summary. Short description that should not end in a dot.", Required = false };
        AppId = new Option<string>("--appId") { Description = "Application Id. Usually a Reverse DNS name like com.SomeCompany.SomeApplication", Required = false };
        ExecutableName = new Option<string>("--executable-name") { Description = "Name of your application's executable", Required = false };
        IsTerminal = new Option<bool>("--is-terminal") { Description = "Indicates whether your application is a terminal application", Required = false };
        Icon = new Option<IIcon?>("--icon") { Required = false, Description = "Path to the application icon" };
        Icon.CustomParser = OptionsBinder.GetIcon;
    }

    public void AddTo(Command command)
    {
        command.Add(ApplicationName);
        command.Add(WmClass);
        command.Add(MainCategory);
        command.Add(AdditionalCategories);
        command.Add(Keywords);
        command.Add(Comment);
        command.Add(Version);
        command.Add(HomePage);
        command.Add(License);
        command.Add(ScreenshotUrls);
        command.Add(Summary);
        command.Add(AppId);
        command.Add(ExecutableName);
        command.Add(IsTerminal);
        command.Add(Icon);
    }

    public OptionsBinder CreateBinder(Option<bool>? defaultLayout = null) =>
        new(ApplicationName, WmClass, Keywords, Comment, MainCategory,
            AdditionalCategories, Icon, Version, HomePage, License, ScreenshotUrls,
            Summary, AppId, ExecutableName, IsTerminal, defaultLayout);
}

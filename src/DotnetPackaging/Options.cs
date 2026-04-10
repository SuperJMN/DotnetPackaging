namespace DotnetPackaging;

public class Options
{
    public Maybe<string> Name { get; set; }
    public Maybe<string> Id { get; set; }
    public Maybe<string> Version { get; set; }
    public Maybe<MainCategory> MainCategory { get; set; }
    public Maybe<string> StartupWmClass { get; set; }
    public Maybe<IEnumerable<string>> Keywords { get; set; }
    public Maybe<string> Comment { get; set; }
    public Maybe<IEnumerable<AdditionalCategory>> AdditionalCategories { get; set; }
    public Maybe<IIcon> Icon { get; set; }
    public Maybe<Uri> HomePage { get; set; }
    public Maybe<string> License { get; set; }
    public Maybe<IEnumerable<Uri>> ScreenshotUrls { get; set; }
    public Maybe<string> Summary { get; set; }
    public Maybe<string> ExecutableName { get; set; }
    public Maybe<bool> IsTerminal { get; set; }
    public Maybe<bool> UseDefaultLayout { get; set; }
    public Maybe<bool> IsService { get; set; }
    public Maybe<ServiceType> ServiceType { get; set; }
    public Maybe<RestartPolicy> ServiceRestart { get; set; }
    public Maybe<string> ServiceUser { get; set; }
    public Maybe<IEnumerable<string>> ServiceEnvironment { get; set; }
}

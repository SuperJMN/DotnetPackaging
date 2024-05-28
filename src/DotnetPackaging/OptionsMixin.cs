namespace DotnetPackaging;

public static class OptionsMixin
{
    public static void From(this FromDirectoryOptions setup, Options options)
    {
        if (setup == null)
        {
            throw new ArgumentNullException(nameof(setup));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.ExecutableName.HasValue)
        {
            setup.WithExecutableName(options.ExecutableName.Value);
        }
        if (options.Icon.HasValue)
        {
            setup.WithIcon(options.Icon.Value);
        }
        if (options.AppName.HasValue)
        {
            setup.WithAppName(options.AppName.Value);
        }
        if (options.StartupWmClass.HasValue)
        {
            setup.WithStartupWmClass(options.StartupWmClass.Value);
        }
        if (options.Comment.HasValue)
        {
            setup.WithComment(options.Comment.Value);
        }
        if (options.HomePage.HasValue)
        {
            setup.WithHomepage(options.HomePage.Value);
        }
        if (options.License.HasValue)
        {
            setup.WithLicense(options.License.Value);
        }
        if (options.ScreenshotUrls.HasValue)
        {
            setup.WithScreenshotUrls(options.ScreenshotUrls.Value);
        }
        if (options.Summary.HasValue)
        {
            setup.WithSummary(options.Summary.Value);
        }
        if (options.Keywords.HasValue)
        {
            setup.WithKeywords(options.Keywords.Value);
        }
        if (options.Version.HasValue)
        {
            setup.WithVersion(options.Version.Value);
        }
        if (options.MainCategory.HasValue)
        {
            var categories = new Categories(options.MainCategory.Value,
                options.AdditionalCategories.GetValueOrDefault(Array.Empty<AdditionalCategory>()).ToArray());
            setup.WithCategories(categories);
        }
    }
}
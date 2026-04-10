namespace DotnetPackaging;

public static class FromDirectoryOptionsExtensions
{
    public static FromDirectoryOptions ApplyOverrides(this FromDirectoryOptions target, FromDirectoryOptions source)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (source.Package.HasValue) target.WithPackage(source.Package.Value);
        if (source.Id.HasValue) target.WithId(source.Id.Value);
        if (source.ExecutableName.HasValue) target.WithExecutableName(source.ExecutableName.Value);
        if (source.Architecture.HasValue) target.WithArchitecture(source.Architecture.Value);
        if (source.Icon.HasValue) target.WithIcon(source.Icon.Value);
        if (source.Name.HasValue) target.WithName(source.Name.Value);
        if (source.Categories.HasValue) target.WithCategories(source.Categories.Value);
        if (source.StartupWmClass.HasValue) target.WithStartupWmClass(source.StartupWmClass.Value);
        if (source.Comment.HasValue) target.WithComment(source.Comment.Value);
        if (source.Description.HasValue) target.WithDescription(source.Description.Value);
        if (source.Homepage.HasValue) target.WithHomepage(source.Homepage.Value);
        if (source.License.HasValue) target.WithLicense(source.License.Value);
        if (source.Priority.HasValue) target.WithPriority(source.Priority.Value);
        if (source.ScreenshotUrls.HasValue) target.WithScreenshotUrls(source.ScreenshotUrls.Value);
        if (source.Maintainer.HasValue) target.WithMaintainer(source.Maintainer.Value);
        if (source.Summary.HasValue) target.WithSummary(source.Summary.Value);
        if (source.Keywords.HasValue) target.WithKeywords(source.Keywords.Value);
        if (source.Recommends.HasValue) target.WithRecommends(source.Recommends.Value);
        if (source.Section.HasValue) target.WithSection(source.Section.Value);
        if (source.Version.HasValue) target.WithVersion(source.Version.Value);
        if (source.VcsBrowser.HasValue) target.WithVcsBrowser(source.VcsBrowser.Value);
        if (source.VcsGit.HasValue) target.WithVcsGit(source.VcsGit.Value);
        if (source.InstalledSize.HasValue) target.WithInstalledSize(source.InstalledSize.Value);
        if (source.ModificationTime.HasValue) target.WithModificationTime(source.ModificationTime.Value);
        if (source.ProjectMetadata.HasValue) target.WithProjectMetadata(source.ProjectMetadata.Value);
        if (source.Vendor.HasValue) target.WithVendor(source.Vendor.Value);
        if (source.Url.HasValue) target.WithUrl(source.Url.Value);
        if (source.IsTerminal.HasValue) target.WithIsTerminal(source.IsTerminal.Value);
        if (source.Service.HasValue)
        {
            target.WithService(svc =>
            {
                var src = source.Service.Value;
                if (src.Type.HasValue) svc.WithType(src.Type.Value);
                if (src.Restart.HasValue) svc.WithRestart(src.Restart.Value);
                if (src.RestartSec.HasValue) svc.WithRestartSec(src.RestartSec.Value);
                if (src.User.HasValue) svc.WithUser(src.User.Value);
                if (src.Group.HasValue) svc.WithGroup(src.Group.Value);
                if (src.After.HasValue) svc.WithAfter(src.After.Value);
                if (src.WantedBy.HasValue) svc.WithWantedBy(src.WantedBy.Value);
                if (src.Environment.HasValue) svc.WithEnvironment(src.Environment.Value.ToArray());
            });
        }

        return target;
    }
}

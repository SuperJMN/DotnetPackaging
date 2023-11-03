using System.Collections.ObjectModel;

namespace DotnetPackaging.Deb;

public class Contents : Collection<Content>
{
    public IEnumerable<Content> Entries => this;
}
using System.Collections.ObjectModel;

namespace DotnetPackaging.Old.Deb;

public class Contents : Collection<Content>
{
    public Contents()
    {
    }

    public Contents(IEnumerable<Content> contents) : base(contents.ToList())
    {
    }
}
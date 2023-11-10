using System.Collections.ObjectModel;

namespace DotnetPackaging.New.Archives.Deb.Contents;

public class ContentCollection : Collection<Content>
{
    public ContentCollection()
    {
    }

    public ContentCollection(IEnumerable<Content> contents) : base(contents.ToList())
    {
    }
}
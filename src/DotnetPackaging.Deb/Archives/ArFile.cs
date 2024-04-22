using CSharpFunctionalExtensions;
using System.Xml.Linq;

namespace DotnetPackaging.Deb.Archives;

public record ArFile(params Entry[] Entries)
{
    public void Accept(IArFileVisitor visitor)
    {
        visitor.Visit(this);
    }
};

public interface IArFileVisitor
{
    Task Visit(ArFile arFile);
    Task Visit(Entry entry);
    Task Visit(Properties properties);
}

public record Entry(string Name, Properties Properties, Func<Stream> ContentStreamFactory)
{
    public void Accept(IArFileVisitor visitor) => visitor.Visit(this);
}

public record Properties
{
    public required DateTimeOffset LastModification { get; init; }
    public required FileMode FileMode { get; init; }
    public required Maybe<int> OwnerId { get; init; }
    public required Maybe<int> GroupId { get; init; }

    public void Accept(IArFileVisitor arFile)
    {
        arFile.Visit(this);
    }
}
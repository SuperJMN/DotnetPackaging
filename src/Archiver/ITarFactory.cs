using CSharpFunctionalExtensions;

namespace Archiver;

public interface ITarFactory
{
    public ITarFile Create();
}
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Msix.Core;
using DotnetPackaging.Msix.Core.Manifest;

namespace DotnetPackaging.Msix;

public class Msix
{
    public static Result<IByteSource> FromDirectory(IDirectory directory, Maybe<ILogger> logger)
    {
        return new MsixPackager(logger).Pack(directory);
    }
    
    public static Result<IByteSource> FromDirectoryAndMetadata(IDirectory directory, AppManifestMetadata metadata, Maybe<ILogger> logger)
    {
        var generateAppManifest = AppManifestGenerator.GenerateAppManifest(metadata);
        var appxManifiest = ByteSource.FromString(generateAppManifest, Encoding.UTF8);
        var dir = Directory.Create("metadata", new File("AppxManifest.xml", appxManifiest));
        
        var merged = Dir.Combine("merged", directory, dir);
        return new MsixPackager(logger).Pack(merged);
    }
}

public class Dir
{
 public static Directory Combine(string newName, IDirectory a, IDirectory b)
    {
        var combinedChildren = new List<INamed>();
        var directoriesByName = new Dictionary<string, List<IDirectory>>();
        
        // Primero, agrupar los subdirectorios por nombre
        foreach (var child in a.Children.Concat(b.Children))
        {
            if (child is IDirectory dir)
            {
                if (!directoriesByName.ContainsKey(dir.Name))
                {
                    directoriesByName[dir.Name] = new List<IDirectory>();
                }
                directoriesByName[dir.Name].Add(dir);
            }
            else
            {
                // Si es un archivo, agregarlo directamente
                combinedChildren.Add(child);
            }
        }
        
        // Ahora combinar los subdirectorios con el mismo nombre
        foreach (var entry in directoriesByName)
        {
            string dirName = entry.Key;
            List<IDirectory> dirs = entry.Value;
            
            if (dirs.Count == 1)
            {
                // Si solo hay un directorio con este nombre, agregarlo directamente
                combinedChildren.Add(dirs[0]);
            }
            else
            {
                // Si hay múltiples directorios con el mismo nombre, combinarlos recursivamente
                var combinedDir = Combine(dirName, dirs[0], dirs[1]);
                
                // Si hay más de dos, combinar los restantes
                for (int i = 2; i < dirs.Count; i++)
                {
                    combinedDir = Combine(dirName, combinedDir, dirs[i]);
                }
                
                combinedChildren.Add(combinedDir);
            }
        }
        
        return new Directory(newName, combinedChildren.ToArray());
    }
}

public class Directory : IDirectory
{
    public string Name { get; }
    public IEnumerable<INamed> Children { get; }
    
    // Constructor privado para la implementación interna
    private Directory(string name, IEnumerable<INamed> children)
    {
        Name = name;
        Children = children.ToList();
    }
    
    // Método de fábrica estático que permite sintaxis fluida
    public static Directory Create(string name, params INamed[] contents)
    {
        return new Directory(name, contents);
    }
    
    // Constructor público que permite la sintaxis sugerida
    public Directory(string name, params INamed[] contents)
    {
        Name = name;
        Children = contents.ToList();
    }
    
    // Métodos para mostrar la estructura
    public override string ToString()
    {
        return $"Directory: {Name} ({Children.Count()} items)";
    }
}

public class File(string name, IByteSource source) : INamedByteSource
{
    public string Name => name;

    public IDisposable Subscribe(IObserver<byte[]> observer)
    {
        return source.Subscribe(observer);
    }

    public Task<Maybe<long>> GetLength()
    {
        return source.GetLength();
    }

    public IObservable<byte[]> Bytes => source.Bytes;
}
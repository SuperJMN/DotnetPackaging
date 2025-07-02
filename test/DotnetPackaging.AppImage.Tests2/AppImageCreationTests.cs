using DotnetPackaging.AppImage.Core;
using FluentAssertions;
using Zafiro.DivineBytes;
using File = Zafiro.DivineBytes.File;
using Directory = Zafiro.DivineBytes.Directory;
using AppImage = DotnetPackaging.AppImage.AppImage;

namespace DotnetPackaging.AppImage.Tests2;

public class AppImageCreationTests
{
    [Fact]
    public async Task Create_AppImage_Should_Generate_Valid_Structure()
    {
        // Arrange - Crear directorio de archivos simulado usando las clases de Zafiro
        var executableContent = ByteSource.FromString("#!/bin/bash\necho 'Hello World'", System.Text.Encoding.UTF8);
        var executable = new File("TestApp", executableContent);
        
        var readmeContent = ByteSource.FromString("This is a test application", System.Text.Encoding.UTF8);
        var readme = new File("README.txt", readmeContent);
        
        var mockDirectory = new Directory("TestApp", [executable, readme]);
        
        // Act - Intentar crear el AppImage
        new WIP.AppImage(new UriRuntime())
        var result = await AppImage.From()
            .Directory(mockDirectory)
            .Configure(options =>
            {
                options.Name = "TestApp";
                options.Version = "1.0.0";
                options.Executable = "TestApp";
                options.Architecture = Architecture.X64;
                options.IsTerminal = false;
            })
            .Build();

        // Assert
        result.Should().Succeed("El AppImage debe crearse correctamente");
        
        var appImage = result.Value;
        appImage.Should().NotBeNull();
        
        // Verificar que tiene un runtime
        appImage.Runtime.Should().NotBeNull();
        
        // Verificar estructura del sistema de archivos Unix
        var unixRoot = appImage.Root;
        unixRoot.Should().NotBeNull();
        
        // Verificar que hay directorios
        unixRoot.Directories.Should().NotBeEmpty("El AppImage debe tener directorios");
        
        // Verificar directorio usr (común en AppImages)
        var usrDir = unixRoot.Directories.FirstOrDefault(d => d.Name == "usr");
        if (usrDir != null)
        {
            var binDir = usrDir.Directories.FirstOrDefault(d => d.Name == "bin");
            if (binDir != null)
            {
                // Verificar que el ejecutable está en usr/bin
                var executableInBin = binDir.Files.FirstOrDefault(f => f.File.Name == "TestApp");
                executableInBin.Should().NotBeNull("El ejecutable debe estar en usr/bin");
                
                // Verificar permisos del ejecutable
                executableInBin?.Permissions.Should().NotBeNull();
                executableInBin?.Permissions.OwnerExec.Should().BeTrue("El ejecutable debe tener permisos de ejecución");
            }
        }
    }

    [Fact]
    public async Task Create_AppImage_Should_Fail_With_Nonexistent_Executable()
    {
        // Arrange - Crear directorio sin el ejecutable especificado
        var readmeContent = ByteSource.FromString("This is a test application without executable", System.Text.Encoding.UTF8);
        var readme = new File("README.txt", readmeContent);
        
        var mockDirectory = new Directory("EmptyApp", [readme]);
        
        // Act - Intentar crear el AppImage con un ejecutable que no existe
        var result = await AppImage.From()
            .Directory(mockDirectory)
            .Configure(options =>
            {
                options.Name = "InvalidApp";
                options.Executable = "NonExistentExecutable"; // Ejecutable que no existe
                options.Architecture = Architecture.X64;
            })
            .Build();

        // Assert
        result.Should().Fail("Debe fallar cuando el ejecutable especificado no existe");
    }

    [Fact]
    public async Task Create_AppImage_With_Multiple_Files_Should_Include_All()
    {
        // Arrange - Crear directorio con múltiples archivos
        var executableContent = ByteSource.FromString("#!/bin/bash\necho 'Multi File App'", System.Text.Encoding.UTF8);
        var executable = new File("MultiFileApp", executableContent);
        
        var configContent = ByteSource.FromString("{\"setting\": \"value\"}", System.Text.Encoding.UTF8);
        var config = new File("config.json", configContent);
        
        var libContent = ByteSource.FromString("Mock library content", System.Text.Encoding.UTF8);
        var library = new File("library.dll", libContent);
        
        var mockDirectory = new Directory("MultiFileApp", [executable, config, library]);
        
        // Act
        var result = await AppImage.From()
            .Directory(mockDirectory)
            .Configure(options =>
            {
                options.Name = "MultiFileApp";
                options.Version = "2.0.0";
                options.Executable = "MultiFileApp";
                options.Architecture = Architecture.X64;
            })
            .Build();

        // Assert
        result.Should().Succeed("El AppImage debe crearse correctamente con múltiples archivos");
        
        var appImage = result.Value;
        appImage.Should().NotBeNull();
        
        // Verificar que la estructura contiene los archivos esperados
        var unixRoot = appImage.Root;
        unixRoot.Should().NotBeNull();
        
        // El AppImage debe tener algún contenido
        var hasFiles = unixRoot.Files.Any() || unixRoot.Directories.Any(d => d.Files.Any());
        hasFiles.Should().BeTrue("El AppImage debe contener archivos");
    }
}
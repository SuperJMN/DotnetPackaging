namespace DotnetPackaging.AppImage.Tests;

public static class Mixin
{
    public static Task<Result> ToFile(this Stream stream, IZafiroFile file)
    {
        return file.SetData(stream);
    }
        
  
}
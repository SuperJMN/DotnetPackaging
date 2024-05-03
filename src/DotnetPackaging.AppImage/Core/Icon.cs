using CSharpFunctionalExtensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace DotnetPackaging.AppImage.Core;

//public class Icon : IIcon
//{
//    public Icon(Func<Task<Result<Stream>>> getFileStream)
//    {
//        Open = () =>
//        {
//            return getFileStream().Bind(async stream =>
//            {
//                await using (stream)
//                {
//                    var image = await Image.LoadAsync(stream);

//                    if (image.HasProperIconSize())
//                    {
//                        return await getFileStream();
//                    }
//                    else
//                    {
//                        var memoryStream = new MemoryStream();
//                        var resize = image.MakeAppIcon();
//                        await resize.SaveAsync(memoryStream, new PngEncoder());
//                        memoryStream.Position = 0;
//                        return memoryStream;
//                    }
//                }
//            });
//        };
//    }

//    public Func<Task<Result<Stream>>> Open { get; }
//}
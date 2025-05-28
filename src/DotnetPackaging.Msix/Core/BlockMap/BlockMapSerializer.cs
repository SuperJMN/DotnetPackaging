using System.Security.Cryptography;
using System.Text;
using System.Xml;
using CSharpFunctionalExtensions;
using Zafiro.Mixins;

namespace DotnetPackaging.Msix.Core.BlockMap;

public class BlockMapSerializer(Maybe<ILogger> logger)
{
    public async Task<string> GenerateBlockMapXml(BlockMapModel model)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            Indent = true,
            OmitXmlDeclaration = true,
            Async = true,
        };

        var sb = new StringBuilder();

        // Primero escribimos la declaración XML manualmente
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>\n");

        // Luego iniciamos el XmlWriter
        using (var writer = XmlWriter.Create(sb, settings))
        {
            // Escribimos la etiqueta BlockMap con su namespace predeterminado
            writer.WriteStartElement("BlockMap", "http://schemas.microsoft.com/appx/2010/blockmap");

            // Añadimos los demás atributos en el orden correcto
            await writer.WriteAttributeStringAsync("xmlns", "b4", null, "http://schemas.microsoft.com/appx/2021/blockmap");
            writer.WriteAttributeString("IgnorableNamespaces", "b4");
            writer.WriteAttributeString("HashMethod", "http://www.w3.org/2001/04/xmlenc#sha256");

            // Iterar sobre los archivos
            foreach (var fileInfo in model.Files)
            {
                writer.WriteStartElement("File");
                writer.WriteAttributeString("Name", fileInfo.Entry.FullPath.Replace("/", "\\"));
                logger.Debug("Calculating size for {FileName}", fileInfo.Entry.FullPath);
                var size = await fileInfo.Entry.Original.GetSize();
                writer.WriteAttributeString("Size", size.ToString());
                writer.WriteAttributeString("LfhSize", GetLhsSize(fileInfo.Entry).ToString());

                // Escribir bloques
                foreach (var block in fileInfo.Blocks)
                {
                    writer.WriteStartElement("Block");
                    logger.Debug("Calculating hash for {FileName}", fileInfo.Entry.FullPath);
                    writer.WriteAttributeString("Hash", Convert.ToBase64String(SHA256.HashData(block.OriginalData)));

                    if (fileInfo.Entry.CompressionLevel != CompressionLevel.NoCompression)
                    {
                        writer.WriteAttributeString("Size", block.CompressedData.Length.ToString());
                    }

                    await writer.WriteEndElementAsync(); // Block
                }

                // Incluir el hash del archivo completo si está disponible
                if (fileInfo.Blocks.Count > 1)
                {
                    await writer.WriteStartElementAsync("b4", "FileHash", "http://schemas.microsoft.com/appx/2021/blockmap");
                    writer.WriteAttributeString("Hash", Convert.ToBase64String(await fileInfo.Entry.Original.Sha256()));
                    await writer.WriteEndElementAsync(); // b4:FileHash
                }

                await writer.WriteEndElementAsync(); // File
            }

            await writer.WriteEndElementAsync(); // BlockMap
        }

        // Quitar los espacios antes de los cierres de etiqueta auto-cerrantes
        string xmlString = sb.ToString();

        return xmlString;
    }

    private int GetLhsSize(MsixEntry entry)
    {
        return 30 + Encoding.UTF8.GetByteCount(entry.FullPath);
    }
}
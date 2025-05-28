using System.IO.Compression;
using System.Text;

namespace MsixPackaging.Tests.Research;

public class Tools
{
    public static byte[] ExtractRawCompressedBytes(string zipPath, string entryName)
    {
        Console.WriteLine($"Intentando extraer: '{entryName}' de '{zipPath}'");

        using (FileStream fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read))
        using (BinaryReader reader = new BinaryReader(fs))
        {
            // Escanear el archivo secuencialmente buscando local file headers
            while (fs.Position < fs.Length - 4)
            {
                // Buscar firma de local file header
                uint signature = reader.ReadUInt32();

                if (signature == 0x04034b50) // Local file header signature
                {
                    // Guardar la posición del inicio del header
                    long headerStart = fs.Position - 4;

                    // Leer campos del local file header
                    ushort versionNeeded = reader.ReadUInt16();
                    ushort generalPurposeFlag = reader.ReadUInt16();
                    ushort compressionMethod = reader.ReadUInt16();
                    ushort lastModTime = reader.ReadUInt16();
                    ushort lastModDate = reader.ReadUInt16();
                    uint crc32 = reader.ReadUInt32();
                    uint headerCompressedSize = reader.ReadUInt32();
                    uint headerUncompressedSize = reader.ReadUInt32();
                    ushort fileNameLength = reader.ReadUInt16();
                    ushort extraFieldLength = reader.ReadUInt16();

                    // Leer el nombre del archivo
                    byte[] fileNameBytes = reader.ReadBytes(fileNameLength);
                    string fileName = Encoding.UTF8.GetString(fileNameBytes);

                    Console.WriteLine(
                        $"Encontrado local header para: '{fileName}', Flag: 0x{generalPurposeFlag:X4}, CompSize: {headerCompressedSize}");

                    // Saltar el extra field
                    fs.Position += extraFieldLength;

                    // Verificar si es la entrada que buscamos
                    if (fileName == entryName)
                    {
                        Console.WriteLine($"Coincidencia encontrada para '{entryName}'");

                        // Verificar si el bit 3 está activado (data descriptor presente)
                        bool hasDataDescriptor = (generalPurposeFlag & 0x0008) != 0;
                        Console.WriteLine($"¿Tiene data descriptor? {hasDataDescriptor}");

                        // Posición donde comienzan los datos comprimidos
                        long dataPosition = fs.Position;

                        // Si no hay data descriptor y el tamaño comprimido es conocido, simplemente leemos los bytes
                        if (!hasDataDescriptor && headerCompressedSize > 0)
                        {
                            Console.WriteLine(
                                $"Leyendo {headerCompressedSize} bytes comprimidos desde la posición {dataPosition}");
                            return reader.ReadBytes((int) headerCompressedSize);
                        }
                        else
                        {
                            // Si hay data descriptor o el tamaño es 0, debemos encontrar el data descriptor
                            // 1. Guardar la posición actual para volver después
                            long currentPos = fs.Position;

                            // 2. Escanear hacia adelante buscando la firma del data descriptor o del siguiente local header
                            uint dataDescriptorSize;

                            // Primero intentamos encontrar un data descriptor con firma (opcional según la especificación)
                            // Si no lo encontramos, buscaremos el próximo local header
                            fs.Position = dataPosition;
                            byte[] scanBuffer = new byte[1024];
                            int bytesRead;
                            long scanPos = dataPosition;
                            bool foundDescriptor = false;
                            uint actualCompressedSize = 0;

                            while ((bytesRead = fs.Read(scanBuffer, 0, scanBuffer.Length)) > 0)
                            {
                                for (int i = 0; i < bytesRead - 3; i++)
                                {
                                    // Buscar firma de data descriptor (PK\7\8 = 0x08074b50) o del siguiente local header
                                    if ((scanBuffer[i] == 0x50 && scanBuffer[i + 1] == 0x4b) &&
                                        ((scanBuffer[i + 2] == 0x07 && scanBuffer[i + 3] == 0x08) || // Data descriptor
                                         (scanBuffer[i + 2] == 0x03 && scanBuffer[i + 3] == 0x04))) // Local file header
                                    {
                                        long descriptorPos = scanPos + i;
                                        fs.Position = descriptorPos;
                                        uint descriptorSignature = reader.ReadUInt32();

                                        if (descriptorSignature == 0x08074b50) // Data descriptor signature
                                        {
                                            // Es un data descriptor con firma
                                            Console.WriteLine($"Data descriptor encontrado en {descriptorPos}");

                                            // Leer valores del data descriptor
                                            uint ddCRC32 = reader.ReadUInt32();
                                            actualCompressedSize = reader.ReadUInt32();
                                            uint ddUncompressedSize = reader.ReadUInt32();

                                            Console.WriteLine(
                                                $"Valores del data descriptor - CRC: {ddCRC32:X8}, CompSize: {actualCompressedSize}, UncompSize: {ddUncompressedSize}");

                                            // El tamaño comprimido es la distancia desde el fin del header hasta el inicio del descriptor
                                            if (actualCompressedSize == 0)
                                            {
                                                actualCompressedSize = (uint) (descriptorPos - dataPosition);
                                                Console.WriteLine(
                                                    $"Recalculando tamaño comprimido: {actualCompressedSize}");
                                            }

                                            foundDescriptor = true;
                                            break;
                                        }
                                        else if (descriptorSignature ==
                                                 0x04034b50) // Local file header de la siguiente entrada
                                        {
                                            // Si encontramos el siguiente local header, el tamaño comprimido es la distancia
                                            // desde el inicio de los datos hasta este nuevo header
                                            actualCompressedSize = (uint) (descriptorPos - dataPosition);
                                            Console.WriteLine(
                                                $"Siguiente local header encontrado. Tamaño comprimido calculado: {actualCompressedSize}");
                                            foundDescriptor = true;
                                            break;
                                        }

                                        // Si no era ninguna de las firmas, continuamos escaneando
                                        fs.Position = descriptorPos + 1;
                                    }
                                }

                                if (foundDescriptor)
                                    break;

                                // Actualizar la posición de escaneo
                                scanPos = fs.Position;
                            }

                            // Si no encontramos un data descriptor con firma, podría estar sin firma
                            if (!foundDescriptor)
                            {
                                Console.WriteLine("No se encontró data descriptor con firma ni siguiente local header");
                                throw new InvalidDataException(
                                    "No se pudo determinar el tamaño comprimido de la entrada");
                            }

                            // Volver a la posición de los datos y leer los bytes comprimidos
                            fs.Position = dataPosition;
                            Console.WriteLine(
                                $"Leyendo {actualCompressedSize} bytes comprimidos desde la posición {dataPosition}");
                            return reader.ReadBytes((int) actualCompressedSize);
                        }
                    }

                    // Si no es la entrada buscada, saltar la entrada completa
                    if ((generalPurposeFlag & 0x0008) != 0 || headerCompressedSize == 0)
                    {
                        // Con data descriptor o tamaño desconocido, debemos escanear hacia adelante
                        // Por simplicidad, continuamos la búsqueda secuencial desde la posición actual
                    }
                    else
                    {
                        // Saltar los datos comprimidos si conocemos su tamaño
                        fs.Position += headerCompressedSize;
                    }
                }
                else
                {
                    // No es un local file header, retroceder 3 bytes y continuar
                    fs.Position -= 3;
                }
            }

            throw new FileNotFoundException($"No se encontró la entrada '{entryName}' en el archivo");
        }
    }
}
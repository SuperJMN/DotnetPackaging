using System.Text;
using Archiver;

namespace Archive.Tests
{
    public class ArchiverFileTests
    {
        [Fact]
        public void Create_AR_file()
        {
            using var stream = new FileStream("C:\\Users\\JMN\\Desktop\\Archivo.ar", FileMode.Create);
            var entry1 = FileEntry.Create("Archive1.txt", new MemoryStream("Hola"u8.ToArray()), DateTimeOffset.Now);
            var entry2 = FileEntry.Create("Archive2.txt", new MemoryStream("Salud y buenos alimentos"u8.ToArray()), DateTimeOffset.Now);
            ArchiverFile.Write(stream, entry1.Value, entry2.Value);
        }

        private static void WriteArFixed(Stream fileStream)
        {
            var streamWriter = new StreamWriter(fileStream, Encoding.ASCII) { NewLine = "\n" };
            using (var writer = streamWriter)
            {
                writer.WriteLine("!<arch>");
                writer.Write("debian-binary   ");
                writer.Write("1342943816  ");
                writer.Write("0     ");
                writer.Write("0     ");
                writer.Write("100644  ");
                writer.Write("4         ");
                writer.Write("`\n");
                writer.WriteLine("2.0");

                //
                writer.Write("file1.txt       ");
                writer.Write("1342943816  ");
                writer.Write("0     ");
                writer.Write("0     ");
                writer.Write("100644  ");
                writer.Write("4         ");
                writer.Write("`\n");
                writer.Write("Hola");

                //
                writer.Write("file2.txt       ");
                writer.Write("1342943816  ");
                writer.Write("0     ");
                writer.Write("0     ");
                writer.Write("100644  ");
                writer.Write("4         ");
                writer.Write("`\n");
                writer.Write("Hola");
            }
        }
    }
}
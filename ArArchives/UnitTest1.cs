using System.Text;
using ArArchives;
using CSharpFunctionalExtensions;
using FluentAssertions;

namespace ArArchives
{
    public class UnitTest1
    {
        [Fact]
        public void Create_AR_file()
        {
            var fileStream = new FileStream("C:\\Users\\JMN\\Desktop\\Archivo.ar", FileMode.Create);
            var entry1 = FileEntry.Create("Archive1.txt", new MemoryStream("Hola"u8.ToArray()), DateTimeOffset.Now);
            var entry2 = FileEntry.Create("Archive2.txt", new MemoryStream("Salud y buenos alimentos"u8.ToArray()), DateTimeOffset.Now);
            WriteAr(fileStream, entry1.Value, entry2.Value);
        }

        [Theory]
        [InlineData("Hola", 3, "Hol")]
        [InlineData("Hola", 4, "Hola")]
        [InlineData("Hola", 5, "Hola")]
        public void Truncate(string str, int length, string expected)
        {
            str.Truncate(length).Should().Be(expected);
        }

        [Theory]
        [InlineData("Hola", 3, "Hol")]
        [InlineData("Hola", 10, "Hola      ")]
        public void ToFixed(string str, int length, string expected)
        {
            str.ToFixed(length).Should().Be(expected);
        }
        
        private static void WriteAr(Stream fileStream, params FileEntry[] entryValue)
        {
            var streamWriter = new StreamWriter(fileStream, Encoding.ASCII) { NewLine = "\n" };
            using (var writer = streamWriter)
            {
                WriteHeader(writer);
                entryValue.ToList().ForEach(entry => WriteEntry(fileStream, entry, writer));
            }
        }

        private static void WriteHeader(StreamWriter writer)
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
        }

        private static void WriteEntry(Stream fileStream, FileEntry entry, StreamWriter writer)
        {
            //
            writer.Write(entry.Name.ToFixed(16));
            writer.Write(entry.DateTimeOffset.ToUnixTimeSeconds().ToString().ToFixed(12));  // Modification timestamp
            writer.Write("0     ");
            writer.Write("0     ");

            writer.Write("100644  ");
            writer.Write(entry.Stream.Length.ToString().ToFixed(10));
            writer.Write("`\n");

            writer.Flush();

            entry.Stream.CopyTo(fileStream);
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

    public class FileEntry
    {
        public string Name { get; }
        public Stream Stream { get; }
        public DateTimeOffset DateTimeOffset { get; }

        private FileEntry(string name, Stream stream, DateTimeOffset dateTimeOffset)
        {
            Name = name;
            Stream = stream;
            DateTimeOffset = dateTimeOffset;
        }

        public static Result<FileEntry> Create(string name, Stream stream, DateTimeOffset dateTimeOffset)
        {
            return Result.Success(new FileEntry(name, stream, dateTimeOffset));
        }
    }
}

public static class Extensions
{
    public static string ToFixed(this string str, int totalWidth) => str.Truncate(totalWidth).PadRight(totalWidth);
    public static string Truncate(this string str, int totalWidth) => new(str.Take(totalWidth).ToArray());
}
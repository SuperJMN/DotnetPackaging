using System.Text;
using ArArchives;
using CSharpFunctionalExtensions;
using FluentAssertions;

namespace ArArchives
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var fileStream = new FileStream("C:\\Users\\JMN\\Desktop\\Archivo.ar", FileMode.Create);
            var entry = FileEntry.Create("Archive", new MemoryStream("Hola"u8.ToArray()));
            WriteArFixed(fileStream);
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
        
        private static void WriteAr(Stream fileStream, FileEntry entryValue)
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
                writer.Write(entryValue.Name.ToFixed(16));
                writer.Write("1342943816  ");
                writer.Write("0     ");
                writer.Write("0     ");
                writer.Write("100644  ");
                writer.Write(entryValue.Stream.Length.ToString().ToFixed(10));
                writer.Write("`\n");
                writer.Write("Hola");

                entryValue.Stream.CopyTo(fileStream);

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

        private FileEntry(string name, Stream stream)
        {
            Name = name;
            Stream = stream;
        }

        public static Result<FileEntry> Create(string name, Stream stream)
        {
            return Result.Success(new FileEntry(name, stream));
        }
    }
}

public static class Extensions
{
    public static string ToFixed(this string str, int totalWidth) => str.Truncate(totalWidth).PadRight(totalWidth);
    public static string Truncate(this string str, int totalWidth) => new(str.Take(totalWidth).ToArray());
}
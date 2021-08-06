using System.IO;

namespace SenseNet.IO.Tests
{
    public static class Extensions
    {
        internal static string RemoveWhitespaces(this string src)
        {
            return src.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace(" ", "");
        }

        internal static Stream ToStream(this string src)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(src);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        internal static string ReadAsString(this MemoryStream stream)
        {
            var buffer = stream.GetBuffer();
            string result;
            using (var reader = new StreamReader(new MemoryStream(buffer)))
                result = reader.ReadToEnd().TrimEnd('\0');

            return result;
        }
    }
}

namespace SenseNet.IO.Tests
{
    public static class Extensions
    {
        internal static string RemoveWhitespaces(this string src)
        {
            return src.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace(" ", "");
        }
    }
}

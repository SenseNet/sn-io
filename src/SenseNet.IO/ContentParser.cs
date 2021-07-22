using System;
using System.IO;
using System.Xml;

namespace SenseNet.IO
{
    public class ContentParser : IContentParser
    {
        private enum ContentSourceType { Unknown, Xml, Json }

        public IContent ParseContent(Stream stream)
        {
            using (var reader = new StreamReader(stream))
                return ParseContent(reader);
        }

        public IContent ParseContent(TextReader reader)
        {
            return ParseContent(reader.ReadToEnd());
        }

        public IContent ParseContent(string src)
        {
            switch(GetSourceType(src))
            {
                case ContentSourceType.Unknown: return null;
                case ContentSourceType.Xml: return new XmlParser().Parse(src);
                case ContentSourceType.Json: return new JsonParser().Parse(src);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private ContentSourceType GetSourceType(string src)
        {
            if (string.IsNullOrEmpty(src))
                return ContentSourceType.Unknown;

            if(src[0] == '<')
                return ContentSourceType.Xml;

            for (int i = 0; i < src.Length; i++)
            {
                if (char.IsWhiteSpace(src[i]))
                    continue;
                return src[i] == '{' ? ContentSourceType.Json : ContentSourceType.Unknown;
            }
            return ContentSourceType.Unknown;
        }
    }
}

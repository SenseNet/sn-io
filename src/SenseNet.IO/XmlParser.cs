using System;
using System.Xml;

namespace SenseNet.IO
{
    internal class XmlParser
    {
        public IContent Parse(string src)
        {
            var xml = new XmlDocument();
            try
            {
                xml.LoadXml(src);
            }
            catch (Exception e)
            {
                throw new ParserException("Invalid XML content", e);
            }
            var typeElement = xml.SelectSingleNode("/ContentMetaData/ContentType") as XmlElement;
            var nameElement = xml.SelectSingleNode("/ContentMetaData/ContentName") as XmlElement;
            var content = new IoContent
            {
                Name = nameElement?.InnerXml, // not required
                Type = typeElement?.InnerXml  // required
            };
            ParseFields(content, xml);
            return content;
        }

        private void ParseFields(IoContent content, XmlDocument xml)
        {
            //UNDONE: Implement XmlParser.ParseFields
        }
    }
}

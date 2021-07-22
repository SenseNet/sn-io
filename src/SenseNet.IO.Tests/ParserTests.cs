using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
// ReSharper disable UnusedVariable

namespace SenseNet.IO.Tests
{
    [TestClass]
    public class ParserTests
    {
        [TestMethod]
        public void Parser_Simple_Xml()
        {
            var src = @"<ContentMetaData><ContentType>ContentType1</ContentType><ContentName>Content1</ContentName></ContentMetaData>";

            // ACTION
            var parser = new ContentParser();
            var content = parser.ParseContent(src);

            // ASSERT
            Assert.AreEqual("Content1", content.Name);
            Assert.AreEqual("ContentType1", content.Type);
        }
        [TestMethod]
        public void Parser_Simple_Json()
        {
            var src = @"{ ContentType: 'ContentType1', ContentName: 'Content1' }";

            // ACTION
            var parser = new ContentParser();
            var content = parser.ParseContent(src);

            // ASSERT
            Assert.AreEqual("Content1", content.Name);
            Assert.AreEqual("ContentType1", content.Type);
        }
        [TestMethod]
        public void Parser_Simple_Null()
        {
            // ACTION
            var parser = new ContentParser();
            var content = parser.ParseContent((string)null);

            // ASSERT
            Assert.IsNull(content);
        }
        [TestMethod]
        public void Parser_Simple_Empty()
        {
            // ACTION
            var parser = new ContentParser();
            var content = parser.ParseContent(string.Empty);

            // ASSERT
            Assert.IsNull(content);
        }
        [TestMethod]
        public void Parser_Simple_Whitespace()
        {
            // ACTION
            var parser = new ContentParser();
            var content = parser.ParseContent("  \t  \n  \r  \r\n");

            // ASSERT
            Assert.IsNull(content);
        }
        [TestMethod]
        public void Parser_Simple_Xml_LeadWhitespace()
        {
            var src = @" <ContentMetaData><ContentType>ContentType1</ContentType><ContentName>Content1</ContentName></ContentMetaData>";

            // ACTION
            var parser = new ContentParser();
            var content = parser.ParseContent(src);

            // ASSERT
            Assert.IsNull(content);
        }
        [TestMethod]
        public void Parser_Simple_Xml_Invalid()
        {
            var src = @"<ContentMetaData>";

            try
            {
                // ACTION
                var parser = new ContentParser();
                var content = parser.ParseContent(src);
            }
            catch (Exception e)
            {
                // ASSERT
                if (!(e is ParserException))
                    Assert.Fail($"The exception is {e.GetType().Name}, expected: {nameof(ParserException)}");
                if (!(e.Message.Contains("XML", StringComparison.OrdinalIgnoreCase)))
                    Assert.Fail($"The exception message does not contain 'Xml'");
            }
        }
        [TestMethod]
        public void Parser_Simple_Json_Invalid()
        {
            var src = @"{ invalidJson";

            try
            {
                // ACTION
                var parser = new ContentParser();
                var content = parser.ParseContent(src);
            }
            catch (Exception e)
            {
                // ASSERT
                if (!(e is ParserException))
                    Assert.Fail($"The exception is {e.GetType().Name}, expected: {nameof(ParserException)}");
                if (!(e.Message.Contains("JSON", StringComparison.OrdinalIgnoreCase)))
                    Assert.Fail($"The exception message does not contain 'Json'");
            }
        }

        //UNDONE: Write more reliability test for parsers

        /*
        [TestMethod]
        public void Parser_FieldsXml()
        {
            var src = @"<?xml version='1.0' encoding='utf-8'?>
<ContentMetaData>
  <ContentType>ContentType1</ContentType>
  <ContentName>Content1</ContentName>
  <Fields>
    <DisplayName><![CDATA[Content-1]]></DisplayName>
    <Description><![CDATA[]]></Description>
    <Empty1 />
    <Date1>0001-01-01T00:00:00</Date1>
    <Bool1>false</Bool1>
    <Int1>0</Int1>
    <Number1>0.0000</Number1>
    <Ref1>
      <Path>/Root/IMS/BuiltIn/Portal/Admin</Path>
    </Ref1>
  </Fields>
  <Permissions>
    <Clear />
  </Permissions>
</ContentMetaData>
";
        }
        */
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SenseNet.IO
{
    internal class JsonParser
    {
        public IContent Parse(string src)
        {
            JObject json;
            try
            {
                json = JsonConvert.DeserializeObject(src) as JObject;
            }
            catch (Exception e)
            {
                throw new ParserException("Invalid JSON content", e);
            }

            if (json == null)
                throw new ParserException("Cannot parse the JSON content");

            var name = json["ContentName"]?.Value<string>();
            var type = json["ContentType"]?.Value<string>();

            var content = new Content
            {
                Name = name,
                Type = type
            };

            ParseFields(content, json);
            return content;
        }

        private void ParseFields(Content content, JObject json)
        {
            //UNDONE: Implement JsonParser.ParseFields
        }
    }
}

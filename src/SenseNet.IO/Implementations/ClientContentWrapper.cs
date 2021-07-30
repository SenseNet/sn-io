using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SenseNet.Client;

namespace SenseNet.IO.Implementations
{
    public class ClientContentWrapper : IContent
    {
        private readonly Content _content;

        public ClientContentWrapper(Content content)
        {
            _content = content;
        }

        public object this[string fieldName]
        {
            get => _content[fieldName];
            set => _content[fieldName] = value;
        }

        public string Name
        {
            get => _content.Name;
            set => _content.Name = value;
        }
        public string Path
        {
            get => _content.Path;
            set => _content.Path = value;
        }
        public string Type
        {
            get => ((JToken)_content["Type"]).Value<string>();
            set => _content["Type"] = value;
        }

        public T GetField<T>(string name)
        {
            var raw = this[name];
            if (raw is JValue jValue)
            {
                if (jValue.Value == null)
                    return default;
                return jValue.ToObject<T>();
            }

            //UNDONE: other types are not implemented
            return default;
        }

        private static readonly string[] FieldBlackList = new[]
        {
            "Actions", "Children", "EffectiveAllowedChildTypes",
            "CreatedById", "ModifiedById", "OwnerId", "ParentId", "SavingState",
            "InFolder", "InTree"
        };

        public string ToJson()
        {
            var fields = _content.FieldNames
                .Except(FieldBlackList)
                .Where(f => !IsNull(_content[f]))
                .ToDictionary(key => key, key => _content[key]);

            var model = new
            {
                ContentType = Type,
                ContentName = Name,
                Fields = fields
            };

            var writer = new StringWriter();
            JsonSerializer.Create(new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            }).Serialize(writer, model);

            return writer.GetStringBuilder().ToString();
        }

        private bool IsNull(object value)
        {
            if (value == null)
                return true;
            if(value is JValue jValue)
                if (jValue.Value == null)
                    return true;
            return false;
        }
    }
}
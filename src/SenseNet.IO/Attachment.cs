using System.IO;

namespace SenseNet.IO
{
    /// <summary>
    /// Represents a streamed binary content.
    /// </summary>
    public class Attachment
    {
        /// <summary>
        /// Gets or sets the name of the field that stores the stream.
        /// </summary>
        public string FieldName { get; set; }
        /// <summary>
        /// Gets or sets the original file name (without any path segment)
        /// </summary>
        public string FileName { get; set; }
        /// <summary>
        /// Gets or sets the mime type
        /// </summary>
        public string ContentType { get; set; }
        /// <summary>
        /// Gets or sets the stream
        /// </summary>
        public Stream Stream { get; set; }
    }
}

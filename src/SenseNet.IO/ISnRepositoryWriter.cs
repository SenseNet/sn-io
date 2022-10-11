using SenseNet.IO.Implementations;

namespace SenseNet.IO
{
    /// <summary>
    /// Defines members of a repository writer.
    /// </summary>
    public interface ISnRepositoryWriter : IContentWriter
    {
        /// <summary>
        /// Repository writer options.
        /// </summary>
        RepositoryWriterArgs WriterOptions { get; }
    }
}

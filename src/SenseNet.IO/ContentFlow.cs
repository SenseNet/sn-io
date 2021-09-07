using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SenseNet.IO.Implementations;

namespace SenseNet.IO
{
    public static class ContentFlow
    {
        public static IContentFlow Create(IContentReader reader, IContentWriter writer)
        {
            return writer is ISnRepositoryWriter repoWriter
                ? new Level5ContentFlow(reader, repoWriter)
                : new Level1ContentFlow(reader, writer);
        }
    }
}

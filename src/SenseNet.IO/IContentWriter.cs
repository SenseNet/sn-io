using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    public interface IContentWriter
    {
        void Write(string path, IContent content);
    }
}

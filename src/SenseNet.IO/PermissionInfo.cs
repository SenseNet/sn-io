using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SenseNet.IO
{
    public class PermissionInfo
    {
        public bool IsInherited { get; set; }
        public AceInfo[] Entries { get; set; }
    }

    public class AceInfo
    {
        public string Identity { get; set; }
        public bool LocalOnly { get; set; }
        public Dictionary<string, string> Permissions { get; set; }
    }
}

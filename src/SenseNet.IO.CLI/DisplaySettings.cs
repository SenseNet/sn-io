using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SenseNet.IO.CLI
{
    public enum DisplayLevel { None, Progress, Errors, Verbose }

    public class DisplaySettings
    {
        public string Level { get; set; }

        private DisplayLevel? _displayLevel;
        public DisplayLevel DisplayLevel
        {
            get
            {
                _displayLevel ??= string.IsNullOrEmpty(Level) ? DisplayLevel.Errors : Enum.Parse<DisplayLevel>(Level, true);
                return _displayLevel.Value;
            }
        }
    }
}

using SenseNet.Tools.Configuration;
using System;

namespace SenseNet.IO.CLI
{
    public enum DisplayLevel { None, Progress, Errors, Verbose }

    [OptionsClass(sectionName: "display")]
    public class DisplaySettings
    {
        /// <summary>
        /// Display level. Possible values: None, Progress, Errors, Verbose
        /// </summary>
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

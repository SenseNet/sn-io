using System;

namespace SenseNet.IO.CLI
{
    internal class ColoredConsoleSupport
    {
        private class ColoredBlock : IDisposable
        {
            public ColoredBlock(ConsoleColor foreground, ConsoleColor background)
            {
                Console.ForegroundColor = foreground;
                Console.BackgroundColor = background;
            }
            public void Dispose()
            {
                SetDefaultColor();
            }
        }

        private static ConsoleColor _defaultBackgroundColor;
        private static ConsoleColor _defaultForegroundColor;

        public ColoredConsoleSupport()
        {
            _defaultBackgroundColor = Console.BackgroundColor;
            _defaultForegroundColor = Console.ForegroundColor;
        }

        private static void SetDefaultColor()
        {
            Console.BackgroundColor = _defaultBackgroundColor;
            Console.ForegroundColor = _defaultForegroundColor;
        }

        public IDisposable Error()
        {
            return new ColoredBlock(ConsoleColor.White, ConsoleColor.Red);
        }

        public IDisposable Highlight()
        {
            return _defaultForegroundColor == ConsoleColor.White || _defaultForegroundColor == ConsoleColor.DarkYellow
                ? new ColoredBlock(ConsoleColor.Yellow, _defaultBackgroundColor)
                : new ColoredBlock(ConsoleColor.White, _defaultBackgroundColor);
        }

        public IDisposable Warning()
        {
            //return new ColoredBlock(ConsoleColor.Yellow, _defaultBackgroundColor);
            return new ColoredBlock(ConsoleColor.Black, ConsoleColor.Yellow);
        }
    }
}

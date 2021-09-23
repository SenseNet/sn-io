using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SenseNet.IO.Implementations;

namespace SenseNet.IO
{
    public abstract class ContentFlow : IContentFlow
    {
        private ILogger<ContentFlow> _logger;
        public IContentReader Reader { get; }
        // ReSharper disable once InconsistentNaming
        protected IContentWriter _writer;
        public virtual IContentWriter Writer => _writer;

        protected ContentFlow(IContentReader reader, IContentWriter writer, ILogger<ContentFlow> logger)
        {
            Reader = reader;
            _writer = writer;
            _logger = logger;
        }

        public abstract Task TransferAsync(IProgress<TransferState> progress, CancellationToken cancel = default);

        /* ========================================================================== LOGGING */

        protected void WriteSummaryToLog(int estimatedCount, int transferredCount, int errorCount, TimeSpan duration)
        {
            //UNDONE: write [INF] Steps, Contents, UpdateRefs, errors, duration
            WriteLog($"FINISH: transferred contents: {transferredCount}/{estimatedCount}, errors: {errorCount}, duration: {duration}");
        }

        /* ========================================================================== TOOLS */

        protected void Rename(IContent content, string newName)
        {
            if (content.FieldNames.Contains("Name"))
                content["Name"] = newName;
            content.Name = newName;
        }

        /* ========================================================================== LOGGING */

        protected int ReferenceUpdateTasksTotalCount;

        private string _logFilePath;
        private string _taskFilePath;
        protected void WriteLogAndTask(WriterState state, bool updateReferences)
        {
            WriteLog(state);
            if (!updateReferences && state.UpdateRequired)
                WriteTask(state);
        }
        private void WriteLog(WriterState state)
        {
            using (var writer = new StringWriter())
            {
                writer.WriteLine($"{state.Action,-8} {state.WriterPath}");
                foreach (var message in state.Messages)
                    writer.WriteLine($"         {message.Replace("The server returned an error (HttpStatus: InternalServerError): ", "")}");
                WriteLog(writer.GetStringBuilder().ToString().Trim());
            }
        }
        protected void WriteLog(Exception e)
        {
            WriteLog(e.ToString());
        }
        private string CreateLogFile(bool createNew, string extension)
        {
            var asm = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var folder = asm == null ? "D:\\" : Path.Combine(asm, "logs");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, $"SenseNet.IO.{DateTime.Now:yyyyMMdd_HHmmss}.{extension}");
            if (!File.Exists(path) || createNew)
            {
                using (FileStream fs = new FileStream(path, FileMode.Create))
                using (StreamWriter wr = new StreamWriter(fs)) { }
            }

            return path;
        }

        /* -------------------------------------------------------------------------- TESTABILITY */

        protected virtual void WriteLog(string entry, bool head = false)
        {
            _logger.LogTrace(entry);

            //if (_logFilePath == null)
            //    _logFilePath = CreateLogFile(true, "log");

            //using (StreamWriter writer = new StreamWriter(_logFilePath, true))
            //    writer.WriteLine(head ? entry : $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffff}  {entry}");
        }
        protected virtual void WriteTask(WriterState state)
        {
            if (_taskFilePath == null)
                _taskFilePath = CreateLogFile(true, "tasks");

            using (StreamWriter writer = new StreamWriter(_taskFilePath, true))
            {
                writer.Write($"{state.ReaderPath};{state.WriterPath};{ string.Join(",", state.BrokenReferences)}");
                if (state.RetryPermissions)
                    writer.Write(";SetPermissions");
                writer.WriteLine();
            }

            ReferenceUpdateTasksTotalCount++;
        }
        protected virtual int LoadTaskCount()
        {
            if (_taskFilePath == null)
                return 0;

            var count = 0;
            string line;
            using (var reader = new StreamReader(_taskFilePath))
            {
                while ((line = reader.ReadLine()) != null)
                    count++;
            }
            return count;
        }
        protected virtual IEnumerable<TransferTask> LoadTasks()
        {
            using (var reader = new StreamReader(_taskFilePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var fields = line.Split(';');
                    if (fields.Length < 3)
                        throw new InvalidOperationException("Invalid task file.");

                    yield return new TransferTask
                    {
                        ReaderPath = fields[0].Trim(),
                        WriterPath = fields[1].Trim(),
                        BrokenReferences = fields[2].Split(',').Select(x => x.Trim()).ToArray(),
                        RetryPermissions = fields.Length > 3 && fields[3] == "SetPermissions"
                    };
                }
            }
        }
    }
}

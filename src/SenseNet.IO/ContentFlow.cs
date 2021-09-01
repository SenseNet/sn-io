using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SenseNet.IO
{
    public class ContentFlow : IContentFlow
    {
        private class InitialContent : IContent
        {
            public string[] FieldNames { get; } = new string[0];

            public object this[string fieldName]
            {
                get => null;
                set => throw new NotImplementedException();
            }

            public string Name { get; set; }
            public string Path { get; }
            public string Type { get; }
            public PermissionInfo Permissions { get; set; }

            public InitialContent(string name, string type)
            {
                Name = name;
                Type = type;
            }

            public Task<Attachment[]> GetAttachmentsAsync()
            {
                return Task.FromResult(Array.Empty<Attachment>());
            }
        }

        private static readonly Dictionary<string, IContent> InitialContents = new Dictionary<string, IContent>
        {
            {"/Root", new InitialContent("Root", "PortalRoot")},
            {"/Root/System", new InitialContent("System", "SystemFolder")},
            {"/Root/System/Settings", new InitialContent("Settings", "SystemFolder")},
            {"/Root/System/Schema", new InitialContent("Schema", "SystemFolder")},
            {"/Root/System/Schema/Aspects", new InitialContent("Aspects", "SystemFolder")},
            {"/Root/System/Schema/ContentTypes", new InitialContent("ContentTypes", "SystemFolder")},
        };

        public IContentReader Reader { get; }
        public IContentWriter Writer { get; }
        public ContentFlow(IContentReader reader, IContentWriter writer)
        {
            Reader = reader;
            Writer = writer;
        }

        private int _count = 0;
        private int _referenceUpdateTasksTotalCount = 0;
        private string _rootName;
        public async Task TransferAsync(IProgress<(string Path, double Percent)> progress = null, CancellationToken cancel = default)
        {
            string section;
            _rootName = Writer.RootName ?? ContentPath.GetName(Reader.RootPath);

            if (Reader.RootPath.Equals("/Root", StringComparison.OrdinalIgnoreCase))
            {
                if (await Reader.ReadContentTypesAsync(cancel))
                {
                    section = "----------- TRANSFER CONTENT TYPES -----------";
                    Console.WriteLine(section);
                    WriteLogToFile(section);

                    await EnsureRootAsync("", cancel);
                    await EnsureSystemAsync("System", cancel);
                    await EnsureSchemaAsync("System/Schema", cancel);
                    await EnsureContentTypesAsync("System/Schema/ContentTypes", cancel);

                    await WriteAsync(progress, false, cancel);
                    while (await Reader.ReadContentTypesAsync(cancel))
                        await WriteAsync(progress, false, cancel);
                }
                if (await Reader.ReadSettingsAsync(cancel))
                {
                    section = "----------- TRANSFER SETTINGS -----------";
                    Console.WriteLine(section);
                    WriteLogToFile(section);

                    await EnsureRootAsync("", cancel);
                    await EnsureSystemAsync("System", cancel);
                    await EnsureSettingsAsync("System/Settings", cancel);

                    await WriteAsync(progress, false, cancel);
                    while (await Reader.ReadSettingsAsync(cancel))
                        await WriteAsync(progress, false, cancel);
                }
                if (await Reader.ReadAspectsAsync(cancel))
                {
                    section = "----------- TRANSFER ASPECT DEFINITIONS -----------";
                    Console.WriteLine(section);
                    WriteLogToFile(section);

                    await EnsureRootAsync("", cancel);
                    await EnsureSystemAsync("System", cancel);
                    await EnsureSchemaAsync("System/Schema", cancel);
                    await EnsureAspectsAsync("System/Schema/Aspects", cancel);

                    await WriteAsync(progress, false, cancel);
                    while (await Reader.ReadAspectsAsync(cancel))
                        await WriteAsync(progress, false, cancel);
                }
            }
            else if (Reader.RootPath.Equals("/Root/System", StringComparison.OrdinalIgnoreCase))
            {
                if (await Reader.ReadContentTypesAsync(cancel))
                {
                    section = "----------- TRANSFER CONTENT TYPES -----------";
                    Console.WriteLine(section);
                    WriteLogToFile(section);

                    await EnsureSystemAsync("", cancel);
                    await EnsureSchemaAsync("Schema", cancel);
                    await EnsureContentTypesAsync("Schema/ContentTypes", cancel);

                    await WriteAsync(progress, false, cancel);
                    while (await Reader.ReadContentTypesAsync(cancel))
                        await WriteAsync(progress, false, cancel);
                }
                if (await Reader.ReadSettingsAsync(cancel))
                {
                    section = "----------- TRANSFER SETTINGS -----------";
                    Console.WriteLine(section);
                    WriteLogToFile(section);

                    await EnsureSystemAsync("", cancel);
                    await EnsureSettingsAsync("Settings", cancel);

                    await WriteAsync(progress, false, cancel);
                    while (await Reader.ReadSettingsAsync(cancel))
                        await WriteAsync(progress, false, cancel);
                }
                if (await Reader.ReadAspectsAsync(cancel))
                {
                    section = "----------- TRANSFER ASPECT DEFINITIONS -----------";
                    Console.WriteLine(section);
                    WriteLogToFile(section);

                    await EnsureSystemAsync("", cancel);
                    await EnsureSchemaAsync("Schema", cancel);
                    await EnsureAspectsAsync("Schema/Aspects", cancel);

                    await WriteAsync(progress, false, cancel);
                    while (await Reader.ReadAspectsAsync(cancel))
                        await WriteAsync(progress, false, cancel);
                }
            }
            else if (Reader.RootPath.Equals("/Root/System/Settings", StringComparison.OrdinalIgnoreCase))
            {
                if (await Reader.ReadSettingsAsync(cancel))
                {
                    section = "----------- TRANSFER SETTINGS -----------";
                    Console.WriteLine(section);
                    WriteLogToFile(section);

                    await EnsureSettingsAsync("", cancel);

                    await WriteAsync(progress, false, cancel);
                    while (await Reader.ReadSettingsAsync(cancel))
                        await WriteAsync(progress, false, cancel);
                }
            }
            else if (Reader.RootPath.Equals("/Root/System/Schema", StringComparison.OrdinalIgnoreCase))
            {
                if (await Reader.ReadContentTypesAsync(cancel))
                {
                    section = "----------- TRANSFER CONTENT TYPES -----------";
                    Console.WriteLine(section);
                    WriteLogToFile(section);

                    await EnsureSchemaAsync("", cancel);
                    await EnsureContentTypesAsync("ContentTypes", cancel);

                    await WriteAsync(progress, false, cancel);
                    while (await Reader.ReadContentTypesAsync(cancel))
                        await WriteAsync(progress, false, cancel);
                }
                if (await Reader.ReadAspectsAsync(cancel))
                {
                    section = "----------- TRANSFER ASPECT DEFINITIONS -----------";
                    Console.WriteLine(section);
                    WriteLogToFile(section);

                    await EnsureSchemaAsync("", cancel);
                    await EnsureAspectsAsync("Aspects", cancel);

                    await WriteAsync(progress, false, cancel);
                    while (await Reader.ReadAspectsAsync(cancel))
                        await WriteAsync(progress, false, cancel);
                }
            }
            else if (Reader.RootPath.Equals("/Root/System/Schema/ContentTypes", StringComparison.OrdinalIgnoreCase))
            {
                if (await Reader.ReadContentTypesAsync(cancel))
                {
                    section = "----------- TRANSFER CONTENT TYPES -----------";
                    Console.WriteLine(section);
                    WriteLogToFile(section);

                    await EnsureContentTypesAsync("", cancel);

                    await WriteAsync(progress, false, cancel);
                    while (await Reader.ReadContentTypesAsync(cancel))
                        await WriteAsync(progress, false, cancel);
                }
            }
            else if (Reader.RootPath.Equals("/Root/System/Schema/Aspects", StringComparison.OrdinalIgnoreCase))
            {
                if (await Reader.ReadAspectsAsync(cancel))
                {
                    section = "----------- TRANSFER ASPECT DEFINITIONS -----------";
                    Console.WriteLine(section);
                    WriteLogToFile(section);

                    await EnsureAspectsAsync("", cancel);

                    await WriteAsync(progress, false, cancel);
                    while (await Reader.ReadAspectsAsync(cancel))
                        await WriteAsync(progress, false, cancel);
                }
            }

            await TransferAllAsync(progress, cancel);

            await UpdateReferencesAsync(progress, cancel);
        }

        private bool _rootWritten;
        private async Task EnsureRootAsync(string relativePath, CancellationToken cancel)
        {
            if (_rootWritten)
                return;
            await Writer.WriteAsync(ContentPath.Combine(_rootName, relativePath), InitialContents["/Root"], cancel);
            _rootWritten = true;
        }
        private bool _systemWritten;
        private async Task EnsureSystemAsync(string relativePath, CancellationToken cancel)
        {
            if (_systemWritten)
                return;
            await Writer.WriteAsync(ContentPath.Combine(_rootName, relativePath), InitialContents["/Root/System"], cancel);
            _systemWritten = true;
        }
        private bool _settingsWritten;
        private async Task EnsureSettingsAsync(string relativePath, CancellationToken cancel)
        {
            if (_settingsWritten)
                return;
            await Writer.WriteAsync(ContentPath.Combine(_rootName, relativePath), InitialContents["/Root/System/Settings"], cancel);
            _settingsWritten = true;
        }
        private bool _schemaWritten;
        private async Task EnsureSchemaAsync(string relativePath, CancellationToken cancel)
        {
            if (_schemaWritten)
                return;
            await Writer.WriteAsync(ContentPath.Combine(_rootName, relativePath), InitialContents["/Root/System/Schema"], cancel);
            _schemaWritten = true;
        }
        private bool _aspectsWritten;
        private async Task EnsureAspectsAsync(string relativePath, CancellationToken cancel)
        {
            if (_aspectsWritten)
                return;
            await Writer.WriteAsync(ContentPath.Combine(_rootName, relativePath), InitialContents["/Root/System/Schema/Aspects"], cancel);
            _aspectsWritten = true;
        }
        private bool _contentTypesWritten;
        private async Task EnsureContentTypesAsync(string relativePath, CancellationToken cancel)
        {
            if (_contentTypesWritten)
                return;
            await Writer.WriteAsync(ContentPath.Combine(_rootName, relativePath), InitialContents["/Root/System/Schema/ContentTypes"], cancel);
            _contentTypesWritten = true;
        }

        private async Task TransferAllAsync(IProgress<(string Path, double Percent)> progress = null, CancellationToken cancel = default)
        {
            if (await Reader.ReadAllAsync(cancel))
            {
                if (Writer.RootName != null)
                    Rename(Reader.Content, _rootName);

                var section = "----------- TRANSFER CONTENTS -----------";
                Console.WriteLine(section);
                WriteLogToFile(section);

                await WriteAsync(progress, false, cancel);
                while (await Reader.ReadAllAsync(cancel))
                {
                    await WriteAsync(progress, false, cancel);
                }
            }
        }
        private void Rename(IContent content, string newName)
        {
            if (content.FieldNames.Contains("Name"))
                content["Name"] = newName;
            content.Name = newName;
        }

        private async Task UpdateReferencesAsync(IProgress<(string Path, double Percent)> progress = null,
            CancellationToken cancel = default)
        {
            var taskCount = LoadTaskCount();
            if (taskCount == 0)
                return;

            var section = "----------- UPDATE REFERENCES -----------";
            Console.WriteLine(section);
            WriteLogToFile(section);

            var tasks = LoadTasks();
            Reader.SetReferenceUpdateTasks(tasks, taskCount);

            while (await Reader.ReadByReferenceUpdateTasksAsync(cancel))
                await WriteAsync(progress, true, cancel);
        }

        private async Task WriteAsync(IProgress<(string Path, double Percent)> progress, bool updateReferences, CancellationToken cancel = default)
        {
            var readerPath = Reader.RelativePath;
            var writerPath = ContentPath.Combine(_rootName, readerPath);
            var state = await Writer.WriteAsync(writerPath, Reader.Content, cancel);
            state.ReaderPath = readerPath;
            state.WriterPath = writerPath;
            Progress(readerPath, ref _count, state, updateReferences, progress);
        }
        private void Progress(string readerPath, ref int count, TransferState state, bool updateReferences, IProgress<(string Path, double Percent)> progress = null)
        {
            Console.Write($"{ActionToDisplay(state, updateReferences),-8} {state.WriterPath}");
            Console.WriteLine(state.WriterPath.Length < 40 ? new string(' ', 40 - state.WriterPath.Length) : string.Empty);

            if(state.Action == TransferAction.Error)
                foreach (var message in state.Messages)
                    Console.WriteLine($"         {message.Replace("The server returned an error (HttpStatus: InternalServerError): ", "")}                               ");

            WriteLogAndTask(state, updateReferences);

            ++count;
            var totalCount = Reader.EstimatedCount + _referenceUpdateTasksTotalCount;
            if (totalCount > 0)
                progress?.Report((readerPath, count * 100.0 / totalCount));
        }

        /* ============================================================================== LOGGING */

        private void WriteLogAndTask(TransferState state, bool updateReferences)
        {
            WriteLogToFile(state, updateReferences);
            if (!updateReferences && (state.RetryPermissions || state.BrokenReferences.Length > 0))
                WriteTaskToFile(state);
        }

        private string _logFilePath;
        private void WriteLogToFile(TransferState state, bool updateReferences)
        {
            using (var writer = new StringWriter())
            {
                writer.WriteLine($"{ActionToDisplay(state, updateReferences),-8} {state.WriterPath}");
                foreach (var message in state.Messages)
                    writer.WriteLine($"         {message.Replace("The server returned an error (HttpStatus: InternalServerError): ", "")}");
                WriteLogToFile(writer.GetStringBuilder().ToString().Trim());
            }
        }
        private void WriteLogToFile(string entry)
        {
            if (_logFilePath == null)
                _logFilePath = CreateLogFile(true, "log");

            using (StreamWriter writer = new StreamWriter(_logFilePath, true))
                writer.WriteLine(entry);
        }

        private string _taskFilePath;
        private void WriteTaskToFile(TransferState state)
        {
            if (_taskFilePath == null)
                _taskFilePath = CreateLogFile(true, "tasks");

            using (StreamWriter writer = new StreamWriter(_taskFilePath, true))
            {
                writer.Write($"{state.ReaderPath};{state.WriterPath};{ string.Join(",", state.BrokenReferences)}");
                if(state.RetryPermissions)
                    writer.Write(";SetPermissions");
                writer.WriteLine();
            }

            _referenceUpdateTasksTotalCount++;
        }

        private int LoadTaskCount()
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
        //private IEnumerable<TransferTask> LoadTasks()
        //{
        //    // Preload whole file because reading per line causes IOException:
        //    //     "The process cannot access the file '****.tasks' because it is being used by another process."
        //    string fileContent;
        //    using (var r = new StreamReader(_taskFilePath))
        //        fileContent = r.ReadToEnd();

        //    using (var reader = new StringReader(fileContent))
        //    {
        //        string line;
        //        while ((line = reader.ReadLine()) != null)
        //        {
        //            var fields = line.Split(';');
        //            if (fields.Length < 3)
        //                throw new InvalidOperationException("Invalid task file.");

        //            yield return new TransferTask
        //            {
        //                ReaderPath = fields[0].Trim(),
        //                WriterPath = fields[1].Trim(),
        //                BrokenReferences = fields[2].Split(',').Select(x => x.Trim()).ToArray(),
        //                RetryPermissions = fields.Length > 3 && fields[3] == "SetPermissions"
        //            };
        //        }
        //    }
        //}
        private IEnumerable<TransferTask> LoadTasks()
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

        public string CreateLogFile(bool createNew, string extension)
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
        private string ActionToDisplay(TransferState state, bool updateReferences)
        {
            var action = state.Action;
            if (!updateReferences)
            {
                if (state.UpdateRequired)
                {
                    if (action == TransferAction.Create)
                        return "Creating";
                    if (action == TransferAction.Update)
                        return "Updating";
                }
            } 
            return action.ToString();
        }


    }
}

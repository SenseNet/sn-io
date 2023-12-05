using System;
using System.Collections.Generic;
using System.Linq;
using SenseNet.IO.Implementations;

namespace SenseNet.IO.CLI
{
    public class ArgumentParser
    {
        private static readonly StringComparison Cmp = StringComparison.OrdinalIgnoreCase;

        public IAppArguments Parse(string[] args)
        {
            if (args.Length == 0)
                throw new ArgumentParserException("Missing verb (EXPORT, IMPORT, COPY, SYNC or TRANSFER)");
            if (!Enum.TryParse<Verb>(args[0], true, out var verb))
                throw new ArgumentParserException($"Invalid verb '{args[0]}'. Valid values are EXPORT, IMPORT, COPY, SYNC or TRANSFER");

            var sourceSection = args
                .SkipWhile(x => !x.Equals("-source", Cmp))
                .Skip(1)
                .TakeWhile(x => !x.Equals("-target", Cmp) && !x.Equals("-source", Cmp))
                .ToArray();
            var targetSection = args
                .SkipWhile(x => !x.Equals("-target", Cmp))
                .Skip(1)
                .TakeWhile(x => !x.Equals("-target", Cmp) && !x.Equals("-source", Cmp))
                .ToArray();

            return ParseByVerb(verb, sourceSection, targetSection);
        }

        protected virtual IAppArguments ParseByVerb(Verb verb, string[] sourceArgs, string[] targetArgs)
        {
            switch (verb)
            {
                case Verb.Export:
                    return new ExportArguments
                    {
                        ReaderArgs = ParseRepositoryReaderArgs(sourceArgs),
                        WriterArgs = ParseFsWriterArgs(targetArgs)
                    };
                case Verb.Import:
                    return new ImportArguments
                    {
                        ReaderArgs = ParseFsReaderArgs(sourceArgs),
                        WriterArgs = ParseRepositoryWriterArgs(targetArgs)
                    };
                case Verb.Copy:
                    return new CopyArguments
                    {
                        ReaderArgs = ParseFsReaderArgs(sourceArgs),
                        WriterArgs = ParseFsWriterArgs(targetArgs)
                    };
                case Verb.Sync:
                    return new SyncArguments
                    {
                        ReaderArgs = ParseRepositoryReaderArgs(sourceArgs),
                        WriterArgs = ParseRepositoryWriterArgs(targetArgs)
                    };
                case Verb.Transfer:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException(nameof(verb), verb, null);
            }
        }

        protected virtual FsReaderArgs ParseFsReaderArgs(string[] args)
        {
            // [[-PATH] Path] [[-SKIP] PathList]
            var result = new FsReaderArgs();
            var parsedArgs = ParseSequence(args);

            if (parsedArgs.Length > 2)
                throw new ArgumentParserException("Too many FsReader arguments.");

            foreach (var arg in parsedArgs)
            {
                if (arg.Key == "0" || arg.Key.Equals("PATH", Cmp))
                {
                    if (result.Path != null)
                        throw new ArgumentParserException("Invalid FsReader arguments.");
                    if(arg.Value == null)
                        throw new ArgumentParserException("Invalid FsReader arguments. Missing path.");
                    result.Path = arg.Value;
                }
                else if (arg.Key == "1" || arg.Key.Equals("SKIP", Cmp))
                {
                    if (result.Skip != null)
                        throw new ArgumentParserException("Invalid FsReader arguments.");
                    result.Skip = arg.Value
                        .Split(",;".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                        .Select(x=>x.Trim())
                        .ToArray();
                }
                else
                    throw new ArgumentParserException("Unknown FsReader argument: " + arg.Key);
            }
            return result;

        }
        protected virtual FsWriterArgs ParseFsWriterArgs(string[] args)
        {
            // [[-PATH] Path] [[-NAME ]Name] [-FLATTEN]
            var result = new FsWriterArgs();
            var parsedArgs = ParseSequence(args);

            if (parsedArgs.Length > 3)
                throw new ArgumentParserException("Too many FsWriter arguments.");

            foreach (var arg in parsedArgs)
            {
                if (arg.Key == "0" || arg.Key.Equals("PATH", Cmp))
                {
                    if (result.Path != null)
                        throw new ArgumentParserException("Invalid FsWriter arguments.");
                    result.Path = arg.Value;
                }
                else if (arg.Key == "1" || arg.Key.Equals("NAME", Cmp))
                {
                    if (result.Name != null)
                        throw new ArgumentParserException("Invalid FsWriter arguments.");
                    result.Name = arg.Value;
                }
                else if (arg.Key.Equals("FLATTEN", Cmp))
                {
                    if (result.Flatten != null)
                        throw new ArgumentParserException("Invalid FsWriter arguments.");
                    result.Flatten = true;
                }
                else
                    throw new ArgumentParserException("Unknown FsWriter argument: " + arg.Key);
            }
            return result;
        }
        protected virtual RepositoryReaderArgs ParseRepositoryReaderArgs(string[] args)
        {
            // [[-URL] Url]] [[-PATH ]Path]] [[-FILTER ]Filter]] [[-BLOCKSIZE ]BlockSize]] [[-APIKEY ]ApiKey]
            // [[-CLIENTID ]ClientId] [[-CLIENTSECRET ]ClientSecret]
            var result = new RepositoryReaderArgs();
            var parsedArgs = ParseSequence(args);

            if (parsedArgs.Length > 8)
                throw new ArgumentParserException("Too many RepositoryReader arguments.");

            foreach (var arg in parsedArgs)
            {
                if (arg.Key == "0" || arg.Key.Equals("URL", Cmp))
                {
                    if (result.Url != null)
                        throw new ArgumentParserException("Invalid RepositoryReader arguments.");
                    result.Url = arg.Value;
                }
                else if (arg.Key == "1" || arg.Key.Equals("PATH", Cmp))
                {
                    if (result.Path != null)
                        throw new ArgumentParserException("Invalid RepositoryReader arguments.");
                    result.Path = arg.Value;
                }
                else if (arg.Key == "2" || arg.Key.Equals("FILTER", Cmp))
                {
                    if (result.Filter != null)
                        throw new ArgumentParserException("Invalid RepositoryReader arguments.");
                    result.Filter = arg.Value;
                }
                else if (arg.Key == "3" || arg.Key.Equals("BLOCKSIZE", Cmp))
                {
                    if (result.BlockSize != null)
                        throw new ArgumentParserException("Invalid RepositoryReader arguments.");
                    if (!int.TryParse(arg.Value, out var intValue))
                        throw new ArgumentParserException("Invalid RepositoryReader argument: BlockSize: " + arg.Value);
                    result.BlockSize = intValue;
                }
                else if (arg.Key == "4" || arg.Key.Equals("APIKEY", Cmp))
                {
                    if (result.Authentication.ApiKey != null)
                        throw new ArgumentParserException("Invalid RepositoryReader arguments.");
                    result.Authentication.ApiKey = arg.Value;
                }
                else if (arg.Key == "5" || arg.Key.Equals("CLIENTID", Cmp))
                {
                    if (result.Authentication.ClientId != null)
                        throw new ArgumentParserException("Invalid RepositoryReader arguments.");
                    result.Authentication.ClientId = arg.Value;
                }
                else if (arg.Key == "6" || arg.Key.Equals("CLIENTSECRET", Cmp))
                {
                    if (result.Authentication.ClientSecret != null)
                        throw new ArgumentParserException("Invalid RepositoryReader arguments.");
                    result.Authentication.ClientSecret = arg.Value;
                }
                else
                    throw new ArgumentParserException("Unknown RepositoryReader argument: " + arg.Key);
            }
            return result;
        }
        protected virtual RepositoryWriterArgs ParseRepositoryWriterArgs(string[] args)
        {
            // [[-URL] Url]] [[-PATH ]Path]] [[-NAME ]Name]] [-CREATEONLY] [[-APIKEY ]ApiKey]
            // [[-CLIENTID ]ClientId] [[-CLIENTSECRET ]ClientSecret]
            var result = new RepositoryWriterArgs();
            var parsedArgs = ParseSequence(args);

            if (parsedArgs.Length > 7)
                throw new ArgumentParserException("Too many RepositoryWriter arguments.");

            foreach (var arg in parsedArgs)
            {
                if (arg.Key == "0" || arg.Key.Equals("URL", Cmp))
                {
                    if (result.Url != null)
                        throw new ArgumentParserException("Invalid RepositoryWriter arguments.");
                    result.Url = arg.Value;
                }
                else if (arg.Key == "1" || arg.Key.Equals("PATH", Cmp))
                {
                    if (result.Path != null)
                        throw new ArgumentParserException("Invalid RepositoryWriter arguments.");
                    result.Path = arg.Value;
                }
                else if (arg.Key == "2" || arg.Key.Equals("NAME", Cmp))
                {
                    if (result.Name != null)
                        throw new ArgumentParserException("Invalid RepositoryWriter arguments.");
                    result.Name = arg.Value;
                }
                else if (arg.Key == "3" || arg.Key.Equals("CREATEONLY", Cmp))
                {
                    if (result.CreateOnly)
                        throw new ArgumentParserException("Invalid RepositoryWriter arguments.");
                    result.CreateOnly = true;
                }
                else if (arg.Key == "4" || arg.Key.Equals("APIKEY", Cmp))
                {
                    if (result.Authentication.ApiKey != null)
                        throw new ArgumentParserException("Invalid RepositoryWriter arguments.");
                    result.Authentication.ApiKey = arg.Value;
                }
                else if (arg.Key == "5" || arg.Key.Equals("CLIENTID", Cmp))
                {
                    if (result.Authentication.ClientId != null)
                        throw new ArgumentParserException("Invalid RepositoryReader arguments.");
                    result.Authentication.ClientId = arg.Value;
                }
                else if (arg.Key == "6" || arg.Key.Equals("CLIENTSECRET", Cmp))
                {
                    if (result.Authentication.ClientSecret != null)
                        throw new ArgumentParserException("Invalid RepositoryReader arguments.");
                    result.Authentication.ClientSecret = arg.Value;
                }
                else
                    throw new ArgumentParserException("Unknown RepositoryWriter argument: " + arg.Key);
            }
            return result;
        }

        private string[] _boolSwitches = new[] {"FLATTEN"};
        private KeyValuePair<string, string>[] ParseSequence(string[] args)
        {
            var result = new Dictionary<string, string>();
            var index = 0;
            string name = null;
            foreach (var arg in args)
            {
                if (/*arg.StartsWith("/") || */arg.StartsWith("-"))
                {
                    if (name != null)
                        result.Add(name, null);
                    name = arg.Substring(1);
                    if (_boolSwitches.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Add(name, "true");
                        name = null;
                    }
                }
                else
                {
                    if (name != null)
                    {
                        result.Add(name, arg);
                        name = null;
                    }
                    else
                    {
                        result.Add(index++.ToString(), arg);
                    }
                }
            }
            if (name != null)
                result.Add(name, null);

            return result.OrderBy(x => x.Key).ToArray();
        }
    }
}

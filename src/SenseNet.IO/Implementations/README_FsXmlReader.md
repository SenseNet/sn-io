# FsXmlReader - SenseNet 6 XML Export Reader

## Overview

`FsXmlReader` is a filesystem reader implementation that reads old **SenseNet 6 XML-based exports** and converts them to the modern SenseNet format.

### SenseNet 6 vs. 7+ Structure Differences

| Feature | SenseNet 6 | SenseNet 7+ |
|---------|------------|-------------|
| **Export Format** | XML (`.Content` files) | JSON (`.Content` files) |
| **Root Structure** | `Root/Sites` | `Root/Content` |
| **System Folders** | `Root/(apps)`, `Root/Portlets`, `Root/Skins` | Removed or restructured |

## Usage

### Command Line Interface (CLI)

#### Converting XML Export to JSON (Filesystem to Filesystem)

**Basic usage:**
```bash
SenseNet.IO.CLI.exe COPYXML -source "c:\tmp\export2" -target "c:\tmp\exportuj"
```

**With skipped subtrees:**
```bash
SenseNet.IO.CLI.exe COPYXML -source "c:\tmp\export2" -SKIP "Root/(apps),Root/Skins,Root/Portlets" -target "c:\tmp\exportuj"
```

**With excluded fields (inline):**
```bash
SenseNet.IO.CLI.exe COPYXML -source "c:\tmp\export2" -EXCLUDEFIELDS "CreatedBy,ModifiedBy,CreationDate,ModificationDate" -target "c:\tmp\exportuj"
```

**With configuration file:**
```bash
SenseNet.IO.CLI.exe COPYXML --config appsettings.fsxmlreader.json
```

Example `appsettings.fsxmlreader.json`:
```json
{
  "fsXmlReader": {
    "Path": "c:\\tmp\\export2",
    "Skip": ["Root/(apps)", "Root/Skins", "Root/Portlets"],
    "ExcludedFields": [
      "ApprovingMode", "CreatedBy", "CreationDate", "ModificationDate",
      "ModifiedBy", "Actions", "Id", "Path", "Type", "Version"
    ]
  },
  "fsWriter": {
    "Path": "c:\\tmp\\exportuj"
  }
}
```

#### Importing XML Export to Repository

```bash
SenseNet.IO.CLI.exe IMPORTXML -source "c:\tmp\export2" -target "https://mysite.sensenet.cloud" -APIKEY "your-api-key"
```

### Programmatic Usage

```csharp
var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());

var excludedFields = new[]
{
    "ApprovingMode", "CreatedBy", "CreationDate", "ModificationDate", "ModifiedBy",
    "Actions", "Id", "Path", "Type", "Version", "Children"
};

var readerArgs = Options.Create(new FsXmlReaderArgs 
{ 
    Path = @"c:\tmp\export2",
    Skip = new[] { "Root/(apps)", "Root/Skins", "Root/Portlets" },
    ExcludedFields = excludedFields
});
var reader = new FsXmlReader(readerArgs, loggerFactory.CreateLogger<FsXmlReader>());

var writerArgs = Options.Create(new FsWriterArgs { Path = @"c:\tmp\exportuj" });
var writer = new FsWriter(writerArgs, loggerFactory.CreateLogger<FsWriter>());

await reader.InitializeAsync();
await writer.InitializeAsync();

var cancel = CancellationToken.None;
while (await reader.ReadAllAsync(Array.Empty<string>(), cancel))
{
    var content = reader.Content;
    var relativePath = reader.RelativePath;

    if (string.IsNullOrEmpty(relativePath))
        relativePath = content.Name;

    await writer.WriteAsync(relativePath, content, cancel);
}
```

## Field Filtering

When migrating from SenseNet 6 to 7+, certain fields should be excluded:

### Excluded Import Fields
- System audit fields: `CreatedBy`, `CreationDate`, `ModificationDate`, `ModifiedBy`
- Versioning fields: `VersionCreatedBy`, `VersionCreationDate`, `VersionModificationDate`, `VersionModifiedBy`
- Deprecated fields: `ApprovingMode`, `InheritableApprovingMode`, `InheritableVersioningMode`, `IsRateable`, `IsTaggable`, `TrashDisabled`

### Excluded Export Fields
- Computed/runtime fields: `Actions`, `Children`, `Path`, `Type`, `Version`, `Locked`, `Icon`, etc.
- Internal IDs: `Id`, `ParentId`, `OwnerId`, `CreatedById`, `ModifiedById`

## Path Transformations

The reader automatically handles path transformations:

- `Root/Sites` → `Root/Content`
- Other mappings can be added as needed

## XML Format

Old SenseNet 6 `.Content` files use this XML structure:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ContentMetaData>
  <ContentType>GenericContent</ContentType>
  <ContentName>myfile</ContentName>
  <Fields>
    <DisplayName>My File</DisplayName>
    <Description>This is a description</Description>
    <Binary attachment="myfile.pdf" />
  </Fields>
  <Permissions>
    <!-- Permission entries -->
  </Permissions>
</ContentMetaData>
```

## Implementation Details

- **FsXmlContent**: Parses XML `.Content` files and implements `IContent` interface
- **FsXmlReader**: Traverses filesystem and reads XML metadata, implements `IFilesystemReader`
- **FsXmlReaderArgs**: Configuration options (path, skip list)

## Testing

See `FsXmlReaderTests.cs` for comprehensive test examples including:
- Programmatic conversion
- CLI argument parsing
- Field filtering and path transformation

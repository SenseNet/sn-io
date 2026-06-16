# Field Filtering in FsXmlReader - Usage Guide

## Overview

The `FsXmlReader` supports the excluded fields feature, which allows you to filter out unnecessary or obsolete fields from old SenseNet 6 XML exports during conversion to the newer format.

## Usage Methods

### 1. Command Line Parameter (inline)

The simplest method is to specify the fields to exclude directly in the command line:

```bash
SenseNet.IO.CLI.exe COPYXML -source "c:\tmp\export2" -EXCLUDEFIELDS "CreatedBy,ModifiedBy,CreationDate,Actions,Id" -target "c:\tmp\exportuj"
```

**Advantages:**
- Fast, simple
- Works well for a few fields
- No separate configuration file needed

**Disadvantages:**
- Cumbersome for many fields
- Not reusable

### 2. JSON Configuration File

For longer lists, it is more practical to use a JSON configuration file:

**appsettings.fsxmlreader.json:**
```json
{
  "fsXmlReader": {
    "Path": "c:\\tmp\\export2",
    "Skip": [
      "Root/(apps)",
      "Root/Skins",
      "Root/Portlets"
    ],
    "ExcludedFields": [
      "ApprovingMode",
      "CreatedBy",
      "CreationDate",
      "DateTaken",
      "GroupAttachments",
      "IconName",
      "InheritableApprovingMode",
      "InheritableVersioningMode",
      "IsRateable",
      "IsTaggable",
      "ModificationDate",
      "ModifiedBy",
      "NotificationMode",
      "Owner",
      "PageCount",
      "RateAvg",
      "RateCount",
      "Sharing",
      "SeeAlso",
      "TrashDisabled",
      "VersionCreatedBy",
      "VersionCreationDate",
      "VersionModificationDate",
      "VersionModifiedBy",
      "VersioningMode",
      "Actions",
      "AllFieldSettingContents",
      "AllRoles",
      "Approvable",
      "AvailableContentTypeFields",
      "AvailableViews",
      "BrowseApplication",
      "BrowseUrl",
      "CheckInComments",
      "CheckedOutTo",
      "Children",
      "CreatedById",
      "Depth",
      "DirectRoles",
      "EffectiveAllowedChildTypes",
      "ExtensionData",
      "FieldSettingContents",
      "HandlerName",
      "Icon",
      "Id",
      "Image",
      "InFolder",
      "InTree",
      "IsFile",
      "IsFolder",
      "IsSystemContent",
      "Locked",
      "ModifiedById",
      "Name",
      "OwnerId",
      "OwnerWhenVisitor",
      "ParentId",
      "ParentTypeName",
      "Path",
      "Publishable",
      "Rate",
      "RateStr",
      "RejectReason",
      "SavingState",
      "SharedBy",
      "SharedWith",
      "SharingLevel",
      "SharingMode",
      "Tags",
      "Type",
      "TypeIs",
      "Version",
      "VersionId",
      "Versions",
      "Workspace",
      "WorkspaceSkin"
    ]
  },
  "fsWriter": {
    "Path": "c:\\tmp\\exportuj"
  }
}
```

**Usage:**
```bash
SenseNet.IO.CLI.exe COPYXML --config appsettings.fsxmlreader.json
```

**Advantages:**
- Reusable
- Easy to maintain
- Manageable even with many fields
- Shareable with the team

**Disadvantages:**
- Requires handling a separate file

### 3. Programmatic Usage

For usage in code:

```csharp
var excludedFields = new[]
{
    // System audit fields
    "CreatedBy", "CreationDate", "ModificationDate", "ModifiedBy",
    "VersionCreatedBy", "VersionCreationDate", "VersionModificationDate", "VersionModifiedBy",
    
    // Computed/runtime fields
    "Actions", "Children", "Path", "Type", "Version", "Id",
    
    // Deprecated SenseNet 6 fields
    "ApprovingMode", "InheritableApprovingMode", "InheritableVersioningMode",
    "IsRateable", "IsTaggable", "TrashDisabled"
};

var readerArgs = Options.Create(new FsXmlReaderArgs 
{ 
    Path = @"c:\tmp\export2",
    Skip = new[] { "Root/(apps)", "Root/Skins", "Root/Portlets" },
    ExcludedFields = excludedFields
});

var reader = new FsXmlReader(readerArgs, loggerFactory.CreateLogger<FsXmlReader>());
```

## Recommended Excluded Fields List

### For SenseNet 6 → 7+ Migration

#### Import Fields (should not be copied to the target)
```
ApprovingMode
CreatedBy
CreationDate
DateTaken
GroupAttachments
IconName
InheritableApprovingMode
InheritableVersioningMode
IsRateable
IsTaggable
ModificationDate
ModifiedBy
NotificationMode
Owner
PageCount
RateAvg
RateCount
Sharing
SeeAlso
TrashDisabled
VersionCreatedBy
VersionCreationDate
VersionModificationDate
VersionModifiedBy
VersioningMode
```

#### Export Fields (computed/system fields)
```
Actions
AllFieldSettingContents
AllRoles
Approvable
AvailableContentTypeFields
AvailableViews
BrowseApplication
BrowseUrl
CheckInComments
CheckedOutTo
Children
CreatedById
Depth
DirectRoles
EffectiveAllowedChildTypes
ExtensionData
FieldSettingContents
HandlerName
Icon
Id
Image
InFolder
InTree
IsFile
IsFolder
IsSystemContent
Locked
ModifiedById
Name
OwnerId
OwnerWhenVisitor
ParentId
ParentTypeName
Path
Publishable
Rate
RateStr
RejectReason
SavingState
SharedBy
SharedWith
SharingLevel
SharingMode
Tags
Type
TypeIs
Version
VersionId
Versions
Workspace
WorkspaceSkin
```

## Examples

### 1. Full conversion with the list used in tests

```bash
SenseNet.IO.CLI.exe COPYXML ^
  -source "c:\tmp\export2" ^
  -SKIP "Root/(apps),Root/Skins,Root/Portlets" ^
  -EXCLUDEFIELDS "ApprovingMode,CreatedBy,CreationDate,ModificationDate,ModifiedBy,Actions,Id,Type,Version,Children" ^
  -target "c:\tmp\exportuj"
```

### 2. Excluding only the most important fields

```bash
SenseNet.IO.CLI.exe COPYXML ^
  -source "c:\tmp\export2" ^
  -EXCLUDEFIELDS "Id,Path,Type,Version,Actions,Children" ^
  -target "c:\tmp\exportuj"
```

### 3. With configuration file

```bash
SenseNet.IO.CLI.exe COPYXML --config appsettings.fsxmlreader.json
```

## Notes

- `ExcludedFields` is case-insensitive
- Fields can be separated by comma or semicolon
- If a field is not present in the XML, it will not cause an error
- Excluded fields are filtered out during reading, saving memory

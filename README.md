# SnIO: Import and export for the sensenet repository
SnIO is a library and command line tool designed to help operators and developers copying content from and to a content repository. It is able to connect to a sensenet content repository service through _http_ and download/upload content items - or even copy content items between two repositories directly.

The source and target are completely independent - either of them can be a repository or the file system. You can define a subtree to export and filter the source so that only a subset of the content tree is exported.

> **Main use cases**: the SnIO tool is useful when you want to **archive** or **back up** content, want to **periodically update** content from a staging service to production. It is also useful if you have a predefined content structure you want to **import** on new sensenet instances.

## Scenarios
There are four scenarios offered by the built-in tool.
- **[EXPORT](#EXPORT)**: Transfer content from a sensenet repository to the file system.
- **[IMPORT](#IMPORT)**: Transfer content from the file system to a sensenet repository.
- **[COPY](#COPY)**: Transfer content between file system directories :).
- **[SYNC](#SYNC)**: Transfer content from a sensenet repository to another repository.

> **For developers**: SnIO is extendable. You can extend our default content tree readers and writers, or implement your own. Currently you'll have to look into the source code for examples.

## Parameters
The SnIO tool has a number of parameters with some restrictions. The general form is the following:
```
SnIO <Scenario> [General parameters] [-SOURCE <Source parameters>] [-TARGET <Target parameters>]
```

### General parameters
SnIO has its own configuration file, as you will see later in this article. All configured values can be overwritten using environment variables or in the command line using the usual .NET Core rules (see the [command-line arguments](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-5.0#command-line-arguments-1 "Microsoft documentation") and configuration in the .NET Core documentation).

### Parameter name restrictions
- Must start with the minus (`-`) character.
- Cannot start with a slash (`/`) to avoid collisions because repository paths start with this character.
- Case insensitive but here in the docs we write them in all capital letters for easier recognition.

### Parameter sections
The source and target parameters are grouped into two sections. A section should start with `-SOURCE` or `-TARGET` and it ends with another section or end of the parameter list. Sections are interchangeable.

The default source and target parameters can be configured (see below).
If all section parameters are configured or default, the section selector is optional.

Section parameters have a default order (see under every scenario below). If the order is followed - parameters are not swapped and there is no missing item between parameters - parameter names are optional. In the following example all source parameters are given in the right order (url, path, blocksize):
```
SnIO EXPORT -SOURCE "https://example.sensenet.cloud" "/Root/Content" 50
```
In the next example the url is omitted (the configured default is used). In this case the first parameter is missing, so the parameter names are required.
```
SnIO EXPORT -SOURCE -PATH "/Root/Content" -BLOCKSIZE 50
```
Considering the rules above, the simplest parameterization is the empty scenario, if all parameters are configured or default:
```
SnIO EXPORT
```
### Help screens
If the parameter list contains a question mark or the `-HELP` parameter, the help screen will be displayed. The help switch can also be used after a scenario keyword. In that case only the scenario's help screen will appear. The main help screen command alternatives:
```
SnIO ?
SnIO -?
SnIO -HELP
```
Scenario help commands:
```
SnIO IMPORT ?
SnIO IMPORT -?
SnIO IMPORT -HELP
```

## EXPORT
Copies a subtree from a sensenet repository to the file system. The source repository is defined only by a single URL, it can be hosted anywhere.

> **Authentication**: the tool has to have access to the source repository. See details in the [Authentication](#Authentication) section below.

The target file system directory will be created if it does not exist. If it does and it contains content items, the transferred material will be merged with the existing files. Existing content files will remain intact if the `-FLATTEN` parameter is used, otherwise they will be overwritten.

### Source parameters
1. **URL**: Url of the source sensenet repository e.g. 'https://example.sensenet.cloud'.
2. **PATH**: Repository path of the root content of the tree to transfer. Default: '/Root'.
3. **FILTER**: Filter the selected subtree. The filter is written in the [Content Query Language](https://docs.sensenet.com/api-docs/querying) format. Only terms and logical operators are effective, keywords (.TOP, .SKIP, .SORT etc.) are omitted.
4. **BLOCKSIZE**: Count of items in one request. Default: 10.

### Target parameters
1. **PATH**: Fully qualified path of a target file system directory.
2. **NAME**: Name of the target tree root if it is different from the source name.
3. **FLATTEN**: Boolean switch without a parameter value. If it is provided, every content will be written into the same target file system directory. Files with the same name will be renamed (suffixed with a number). The original name and path can be found in the `*.Content` metafiles. _Warning_: flattening a big result set can degrade the writing performance.

### Examples
```
SnIO EXPORT -SOURCE "https://example.sensenet.cloud" "/Root/Content" -TARGET "D:\Backup\example.sensenet.cloud"
```
As a result the following file system entry appears: `D:\Backup\example.sensenet.cloud\Content` and under that the whole subtree's content items in the same folder structure as in the repository.

The subtree can also be saved with another name (see the last parameter):
```
SnIO EXPORT -SOURCE "https://example.sensenet.cloud" "/Root/Content" -TARGET "D:\Backup\example.sensenet.cloud" -NAME "OldContents"
```
In this case the new root file system entry is: `D:\Backup\example.sensenet.cloud\OldContents`.

A common scenario is exporting a subset of content items from a developer repository and importing it to another. The following command exports all items that were modified in the last 7 days (source url and path are configured):
```
SnIO EXPORT -SOURCE -FILTER "ModificationDate:>@@Today-7@@" -TARGET "D:\Release\example.sensenet.cloud"
```

If the goal is to collect filtered results in one place, use the filter and the flattening:
```
SnIO EXPORT -SOURCE "https://example.sensenet.cloud" "/Root/Content" "+TypeIs:File +Name:My*" -TARGET "D:\Backup\MyFiles" -FLATTEN
```
This command copies all files starting with "My" to the "MyFiles" folder.

## IMPORT
Copies a subtree from the file system to a sensenet repository.

> **Authentication**: the tool has to have access to the target repository. See details in the [Authentication](#Authentication) section below.

The target container will be created if it does not exist. If it does, the transferred content items will be merged, existing contents will be "patched" (only the pushed fields will change).

### Source parameters
1. **PATH**: Fully qualified path of the file system entry to read.

### Target parameters
1. **URL**: Url of the target sensenet repository, e.g. 'https://example.sensenet.cloud'.
2. **PATH**: Repository path of the target container. Default: '/'.
3. **NAME**: Name of the target tree root if it is different from the source name.

### Example
The following example restores a backup material. Note that the source and target names are different. Parameter names are optional because the parameters are ordered as specified.
```
SnIO IMPORT -SOURCE "D:\Backup\example.sensenet.cloud\Content_2021-09-27" -TARGET "https://example.sensenet.cloud" "/Root" "Content"
```

## COPY
Copies a subtree from the file system to another directory in the file system.

The target file system directory will be created if it does not exist. If it does, transferred items will be merged, existing file system entries will be overwritten.

### Source parameters
See the source parameters of the IMPORT scenario.

### Target parameters
See the target parameters of the EXPORT scenario.

## SYNC
Copies a subtree from a sensenet repository to another sensenet repository.

The target container will be created if it does not exist. If it does, transferred material will be merged, existing contents will be "patched" (only the pushed fields will change).

### Source parameters
See the source parameters of the EXPORT scenario.

### Target parameters
See the target parameters of the IMPORT scenario.

## Configuration
The SnIO tool uses two config files. The first is the default `appsettings.json`. It contains the general configuration of display appearance and logging. The second is `providerSettings.json` that contains the default parameter values of the built-in SnIO providers.
### appsettings.json
#### display.level
This section controls the details of writing to the console. There is only one value: `level`. Valid values: "None", "Progress", "Errors", "Verbose". Default: "Errors".

```json
{
  "display": {
    "level": "Errors"
  }
}
```

### Serilog
This app uses the [Serilog](https://serilog.net/ "Serilog official site") package for logging. See it's documentation [here](https://github.com/serilog/serilog/wiki "Serilog wiki").

### Examples
In these examples configuration values will be overwritten in the command line using the .NET Core "full-path" form.

Parts of appsettings.json
```json
{
  "display": {
    "level": "Errors"
  },
  "Serilog": {
    /* ... */
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs/SenseNet.IO-.txt",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```
In the firs example the `display.level` is overwritten to display detailed information:
```
SnIO EXPORT --DISPLAY:LEVEL Verbose -SOURCE -PATH "/Root/Content"
```
In the second example the log-file name pattern will be changed (consider that an array is accessed by the index of the desired item):
```
./SnIO.exe EXPORT --Serilog:WriteTo:0:Args:path "logs/SnIO-.txt" -SOURCE -PATH /Root/IMS
```

### providerSettings.json
SnIO scenarios always use two providers: one source and one target. In this version there are four providers available that can drive these scenarios. Here are the valid combinations:

|            | SOURCE           | TARGET           |
| ---------- | ---------------- | ---------------- |
| **EXPORT** | repositoryReader | fsWriter         |
| **IMPORT** | fsReader         | repositoryWriter |
| **COPY**   | fsReader         | fsWriter         |
| **SYNC**   | repositoryReader | repositoryWriter |

The providers have several parameters. To simplify SnIO usages, default values can be configured. Here is an annotated example:

```json
{
  "repositoryReader": {
    "url": "https://localhost:44362", // Source URL
    "path": null, // Source path. Default: "/Root",
    "blockSize": null // Contents per request. Default (null, 0 or less): 10. 
  },
  "repositoryWriter": {
    "url": "https://localhost:44362", // Target URL
    "path": null, // Target path. Default: "/",
    "name": null // Target name under the container. Default: name of the reader's root.
  },
  "fsReader": {
    "path": "D:\\_sn-io-test\\localhost_44362\\Root" // Source root
  },
  "fsWriter": {
    "path": "D:\\_sn-io-test\\localhost_44362", // Target container. Will be created if does not exists.
    "name": null // Target name under the container. Default: name of the reader's root.
  }
}
```

## Authentication
Authenticating works the same way in case of the source and the target repository: it uses the same client/secret technique other sensenet tools use. You provide the clientid and secret values for the repository reader and/or writer in the configuration (or overwrite them in environment variables or command line) and let the tool get the auth token from the corresponding authority related to the sensenet content repository.

To obtain the necessary values, please log in to your repository on the admin ui and visit the [Security settings page](https://docs.sensenet.com/guides/settings/api-and-security). There are also OData actions that will let you manage these values. 

### Examples
```json
{
  "repositoryReader": {
    "Authentication": {
      "ClientId": "...",
      "ClientSecret": "..." 
    } 
  },
  "repositoryWriter": {
    "Authentication": {
      "ClientId": "...",
      "ClientSecret": "..." 
    } 
  }
}
```
# Following server-assigned import paths

The Import endpoint may return a different `path` or `name` after ContentNaming.
The importer must treat that response as the identity of the imported content.
Naming remains entirely on the server.

`SemanticContentFlow` keeps an import-scoped map from planned absolute target paths
to returned paths. Descendants use the longest known complete ancestor, so a mapping
for `/News` cannot affect `/Newsletter`. Progress, task files and subtree existence
checks retain the actual writer path. Deferred tasks keep the original reader path
for reloading source metadata, and their actual writer path for the destination.
When revisiting a mapped content, its outgoing ContentName/Name is the actual leaf.

`RepositoryWriter` parses the Import response before Binary or property uploads.
Uploads address the actual parent and leaf, and an upload failure retains that
destination in the failed state. The writer also remembers returned aliases for
repeated calls. Content type definitions are uploaded first as required by the
server; the returned upload path/name is used for subsequent properties and Import.

## Reference and prefix boundaries

The export format does not identify the types of arbitrary metadata fields.
Only fields explicitly listed by the server in `brokenReferences` are remapped on
the deferred pass. Their scalar repository paths and arrays of scalar paths use
the same map. Numeric IDs and structured objects retain their representation.
Permission entry identities are explicit paths and can be remapped when the server
requests a permission retry. Request views leave the source metadata unchanged.

Queries, body text, links and other ordinary strings are never searched/replaced.
This is not a package-root migration: old-site references outside the planned
target tree stay unchanged. An explicit writer root name changes the destination
tree; it does not imply rewriting arbitrary old-root references in package fields.

## Existing content and repeated runs

A fresh normal import relearns paths from server responses. With stable server
naming, importing the same package again updates the same tree. The regression
suite checks a completely fresh flow/writer for that second run.

CreateOnly checks the resolved known target. It can skip exact canonical paths and
aliases already learned by that writer. Deferred fields on content created by that
writer may be completed even with CreateOnly; skipped existing content gets no such
update. A fresh CreateOnly run cannot discover a previously normalized alias before
Import using the current server API. It may therefore update an existing canonical
content if only the original alias is supplied. Do not use CreateOnly as protection
for that unresolved case. No client-side normalization or speculative server query
is added to guess the missing identity.

## Verification

Run `dotnet test src/SenseNet.IO.Tests/SenseNet.IO.Tests.csproj -c Release`.
`RepositoryWriterHttpTests` uses real FsReader, DI, RepositoryWriter, SenseNet.Client
and loopback HTTP. The endpoint is a **synthetic contract fixture**, with explicit
unpredictable name aliases and strict parent checks, rather than a real SenseNet
backend. It checks accent/space names, multiple renamed ancestors, root rename,
primary/property binaries, reference and permission retries, fresh-flow idempotence,
supported CreateOnly cases, response-name fallback and failed-upload recovery.
`ContentPathRemappingTests` checks segment boundaries and unchanged opaque metadata.

The untouched origin/develop `2992544` baseline has one existing failing test,
`App_DisplaySettings_DisplayLevel_Fake` (135/136 pass). Its assertion expects an
exception during app construction, before the lazy DisplayLevel getter is read.
This repair does not change that unrelated behavior. Run the remaining suite with
`--filter FullyQualifiedName!~App_DisplaySettings_DisplayLevel_Fake` to isolate it;
retain an unfiltered result alongside it.

A separate local SenseNet snapshot verified a first import of 206 package items
with server naming enabled: zero import errors, matching actual paths/types and
body hashes, and matching SHA256 hashes for all 100 existing attachments. A fresh
second CLI process exposed 91 failures from an existing composite `Image` field
export/import incompatibility, with no parent-path or HTTP 413 errors and unchanged
content/binaries. The synthetic fixture does not validate this composite-field
contract. Full-package idempotence requires that separate compatibility fix;
the path-following regressions alone do not establish it.

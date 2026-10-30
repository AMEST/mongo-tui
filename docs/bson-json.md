# BSON and document editing

Both clients use `BsonTextCodec` in Application for Canonical Extended JSON. The
codec is used for editor text, JSON results, copying documents and browser result
downloads. Web array imports use its compact representation. Collection exports
use MongoDB Database Tools with `--jsonFormat=canonical`; this is also the default
format in the TUI export dialog. Explicit relaxed JSON and CSV remain export options.
`BsonDisplay` only provides short previews and CSS types for Web rendering.

## Editing an existing document

1. Keep the selected result as a `BsonDocument` and identify its source collection.
   Arbitrary scripts, projections and transformed results remain read only.
2. Call `DocumentService.LoadForEditAsync` with the selection. It performs one
   driver read by `_id`; it never executes the original script again. The editor
   therefore opens the current stored document, which may be newer than the list.
3. Verify BSON → canonical text → BSON. Compare serialized BSON bytes, including
   numeric types, nested values and field order, rather than `BsonDocument.Equals`
   (which can equate different numeric types).
4. Enable saving only after a successful read and round-trip check. Refuse editing
   if Extended JSON cannot represent the original document exactly, including a
   literal nested document such as `{"$date":"2020-01-01T00:00:00Z"}`.
5. Parse the user's text with the common codec, verify that `_id` and its BSON type
   have not changed, then replace the document. An unchanged replacement is a
   success when it matches the original identity.

Canonical JSON preserves types present in the input; it cannot reconstruct types
already lost by `mongosh`. In particular, a stored whole `Double(42.0)` can reach
the shell output as `Int32(42)`. Driver reload prevents this result representation
from being used as the replacement document. Shell result displays and downloads
still reflect the shell's returned types.

The current BSON driver normalizes `Double(-0.0)` to positive zero while reading.
The codec detects a negative zero still present in memory, but cannot recover its
sign after driver normalization. Tests document this limitation; this workflow
does not promise byte-exact preservation of values the driver already normalizes.
Use BSON tools for a byte-exact backup. Document replacement also does not detect
concurrent writes made after the editor was opened.

## Verification and CI

Unit tests cover the codec, identity validation, unsafe documents and editor
availability. Integration tests create typed BSON directly in a disposable MongoDB
container, read through the driver or actual `mongosh`, save through the common
service, TUI model or Web dialog, and compare stored BSON bytes. Both no-op saves
and editing one unrelated field are covered, including nested `Int64(42)` and
`Double(42.0)`, precision boundaries, dates, binary/UUID, regex and JavaScript scope.
Unsafe literal wrappers must leave the database untouched.

Run the suite locally with Docker available:

```sh
dotnet test tests/MongoTui.IntegrationTests -c Release
```

The `integration-tests` GitHub Actions job runs this same suite for pull requests
and pushes to `master`, and uploads TRX results. `MongoContainerProcessRunner`
executes the production shell command using the real `mongosh` bundled in the test
MongoDB image; no host shell installation, application database or secrets are
required. A failing test fails the job. Making that check mandatory for merging
is a repository branch-protection setting.

---
applyTo: "Kavita.Models/Entities/**/*.cs,Kavita.Models/DTOs/**/*.cs,Kavita.Models/Attributes/ExportScrubAttribute.cs,Kavita.Database/DataContext.cs,Kavita.Services/DebugExport/**/*.cs,Kavita.Services/Plus/KavitaPlusAuditService.cs,Kavita.Services.Tests/DebugExport/ReviewedTextFields.cs"
excludeAgent: "cloud-agent"
---
# Debug export scrub review

The debug export ships a copy of the database to a developer. Every text value that can hold a secret or personal data is replaced first. Kavita.Services/DebugExport/ExportScrubCatalog.cs decides what is replaced. A value is scrubbed only when it is marked; anything unmarked ships as it is. Flag every gap below as a privacy issue, naming the property.

## Where a value gets its classification
- `[ExportScrub(ExportScrubKind.X)]` (Kavita.Models/Attributes/ExportScrubAttribute.cs) on an entity property, on a property of a type stored as JSON in a column, or on a `ServerSettingKey` member.
- `ExportScrubCatalog.FrameworkFallback`, only for properties declared on ASP.NET Identity base classes, where the attribute can't be placed.
- `Kavita.Services.Tests/DebugExport/ReviewedTextFields.cs`, a list of `Type.Property` names someone checked and found safe to ship unchanged.

`ExportScrubCatalogTests` fails when a text field is in none of these. It cannot tell whether the choice was right, so that is what to review.

## Flag these
1. **New or renamed text field with no classification.** A `string`, `string?`, or list or dictionary of strings is added or renamed on an entity in `Kavita.Models/Entities`, on a type stored as JSON in a column (an owned `ToJson` type, a value-converted property, or a type named by `[ExportScrub(ExportScrubKind.Json, typeof(T))]`), or a `ServerSettingKey` member is added, and the PR neither marks it nor adds its `Type.Property` name to `ReviewedTextFields`. A rename leaves the old name in `ReviewedTextFields`; flag that too.
2. **Sensitive value put on the reviewed list.** A `ReviewedTextFields` entry, new in this PR, whose name or use suggests a password, hash, token, API key, secret, license key, email, IP address, host name, username, phone number, OIDC or provider id, or text a user typed (reviews, comments, names they chose). These need `[ExportScrub]`. The reason it's on the list must be evident: a path, a public URL, a language code, a series or person name from metadata.
3. **Wrong kind.** The kind must fit the value: `Random` for secrets and tokens, `Email`, `IpAddress`, `Host` (URLs and host names), `Username`, `Guid`, `Phone`, `FreeText` for text a user wrote, `TextScrub` for machine text that can embed any of those (error messages, email subjects), and only on an entity column: `ExportScrubCatalog.Build` throws when a JSON field or a `ServerSettingKey` member carries it, `Fingerprint` for hashes that must stay comparable, `Json` for a string column holding JSON. Example of the bug: an email column marked `Random` loses the `user-3@scrubbed.invalid` form that lets logs and the database agree.
4. **JSON column whose type depends on the row.** `KavitaPlusAuditLog.Payload` holds a different DTO per `KavitaPlusEventType`. A new event type must be added to `KavitaPlusAuditPayloads.TypesFor` (Kavita.Models/DTOs/KavitaPlus/Audit/KavitaPlusAuditPayloads.cs) with the payload types its `LogAsync` calls write, and a new `AuditLog*ParamsDto` must be reachable from it. A `LogAsync` call that logs an existing DTO under an event that doesn't list it is the same gap: `LogAsync` only warns at runtime.
5. **New entity table holding only secrets.** A table like `DataProtectionKeys`, whose rows are useless without the secret, should be dropped in the export (`ExportScrubCatalog.DroppedTables`), not scrubbed column by column.

## Do not flag
- Non-text properties: ints, bools, dates, enums, ids. The catalog ignores them.
- DTOs that are only API request or response bodies and are never stored in a column.
- Existing `ReviewedTextFields` entries the PR doesn't touch.

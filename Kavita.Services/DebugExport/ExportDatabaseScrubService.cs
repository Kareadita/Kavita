using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Kavita.API.Services;
using Kavita.Common.Helpers;
using Kavita.Database;
using Kavita.Database.Repositories;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.DebugExport;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Kavita.Models.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.DebugExport;

/// <summary>
/// Opens the snapshot in one transaction, asks <see cref="ExportScrubCatalog.Build"/> what to scrub, rewrites each planned
/// table and server setting, then adds the <c>kavita-debug</c> admin
/// </summary>
public class ExportDatabaseScrubService(
    ILogger<ExportDatabaseScrubService> logger,
    IAccountService accountService,
    IMapper mapper)
    : IExportDatabaseScrubService
{
    private const int BatchSize = 1_000;
    private const string AdminUserName = "kavita-debug";
#pragma warning disable S2068
    private const string PasswordCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
#pragma warning restore S2068
    private const int PasswordLength = 24;

    /// <inheritdoc />
    public async Task<ExportScrubResult> ScrubAsync(string snapshotPath, CancellationToken ct = default)
    {
        // Pooled connections keep the file open after dispose, and the export zips and deletes it next
        var connectionString = new SqliteConnectionStringBuilder { DataSource = snapshotPath, Pooling = false }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var transaction = (SqliteTransaction) await connection.BeginTransactionAsync(ct);
        await using var context = new DataContext(new DbContextOptionsBuilder<DataContext>().UseSqlite(connection).Options);
        await context.Database.UseTransactionAsync(transaction, ct);

        var run = new ScrubRun(ExportScrubCatalog.Build(context.Model), new ExportJunk(), connection, transaction);
        await run.ClaimAccountUsernames(context.Model, ct);

        foreach (var table in run.Plan.Tables)
        {
            var changed = await run.ScrubTable(table, ct);
            logger.LogDebug("Debug export scrubbed {Rows} rows in {Table}", changed, table.Table);
        }
        await run.ScrubSettings(context.Model, ct);
        await run.ScrubText(ct);

        // After the scrub or the admin's own hash and keys would be replaced
        var admin = await InjectAdmin(context, ct);

        await transaction.CommitAsync(ct);

        // Re-vacuum so replaced fields don't leave their original bytes behind in the freed space of each page
        await using (var vacuum = connection.CreateCommand())
        {
            vacuum.CommandText = "VACUUM";
            await vacuum.ExecuteNonQueryAsync(ct);
        }

        logger.LogInformation("Debug export scrubbed {Values} distinct values in {Tables} tables", run.Junk.Replaced.Count, run.Plan.Tables.Count);

        return new ExportScrubResult(run.Junk, run.Usernames, AdminUserName, admin.Password, admin.AuthKey);
    }

    /// <summary>
    /// The account an admin would get from an invite with the Admin role, so the dev opening the export can log in
    /// </summary>
    private async Task<InjectedAdmin> InjectAdmin(DataContext context, CancellationToken ct)
    {
        var password = RandomNumberGenerator.GetString(PasswordCharacters, PasswordLength);
        var theme = await new SiteThemeRepository(context, mapper).GetDefaultTheme();

        var user = new AppUserBuilder(AdminUserName, $"{AdminUserName}@scrubbed.invalid", theme).Build();
        user.NormalizedUserName = user.UserName!.ToUpperInvariant();
        user.NormalizedEmail = user.Email!.ToUpperInvariant();
        user.EmailConfirmed = true;
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, password);
        user.AgeRestriction = AgeRating.NotApplicable;
        user.AgeRestrictionIncludeUnknowns = true;
        accountService.SeedUser(user);

        var roles = await context.Roles
            .Where(r => r.Name == PolicyConstants.AdminRole || r.Name == PolicyConstants.LoginRole)
            .ToListAsync(ct);
        user.UserRoles = roles.Select(role => new AppUserRole { Role = role }).ToList();

        foreach (var library in await context.Library.ToListAsync(ct))
        {
            user.Libraries.Add(library);
            user.CreateSideNavFromLibrary(library);
        }

        context.AppUser.Add(user);
        await context.SaveChangesAsync(ct);

        return new InjectedAdmin(password, user.AuthKeys.First(k => k.Name == AuthKeyHelper.OpdsKeyName).Key);
    }

    /// <summary>
    /// Raw SQL addressed by rowid, never change tracking: a tracked save resets <c>LastModified</c> and bumps <c>RowVersion</c>
    /// </summary>
    private sealed class ScrubRun(ExportScrubCatalog.Plan plan, ExportJunk junk, SqliteConnection connection, SqliteTransaction transaction)
    {
        private readonly Dictionary<string, string> _usernames = [];
        private ExportTextScrubber? _text;

        public ExportScrubCatalog.Plan Plan => plan;
        public ExportJunk Junk => junk;
        public IReadOnlyDictionary<string, string> Usernames => _usernames;

        /// <summary>
        /// Maps every account's name to its <c>user-&lt;Id&gt;</c> before any table is scrubbed. Otherwise the first column to
        /// meet a name claims it: an AniList handle <c>amelia</c> on user 1 would make account <c>amelia</c> <c>user-1</c> too
        /// </summary>
        public async Task ClaimAccountUsernames(IModel model, CancellationToken ct)
        {
            var entityType = model.FindEntityType(typeof(AppUser))!;
            var id = Quote(entityType.FindPrimaryKey()!.Properties[0].GetColumnName());
            var userName = Quote(entityType.FindProperty(nameof(AppUser.UserName))!.GetColumnName());
            await using var select = Command($"SELECT {id}, {userName} FROM {Quote(entityType.GetTableName()!)}");

            await using var reader = await select.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (reader.IsDBNull(1)) continue;

                var user = $"user-{reader.GetInt64(0)}";
                Replace(ExportScrubKind.Username, reader.GetString(1), new RowOwner(user, user));
            }
        }

        /// <summary>
        /// Deletes every row of a dropped table. Otherwise, reads <see cref="BatchSize"/> rows at a time in rowid order
        /// and writes back only the rows where a value changed
        /// </summary>
        /// <returns>The number of rows deleted or changed</returns>
        public async Task<int> ScrubTable(ExportScrubCatalog.TableScrub table, CancellationToken ct)
        {
            if (table.Drop)
            {
                await using var delete = Command($"DELETE FROM {Quote(table.Table)}");
                return await delete.ExecuteNonQueryAsync(ct);
            }

            var columns = table.Columns;
            var extra = columns.SelectMany(c => c.Selector?.Columns ?? []).Append(table.UserColumn).OfType<string>().Distinct().ToList();
            var userIndex = table.UserColumn == null ? -1 : extra.IndexOf(table.UserColumn);
            var select = $"SELECT rowid, {string.Join(", ", columns.Select(c => c.Column).Concat(extra).Select(Quote))} " +
                         $"FROM {Quote(table.Table)} WHERE rowid > $after ORDER BY rowid LIMIT {BatchSize}";
            await using var update = Command($"UPDATE {Quote(table.Table)} SET " +
                                             $"{string.Join(", ", columns.Select((c, i) => $"{Quote(c.Column)} = $v{i}"))} WHERE rowid = $rowid");
            var label = OwnerLabel(table.EntityName);
            var changedRows = 0;
            long after = 0;

            while (true)
            {
                var rows = await ReadBatch(select, after, columns.Count + extra.Count, ct);
                if (rows.Count == 0) break;

                foreach (var row in rows)
                {
                    var extraValues = row.Values[columns.Count..];
                    var userId = userIndex < 0 ? null : extraValues[userIndex];
                    var owner = new RowOwner($"{label}-{row.RowId}", userId == null ? null : $"user-{userId}");
                    var changed = false;
                    for (var i = 0; i < columns.Count; i++)
                    {
                        var jsonTypes = JsonTypes(columns[i], extraValues, extra);
                        var scrubbed = ScrubValue(columns[i].Kind, jsonTypes, row.Values[i], owner, table.Table, columns[i].Column);
                        if (scrubbed == row.Values[i]) continue;

                        row.Values[i] = scrubbed;
                        changed = true;
                    }

                    if (!changed) continue;

                    await Write(update, row, columns.Count, ct);
                    changedRows++;
                }

                after = rows[^1].RowId;
            }

            return changedRows;
        }

        /// <summary>
        /// Replaces the <c>Value</c> of each <see cref="ServerSetting"/> row whose key is in <see cref="ExportScrubCatalog.Plan.Settings"/>
        /// </summary>
        public async Task ScrubSettings(IModel model, CancellationToken ct)
        {
            var entityType = model.FindEntityType(typeof(ServerSetting))!;
            var table = Quote(entityType.GetTableName()!);
            var key = Quote(entityType.FindProperty(nameof(ServerSetting.Key))!.GetColumnName());
            var value = Quote(entityType.FindProperty(nameof(ServerSetting.Value))!.GetColumnName());
            await using var update = Command($"UPDATE {table} SET {value} = $v0 WHERE rowid = $rowid");

            foreach (var setting in plan.Settings)
            {
                await using var select = Command($"SELECT rowid, {value} FROM {table} WHERE {key} = $key");
                select.Parameters.AddWithValue("$key", (int) setting.Key);
                var rows = await Read(select, 1, ct);

                foreach (var row in rows)
                {
                    IReadOnlyList<Type> jsonTypes = setting.JsonType == null ? [] : [setting.JsonType];
                    var scrubbed = ScrubValue(setting.Kind, jsonTypes, row.Values[0], new RowOwner($"setting-{row.RowId}", null), "ServerSetting", setting.Key.ToString());
                    if (scrubbed == row.Values[0]) continue;

                    row.Values[0] = scrubbed;
                    await Write(update, row, 1, ct);
                }
            }
        }

        /// <summary>
        /// Runs every <see cref="ExportScrubKind.TextScrub"/> column through <see cref="ExportTextScrubber"/>. Called after every
        /// other table, since a username first seen in a later table must already be known when an earlier table's text is scrubbed
        /// </summary>
        public async Task ScrubText(CancellationToken ct)
        {
            _text = new ExportTextScrubber(junk, _usernames);
            foreach (var table in plan.Tables.Where(t => !t.Drop && t.Columns.Any(c => c.Kind == ExportScrubKind.TextScrub)))
            {
                await ScrubTable(table with { Columns = table.Columns.Where(c => c.Kind == ExportScrubKind.TextScrub).ToList() }, ct);
            }
        }

        /// <summary>
        /// A JSON value is parsed and scrubbed field by field, every other kind goes to <see cref="ExportJunk"/>.
        /// <see cref="ExportScrubKind.TextScrub"/> is left for <see cref="ScrubText"/>
        /// </summary>
        /// <param name="owner">The row the value came from and its user, used to name the replacement</param>
        /// <param name="jsonTypes">For a JSON value, the types it can hold: each one's marked fields are scrubbed</param>
        private string? ScrubValue(ExportScrubKind kind, IReadOnlyList<Type> jsonTypes, string? value, RowOwner owner, string table, string column)
        {
            if (string.IsNullOrEmpty(value)) return value;

            switch (kind)
            {
                case ExportScrubKind.TextScrub:
                    return _text?.Scrub(value) ?? value;
                case ExportScrubKind.Json:
                    try
                    {
                        var node = JsonNode.Parse(value);
                        // | not ||: every type must be scrubbed, even after an earlier one changed the node
#pragma warning disable S2178
                        var changed = jsonTypes.Aggregate(false, (scrubbed, type) => ScrubNode(node, type, owner) | scrubbed);
#pragma warning restore S2178
                        return changed ? node!.ToJsonString() : value;
                    }
                    catch (JsonException ex)
                    {
                        throw new InvalidOperationException($"{table}.{column} on {owner.Row} is not valid JSON, so it can't be scrubbed", ex);
                    }
                default:
                    return Replace(kind, value, owner);
            }
        }

        private string Replace(ExportScrubKind kind, string value, RowOwner owner)
        {
            var replaced = junk.Replace(kind, value, kind == ExportScrubKind.Username ? owner.User : owner.Row)!;
            if (kind == ExportScrubKind.Username && replaced != value)
            {
                _usernames.TryAdd(value, replaced);
            }

            return replaced;
        }

        /// <param name="type">The CLR type the node was serialized from: an object, a list of them, or a dictionary of them</param>
        private bool ScrubNode(JsonNode? node, Type type, RowOwner owner)
        {
            var objectType = ExportScrubCatalog.JsonObjectType(type);
            if (node == null || objectType == null) return false;

            // | not ||: every item must be scrubbed, even after an earlier one changed
            return node switch
            {
                JsonArray array => array.Aggregate(false, (changed, item) => ScrubNode(item, objectType, owner) | changed),
                JsonObject obj when (Nullable.GetUnderlyingType(type) ?? type) == objectType => ScrubObject(obj, objectType, owner),
                JsonObject dictionary => dictionary.Aggregate(false, (changed, pair) => ScrubNode(pair.Value, objectType, owner) | changed),
                _ => false,
            };
        }

        private bool ScrubObject(JsonObject obj, Type type, RowOwner owner)
        {
            var changed = false;
            foreach (var property in ExportScrubCatalog.JsonProperties(type))
            {
                var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
                if (!obj.TryGetPropertyValue(name, out var child) || child == null) continue;

                if (ExportScrubCatalog.JsonObjectType(property.PropertyType) != null)
                {
                    changed |= ScrubNode(child, property.PropertyType, owner);
                    continue;
                }

                if (plan.JsonFieldKind(type, property.Name) is not { } kind || kind == ExportScrubKind.TextScrub) continue;

                if (child is JsonArray array)
                {
                    for (var i = 0; i < array.Count; i++)
                    {
                        changed |= ReplaceLeaf(array[i], kind, owner, replaced => array[i] = replaced);
                    }
                }
                else
                {
                    changed |= ReplaceLeaf(child, kind, owner, replaced => obj[name] = replaced);
                }
            }

            return changed;
        }

        private bool ReplaceLeaf(JsonNode? leaf, ExportScrubKind kind, RowOwner owner, Action<string> set)
        {
            if (leaf is not JsonValue value || !value.TryGetValue<string>(out var text) || string.IsNullOrEmpty(text)) return false;

            var replaced = Replace(kind, text, owner);
            if (replaced == text) return false;

            set(replaced);
            return true;
        }

        private async Task<List<RowValues>> ReadBatch(string sql, long after, int columnCount, CancellationToken ct)
        {
            await using var select = Command(sql);
            select.Parameters.AddWithValue("$after", after);
            return await Read(select, columnCount, ct);
        }

        private static async Task<List<RowValues>> Read(SqliteCommand select, int columnCount, CancellationToken ct)
        {
            var rows = new List<RowValues>();
            await using var reader = await select.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var values = new string?[columnCount];
                for (var i = 0; i < columnCount; i++)
                {
                    values[i] = reader.IsDBNull(i + 1) ? null : reader.GetString(i + 1);
                }
                rows.Add(new RowValues(reader.GetInt64(0), values));
            }

            return rows;
        }

        /// <summary>
        /// For a row whose type depends on others, the types its selector picks from the row's discriminator values
        /// </summary>
        private static IReadOnlyList<Type> JsonTypes(ExportScrubCatalog.ColumnScrub column, string?[] discriminatorValues, List<string> discriminators)
        {
            if (column.Selector is not { } selector) return column.StoredTypes;

            return selector.Selector.TypesFor(selector.Columns.Select(c => discriminatorValues[discriminators.IndexOf(c)]).ToList());
        }

        /// <param name="columnCount">How many of the row's values are written back; the rest were read to choose a JSON type</param>
        private static async Task Write(SqliteCommand update, RowValues row, int columnCount, CancellationToken ct)
        {
            update.Parameters.Clear();
            update.Parameters.AddWithValue("$rowid", row.RowId);
            for (var i = 0; i < columnCount; i++)
            {
                update.Parameters.AddWithValue($"$v{i}", (object?) row.Values[i] ?? DBNull.Value);
            }

            await update.ExecuteNonQueryAsync(ct);
        }

        private SqliteCommand Command(string sql)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = transaction;
            return command;
        }

        private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

        /// <summary>
        /// Names a replaced email or username after its row: <c>user-3@scrubbed.invalid</c>, <c>device-7@scrubbed.invalid</c>
        /// </summary>
        private static string OwnerLabel(string entityName) => entityName == nameof(AppUser) ? "user" : entityName.ToLowerInvariant();

        private sealed record RowValues(long RowId, string?[] Values);

        /// <param name="Row">Names an email or a random value after its row: <c>device-7</c></param>
        /// <param name="User">Names a username after the row's user: <c>user-3</c>. Null when the row has none</param>
        private sealed record RowOwner(string Row, string? User);
    }

    private sealed record InjectedAdmin(string Password, string AuthKey);
}

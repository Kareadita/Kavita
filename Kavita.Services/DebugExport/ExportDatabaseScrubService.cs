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
    private const string PasswordCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
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
        foreach (var table in run.Plan.Tables)
        {
            var changed = await run.ScrubTable(table, ct);
            logger.LogDebug("Debug export scrubbed {Rows} rows in {Table}", changed, table.Table);
        }
        await run.ScrubSettings(context.Model, ct);

        // After the scrub or the admin's own hash and keys would be replaced
        var admin = await InjectAdmin(context, ct);

        await transaction.CommitAsync(ct);
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

        public ExportScrubCatalog.Plan Plan => plan;
        public ExportJunk Junk => junk;
        public IReadOnlyDictionary<string, string> Usernames => _usernames;

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
            var select = $"SELECT rowid, {string.Join(", ", columns.Select(c => Quote(c.Column)))} FROM {Quote(table.Table)} " +
                         $"WHERE rowid > $after ORDER BY rowid LIMIT {BatchSize}";
            await using var update = Command($"UPDATE {Quote(table.Table)} SET " +
                                             $"{string.Join(", ", columns.Select((c, i) => $"{Quote(c.Column)} = $v{i}"))} WHERE rowid = $rowid");
            var owner = OwnerLabel(table.EntityName);
            var changedRows = 0;
            long after = 0;

            while (true)
            {
                var rows = await ReadBatch(select, after, columns.Count, ct);
                if (rows.Count == 0) break;

                foreach (var row in rows)
                {
                    var changed = false;
                    for (var i = 0; i < columns.Count; i++)
                    {
                        var scrubbed = ScrubValue(columns[i].Kind, columns[i].JsonType, row.Values[i], $"{owner}-{row.RowId}", table.Table, columns[i].Column);
                        if (scrubbed == row.Values[i]) continue;

                        row.Values[i] = scrubbed;
                        changed = true;
                    }

                    if (!changed) continue;

                    await Write(update, row, ct);
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
                    var scrubbed = ScrubValue(setting.Kind, setting.JsonType, row.Values[0], $"setting-{row.RowId}", "ServerSetting", setting.Key.ToString());
                    if (scrubbed == row.Values[0]) continue;

                    row.Values[0] = scrubbed;
                    await Write(update, row, ct);
                }
            }
        }

        /// <summary>
        /// A JSON value is parsed and scrubbed field by field, every other kind goes to <see cref="ExportJunk"/>
        /// </summary>
        /// <param name="owner">The row the value came from, <c>user-3</c>, used to name its replacement</param>
        private string? ScrubValue(ExportScrubKind kind, Type? jsonType, string? value, string owner, string table, string column)
        {
            if (string.IsNullOrEmpty(value)) return value;

            switch (kind)
            {
                case ExportScrubKind.TextScrub:
                    return value;
                case ExportScrubKind.Json:
                    try
                    {
                        var node = JsonNode.Parse(value);
                        return ScrubNode(node, jsonType!, owner) ? node!.ToJsonString() : value;
                    }
                    catch (JsonException ex)
                    {
                        throw new InvalidOperationException($"{table}.{column} on {owner} is not valid JSON, so it can't be scrubbed", ex);
                    }
                default:
                    return Replace(kind, value, owner);
            }
        }

        private string Replace(ExportScrubKind kind, string value, string owner)
        {
            var replaced = junk.Replace(kind, value, owner)!;
            if (kind == ExportScrubKind.Username && replaced != value)
            {
                _usernames.TryAdd(value, replaced);
            }

            return replaced;
        }

        /// <param name="type">The CLR type the node was serialized from: an object, a list of them, or a dictionary of them</param>
        private bool ScrubNode(JsonNode? node, Type type, string owner)
        {
            var objectType = ExportScrubCatalog.JsonObjectType(type);
            if (node == null || objectType == null) return false;

            return node switch
            {
                JsonArray array => array.Aggregate(false, (changed, item) => ScrubNode(item, objectType, owner) | changed),
                JsonObject obj when (Nullable.GetUnderlyingType(type) ?? type) == objectType => ScrubObject(obj, objectType, owner),
                JsonObject dictionary => dictionary.Aggregate(false, (changed, pair) => ScrubNode(pair.Value, objectType, owner) | changed),
                _ => false,
            };
        }

        private bool ScrubObject(JsonObject obj, Type type, string owner)
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

        private bool ReplaceLeaf(JsonNode? leaf, ExportScrubKind kind, string owner, Action<string> set)
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

        private static async Task Write(SqliteCommand update, RowValues row, CancellationToken ct)
        {
            update.Parameters.Clear();
            update.Parameters.AddWithValue("$rowid", row.RowId);
            for (var i = 0; i < row.Values.Length; i++)
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
    }

    private sealed record InjectedAdmin(string Password, string AuthKey);
}

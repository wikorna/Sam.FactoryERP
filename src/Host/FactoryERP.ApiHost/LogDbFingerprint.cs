using Npgsql;

namespace FactoryERP.ApiHost;

/// <summary>
/// Startup diagnostic — logs exactly which PostgreSQL server, database and user
/// the host connected to, plus confirms the MassTransit Outbox/Inbox tables exist.
/// Call after builder.Build() and before host.RunAsync().
/// </summary>
internal static partial class DbFingerprint
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "DB Fingerprint => db={Db}, user={User}, addr={Addr}:{Port} | {Ver}")]
    private static partial void LogFingerprint(
        ILogger logger, object db, object user, object addr, object port, object ver);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "DB Tables => labeling.OutboxState={OutboxState}, labeling.InboxState={InboxState}, labeling.OutboxMessage={OutboxMsg}")]
    private static partial void LogTables(
        ILogger logger, object outboxState, object inboxState, object outboxMsg);

    public static async Task LogAsync(IServiceProvider sp, ILogger logger, CancellationToken ct = default)
    {
        var cfg = sp.GetRequiredService<IConfiguration>();
        var cs = cfg.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync(ct);

        const string sql = """
                           select
                             current_database() as db,
                             current_user       as usr,
                             inet_server_addr() as server_addr,
                             inet_server_port() as server_port,
                             version()          as ver;
                           """;

        await using (var cmd = new NpgsqlCommand(sql, conn))
        await using (var rdr = await cmd.ExecuteReaderAsync(ct))
        {
            await rdr.ReadAsync(ct);
            LogFingerprint(logger, rdr["db"], rdr["usr"], rdr["server_addr"], rdr["server_port"], rdr["ver"]);
        }

        await using var cmd2 = new NpgsqlCommand("""
                                                 select
                                                   cast(to_regclass('labeling."OutboxState"') as text)   as outbox_state,
                                                   cast(to_regclass('labeling."InboxState"') as text)    as inbox_state,
                                                   cast(to_regclass('labeling."OutboxMessage"') as text) as outbox_msg;
                                                 """, conn);
        await using var rdr2 = await cmd2.ExecuteReaderAsync(ct);
        await rdr2.ReadAsync(ct);

        var outboxState = rdr2.IsDBNull(0) ? "(missing)" : rdr2.GetString(0);
        var inboxState  = rdr2.IsDBNull(1) ? "(missing)" : rdr2.GetString(1);
        var outboxMsg   = rdr2.IsDBNull(2) ? "(missing)" : rdr2.GetString(2);

        LogTables(logger, outboxState, inboxState, outboxMsg);
    }
}

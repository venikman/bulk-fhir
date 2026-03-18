namespace BulkFhir.Storage

open System
open Npgsql

module Connection =

    let normalizeConnectionString (connString: string) =
        if String.IsNullOrWhiteSpace connString then connString
        elif connString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
             || connString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) then
            let uri = Uri(connString)
            let userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.None)
            let username = if userInfo.Length > 0 then Uri.UnescapeDataString(userInfo.[0]) else ""
            let password = if userInfo.Length > 1 then Uri.UnescapeDataString(userInfo.[1]) else ""
            let database = uri.AbsolutePath.TrimStart('/')

            let builder = NpgsqlConnectionStringBuilder()
            builder.Host <- uri.Host
            if uri.Port > 0 then builder.Port <- uri.Port
            if not (String.IsNullOrWhiteSpace database) then builder.Database <- database
            if not (String.IsNullOrWhiteSpace username) then builder.Username <- username
            if not (String.IsNullOrWhiteSpace password) then builder.Password <- password

            let query = uri.Query.TrimStart('?')
            if not (String.IsNullOrWhiteSpace query) then
                for pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries) do
                    let parts = pair.Split('=', 2, StringSplitOptions.None)
                    let key = Uri.UnescapeDataString(parts.[0]).Replace("-", "_")
                    let value =
                        if parts.Length > 1 then Uri.UnescapeDataString(parts.[1])
                        else ""

                    match key with
                    | "sslmode" -> builder.SslMode <- Enum.Parse<SslMode>(value, true)
                    | "channel_binding" -> builder.ChannelBinding <- Enum.Parse<ChannelBinding>(value, true)
                    | "pooling" -> builder.Pooling <- Boolean.Parse(value)
                    | "timeout" -> builder.Timeout <- Int32.Parse(value)
                    | "command_timeout" -> builder.CommandTimeout <- Int32.Parse(value)
                    | _ -> builder.[key] <- value

            builder.ConnectionString
        else
            connString

    let createConnection (connString: string) =
        new NpgsqlConnection(normalizeConnectionString connString)

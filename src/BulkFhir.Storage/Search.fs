namespace BulkFhir.Storage

open System

/// FHIR search parameter parsing.
/// Supports date prefixes (ge, le, gt, lt, eq), token (system|code), reference (Type/id).
module Search =

    type DatePrefix = Eq | Ge | Le | Gt | Lt

    type SearchParam =
        | StringParam of column: string * value: string
        | TokenParam of systemCol: string * codeCol: string * system: string option * code: string
        | DateParam of column: string * prefix: DatePrefix * value: DateTime
        | ReferenceParam of column: string * value: string
        | OrStringParam of columns: string list * value: string

    let parseDatePrefix (raw: string) : DatePrefix * string =
        if raw.Length >= 3 then
            match raw.[0..1] with
            | "ge" -> Ge, raw.[2..]
            | "le" -> Le, raw.[2..]
            | "gt" -> Gt, raw.[2..]
            | "lt" -> Lt, raw.[2..]
            | "eq" -> Eq, raw.[2..]
            | _    -> Eq, raw
        else
            Eq, raw

    let parseToken (raw: string) : string option * string =
        match raw.IndexOf('|') with
        | -1 -> None, raw
        | i  -> Some (raw.[0..i-1]), raw.[i+1..]

    let dateParamToSql (prefix: DatePrefix) (paramName: string) =
        match prefix with
        | Eq -> $"= @{paramName}"
        | Ge -> $">= @{paramName}"
        | Le -> $"<= @{paramName}"
        | Gt -> $"> @{paramName}"
        | Lt -> $"< @{paramName}"

    /// Build WHERE clause fragments + parameters from a list of SearchParams.
    let buildWhere (searchParams: SearchParam list) =
        let mutable paramIdx = 0
        let clauses = ResizeArray<string>()
        let parameters = ResizeArray<string * obj>()

        for sp in searchParams do
            paramIdx <- paramIdx + 1
            let pn = $"p{paramIdx}"

            match sp with
            | StringParam (col, value) ->
                clauses.Add($"lower({col}) LIKE @{pn}")
                parameters.Add(pn, $"%%{value.ToLowerInvariant()}%%" :> obj)

            | TokenParam (sysCol, codeCol, system, code) ->
                match system with
                | Some sys ->
                    clauses.Add($"{sysCol} = @{pn}s AND {codeCol} = @{pn}c")
                    parameters.Add($"{pn}s", sys :> obj)
                    parameters.Add($"{pn}c", code :> obj)
                | None ->
                    clauses.Add($"{codeCol} = @{pn}c")
                    parameters.Add($"{pn}c", code :> obj)

            | DateParam (col, prefix, value) ->
                clauses.Add($"{col} {dateParamToSql prefix pn}")
                parameters.Add(pn, value :> obj)

            | ReferenceParam (col, value) ->
                clauses.Add($"{col} = @{pn}")
                parameters.Add(pn, value :> obj)

            | OrStringParam (cols, value) ->
                let orClauses = cols |> List.mapi (fun i col -> $"lower({col}) LIKE @{pn}_{i}")
                clauses.Add("(" + String.Join(" OR ", orClauses) + ")")
                for i in 0 .. cols.Length - 1 do
                    parameters.Add($"{pn}_{i}", $"%%{value.ToLowerInvariant()}%%" :> obj)

        let whereClause =
            if clauses.Count = 0 then ""
            else " WHERE " + String.Join(" AND ", clauses)

        whereClause, parameters |> Seq.toList

namespace TruckingApi;

public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this WebApplication app, string connectionString)
    {
        // Flexible query endpoint — metric × groupBy
        app.MapGet("/api/analytics/query", async (string metric, string groupBy) =>
        {
            // Whitelist all values — never interpolate raw user input into SQL
            var validMetrics  = new[] { "revenue", "fuel", "load_count" };
            var validGroupBys = new[] { "driver", "route", "month", "status" };

            if (!validMetrics.Contains(metric) || !validGroupBys.Contains(groupBy))
                return Results.BadRequest(new { error = "Invalid metric or groupBy" });

            string labelExpr = groupBy switch
            {
                "driver" => "d.first_name || ' ' || d.last_name",
                "route"  => "l.origin || ' → ' || l.destination",
                "month"  => "strftime('%Y-%m', l.ship_date)",
                "status" => "l.status",
                _        => ""
            };

            string sql;

            if (metric == "fuel")
            {
                // Fuel spend — grouped differently depending on dimension
                if (groupBy == "status")
                    return Results.Content("""[]""", "application/json");

                var fuelLabel = groupBy switch
                {
                    "driver" => "d.first_name || ' ' || d.last_name",
                    "route"  => "l.origin || ' → ' || l.destination",
                    "month"  => "strftime('%Y-%m', da.advance_date)",
                    _        => ""
                };

                var fuelJoin  = groupBy == "driver"
                    ? "JOIN drivers d ON da.driver_id = d.driver_id"
                    : "JOIN drivers d ON da.driver_id = d.driver_id JOIN loads l ON da.load_id = l.load_id";

                var fuelWhere = groupBy == "driver"
                    ? "WHERE da.advance_type = 'Fuel'"
                    : "WHERE da.advance_type = 'Fuel' AND da.load_id IS NOT NULL";

                sql = $"""
                    SELECT {fuelLabel} AS label, ROUND(SUM(da.amount), 2) AS value
                    FROM driver_advances da
                    {fuelJoin}
                    {fuelWhere}
                    GROUP BY label
                    ORDER BY value DESC
                """;
            }
            else
            {
                var valueExpr = metric switch
                {
                    "revenue"    => "ROUND(SUM(l.line_haul_rate + l.fsc_rate), 2)",
                    "load_count" => "COUNT(DISTINCT l.load_id)",
                    _            => ""
                };

                if (groupBy == "status")
                {
                    sql = $"""
                        SELECT l.status AS label, {valueExpr} AS value
                        FROM loads l
                        GROUP BY label
                        ORDER BY value DESC
                    """;
                }
                else
                {
                    sql = $"""
                        SELECT {labelExpr} AS label, {valueExpr} AS value
                        FROM loads l
                        JOIN load_drivers ld ON l.load_id = ld.load_id
                        JOIN drivers d ON ld.driver_id = d.driver_id
                        GROUP BY label
                        ORDER BY value DESC
                        LIMIT 10
                    """;
                }
            }

            return Results.Content(await Database.ExecuteSql(connectionString, sql), "application/json");
        });
    }
}

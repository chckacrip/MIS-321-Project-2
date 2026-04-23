using Microsoft.AspNetCore.Mvc;

namespace TruckingApi.Controllers;

[ApiController]
[Route("api/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly string _conn;

    public AnalyticsController(DatabaseOptions db) => _conn = db.ConnectionString;

    [HttpGet("query")]
    public async Task<IActionResult> Query([FromQuery] string metric, [FromQuery] string groupBy)
    {
        // Whitelist all values — never interpolate raw user input into SQL
        var validMetrics  = new[] { "revenue", "fuel", "load_count" };
        var validGroupBys = new[] { "driver", "route", "month", "status" };

        if (!validMetrics.Contains(metric) || !validGroupBys.Contains(groupBy))
            return BadRequest(new { error = "Invalid metric or groupBy" });

        string labelExpr = groupBy switch
        {
            "driver" => "CONCAT(d.first_name, ' ', d.last_name)",
            "route"  => "CONCAT(l.origin, ' → ', l.destination)",
            "month"  => "DATE_FORMAT(l.ship_date, '%Y-%m')",
            "status" => "l.status",
            _        => ""
        };

        string groupByExpr = groupBy switch
        {
            "driver" => "d.driver_id",
            "route"  => "l.origin, l.destination",
            "month"  => "DATE_FORMAT(l.ship_date, '%Y-%m')",
            "status" => "l.status",
            _        => ""
        };

        string sql;

        if (metric == "fuel")
        {
            if (groupBy == "status")
                return Content("""[]""", "application/json");

            var fuelLabel = groupBy switch
            {
                "driver" => "CONCAT(d.first_name, ' ', d.last_name)",
                "route"  => "CONCAT(l.origin, ' → ', l.destination)",
                "month"  => "DATE_FORMAT(da.advance_date, '%Y-%m')",
                _        => ""
            };

            var fuelGroupBy = groupBy switch
            {
                "driver" => "d.driver_id",
                "route"  => "l.origin, l.destination",
                "month"  => "DATE_FORMAT(da.advance_date, '%Y-%m')",
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
                GROUP BY {fuelGroupBy}
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
                    GROUP BY l.status
                    ORDER BY value DESC
                """;
            }
            else
            {
                sql = $"""
                    SELECT {labelExpr} AS label, {valueExpr} AS value
                    FROM loads l
                    JOIN drivers d ON l.driver_id = d.driver_id
                    GROUP BY {groupByExpr}
                    ORDER BY value DESC
                    LIMIT 10
                """;
            }
        }

        return Content(await Database.ExecuteSql(_conn, sql), "application/json");
    }
}

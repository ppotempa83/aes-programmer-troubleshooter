using System.Net;
using System.Text;
using SuperiorAes.Core.Models;

namespace SuperiorAes.Core.Reporting;

public sealed record TroubleshootingReportContext(
    string SiteName,
    string AccountNumber,
    string Technician,
    AesModel SelectedModel,
    AesLocalStatus? Status,
    IReadOnlyList<RouteEntry> Routes,
    IReadOnlyList<DiagnosticFinding> Findings,
    AesMapCoverageAnalysis? Coverage = null);

public static class TroubleshootingReportGenerator
{
    public static string Generate(TroubleshootingReportContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Superior AES Troubleshooting Report</title>
              <style>
                :root { --navy:#10253d; --red:#c5202f; --ink:#1d2935; --muted:#657487; --line:#dce3e9; --bg:#f2f5f7; }
                * { box-sizing:border-box; }
                body { margin:0; color:var(--ink); background:var(--bg); font-family:Segoe UI,Arial,sans-serif; }
                main { width:min(1050px,calc(100% - 32px)); margin:30px auto; background:#fff; padding:40px; box-shadow:0 8px 30px #1d29351a; }
                header { border-bottom:4px solid var(--red); padding-bottom:18px; display:flex; justify-content:space-between; gap:24px; }
                h1,h2 { color:var(--navy); } h1 { margin:0; } h2 { margin-top:28px; border-bottom:1px solid var(--line); padding-bottom:7px; }
                .eyebrow { color:var(--red); text-transform:uppercase; font-size:12px; font-weight:700; letter-spacing:.12em; }
                .meta { color:var(--muted); text-align:right; line-height:1.55; }
                .grid { display:grid; grid-template-columns:repeat(4,1fr); gap:10px; }
                .card { border:1px solid var(--line); border-radius:8px; padding:12px; }
                .label { color:var(--muted); font-size:11px; text-transform:uppercase; }
                .value { color:var(--navy); font-size:19px; font-weight:700; margin-top:4px; }
                .path { overflow-x:auto; border:1px solid var(--line); border-radius:8px; background:#f8fafb; padding:12px; }
                table { width:100%; border-collapse:collapse; font-size:13px; }
                th,td { border-bottom:1px solid var(--line); padding:9px 8px; text-align:left; vertical-align:top; }
                th { color:var(--navy); background:#f7f9fa; }
                .Pass { color:#18794e; } .Warning { color:#9a6700; } .Critical { color:#b42318; } .Information { color:#175cd3; }
                .recommendations li { margin:8px 0; }
                footer { margin-top:30px; padding-top:14px; border-top:1px solid var(--line); color:var(--muted); font-size:12px; }
                @media print { body { background:#fff; } main { width:100%; margin:0; padding:18px; box-shadow:none; } }
              </style>
            </head>
            <body><main>
            """);

        builder.AppendLine("<header><div>");
        builder.AppendLine("<div class=\"eyebrow\">AES PROGRAMMER &amp; TROUBLESHOOTER</div>");
        builder.AppendLine("<h1>AES Troubleshooting Report</h1>");
        builder.AppendLine("</div><div class=\"meta\">");
        builder.AppendLine($"{Encode(context.SiteName)}<br>{Encode(context.AccountNumber)}<br>{Encode(context.Technician)}<br>{DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}");
        builder.AppendLine("</div></header>");

        builder.AppendLine("<h2>Subscriber evidence</h2><section class=\"grid\">");
        AddCard(builder, "Model", context.Status?.Model ?? ModelName(context.SelectedModel));
        AddCard(builder, "Subscriber ID", context.Status?.SubscriberId ?? "Not captured");
        AddCard(builder, "NETCON", context.Status?.NetCon.ToString() ?? "—");
        AddCard(builder, "STAT", context.Status?.StatCode ?? "—");
        AddCard(builder, "RT1", context.Status?.RouteOne ?? "—");
        AddCard(builder, "LEVEL", context.Status?.Level.ToString() ?? "—");
        AddCard(builder, "Route diversity", $"{context.Routes.Count} route(s)");
        AddCard(builder, "Enrollment", context.Status?.IsEnrolled == true ? "Enrolled" : "Not verified");
        builder.AppendLine("</section>");

        builder.AppendLine("<h2>Visualized RF paths</h2>");
        builder.AppendLine("<p>Each branch is an alternate first-hop route from the subscriber. L is link layer, N is the peer radio’s NETCON, and Q is local readability.</p>");
        builder.AppendLine("<div class=\"path\">");
        builder.AppendLine(BuildRouteSvg(context));
        builder.AppendLine("</div>");

        builder.AppendLine("<h2>Routing table and associated radio values</h2>");
        builder.AppendLine("<table><thead><tr><th>Preference</th><th>Associated radio</th><th>Link layer</th><th>Peer NETCON</th><th>Q</th><th>RF interpretation</th><th>Path assessment</th></tr></thead><tbody>");
        foreach (var route in context.Routes.OrderBy(route => route.Preference))
        {
            builder.AppendLine(
                $"<tr><td>{route.Preference}</td><td>{Encode(route.Id)}</td><td>L{route.LinkLayer:00}</td><td>N{route.PeerNetCon}</td><td>Q{Encode(route.Quality)}</td><td>{Encode(route.QualityLabel)}</td><td>{Encode(AssessRoute(route))}</td></tr>");
        }
        if (context.Routes.Count == 0)
        {
            builder.AppendLine("<tr><td colspan=\"7\">No routing-table entries were captured. Run the baseline diagnosis while connected, then export again.</td></tr>");
        }
        builder.AppendLine("</tbody></table>");

        builder.AppendLine("<h2>Diagnostic findings</h2>");
        builder.AppendLine("<table><thead><tr><th>Priority</th><th>Finding</th><th>Evidence</th><th>Recommended action</th></tr></thead><tbody>");
        foreach (var finding in context.Findings.OrderBy(FindingOrder))
        {
            builder.AppendLine(
                $"<tr><td class=\"{finding.Severity}\">{finding.Severity}</td><td>{Encode(finding.Title)}</td><td>{Encode(finding.Detail)}</td><td>{Encode(finding.RecommendedAction)}</td></tr>");
        }
        builder.AppendLine("</tbody></table>");

        builder.AppendLine("<h2>Prioritized technician recommendations</h2><ol class=\"recommendations\">");
        foreach (var recommendation in BuildRecommendations(context))
        {
            builder.AppendLine($"<li>{Encode(recommendation)}</li>");
        }
        builder.AppendLine("</ol>");

        if (context.Coverage is not null)
        {
            builder.AppendLine("<h2>Emergency24 map comparison</h2>");
            builder.AppendLine($"<p>{Encode(context.Coverage.RecommendationSummary)} Coordinates: {context.Coverage.Latitude:0.000000}, {context.Coverage.Longitude:0.000000}.</p>");
            builder.AppendLine("<table><thead><tr><th>Antenna layer</th><th>Gain</th><th>Expected NETCON</th><th>N5 peers</th><th>N6/7 peers</th><th>Route-related interpretation</th></tr></thead><tbody>");
            foreach (var layer in context.Coverage.Results)
            {
                var interpretation = layer.ExpectedNetCon switch
                {
                    null => "No mapped signal; verify coordinates and perform a measured survey.",
                    <= 5 => "Mapped usable candidate; compare with measured NETCON, Q, and route diversity.",
                    6 => "Mapped corrective-investigation area; distinguish local RF loss from upstream weakness.",
                    _ => "Mapped weak/unusable area; test alternate layers and physical antenna locations."
                };
                builder.AppendLine(
                    $"<tr><td>{Encode(layer.Antenna)}</td><td>{layer.GainDb:0.#} dB</td><td>{layer.ExpectedNetCon?.ToString() ?? "No signal"}</td><td>{layer.NetConFivePeers}</td><td>{layer.NetConSixOrSevenPeers}</td><td>{Encode(interpretation)}</td></tr>");
            }
            builder.AppendLine("</tbody></table>");

            var mapped = SelectMappedCoverage(context.Coverage);
            if (context.Status is not null && mapped is not null)
            {
                builder.AppendLine(
                    $"<p><strong>Measured-to-map comparison:</strong> subscriber NETCON {context.Status.NetCon}; " +
                    $"{Encode(mapped.Antenna)} layer " +
                    $"{(mapped.ExpectedNetCon.HasValue ? $"predicts NETCON {mapped.ExpectedNetCon}" : "contains no mapped signal at this point")}. " +
                    $"The captured routing table contains {context.Routes.Count} alternate first-hop route(s).</p>");
            }
        }

        builder.AppendLine("<footer>Troubleshooting evidence only. Confirm results on a known-good radio, complete physical RF/voltage checks, and verify required signals with the central station before field deployment or return to service.</footer>");
        builder.AppendLine("</main></body></html>");
        return builder.ToString();
    }

    private static string BuildRouteSvg(TroubleshootingReportContext context)
    {
        var routes = context.Routes.OrderBy(route => route.Preference).ToArray();
        var height = Math.Max(180, 70 + routes.Length * 88);
        var subscriberId = context.Status?.SubscriberId ?? "Subscriber";
        var svg = new StringBuilder(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" role=\"img\" aria-label=\"AES routing path diagram\" viewBox=\"0 0 900 {height}\" width=\"100%\" height=\"{height}\">");
        svg.AppendLine("<defs><marker id=\"arrow\" markerWidth=\"10\" markerHeight=\"7\" refX=\"9\" refY=\"3.5\" orient=\"auto\"><polygon points=\"0 0,10 3.5,0 7\" fill=\"#657487\"/></marker></defs>");
        AddSvgNode(svg, 25, height / 2 - 34, 205, 68, "#10253d", "#ffffff", $"Subscriber {subscriberId}", $"N{context.Status?.NetCon.ToString() ?? "—"} · STAT {context.Status?.StatCode ?? "—"}");
        AddSvgNode(svg, 705, height / 2 - 34, 170, 68, "#edf1f5", "#10253d", "AES mesh", "Alternate paths");

        if (routes.Length == 0)
        {
            svg.AppendLine($"<line x1=\"230\" y1=\"{height / 2}\" x2=\"705\" y2=\"{height / 2}\" stroke=\"#c5202f\" stroke-width=\"3\" stroke-dasharray=\"9 7\" marker-end=\"url(#arrow)\"/>");
            svg.AppendLine($"<text x=\"465\" y=\"{height / 2 - 12}\" text-anchor=\"middle\" font-family=\"Segoe UI,Arial\" font-size=\"15\" fill=\"#b42318\">No route data captured</text>");
        }
        else
        {
            for (var index = 0; index < routes.Length; index++)
            {
                var route = routes[index];
                var y = 22 + index * 88;
                var color = RouteColor(route);
                svg.AppendLine($"<path d=\"M230 {height / 2} C285 {height / 2},275 {y + 32},330 {y + 32}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"3\" marker-end=\"url(#arrow)\"/>");
                svg.AppendLine($"<line x1=\"575\" y1=\"{y + 32}\" x2=\"705\" y2=\"{height / 2}\" stroke=\"{color}\" stroke-width=\"3\" marker-end=\"url(#arrow)\"/>");
                AddSvgNode(svg, 330, y, 245, 64, "#ffffff", "#10253d", $"P{route.Preference} · Radio {route.Id}", $"L{route.LinkLayer:00} · N{route.PeerNetCon} · Q{route.Quality} · {route.QualityLabel}", color);
            }
        }

        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private static void AddSvgNode(
        StringBuilder builder,
        int x,
        int y,
        int width,
        int height,
        string fill,
        string textColor,
        string title,
        string subtitle,
        string stroke = "#10253d")
    {
        builder.AppendLine($"<rect x=\"{x}\" y=\"{y}\" width=\"{width}\" height=\"{height}\" rx=\"9\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"2\"/>");
        builder.AppendLine($"<text x=\"{x + width / 2}\" y=\"{y + 27}\" text-anchor=\"middle\" font-family=\"Segoe UI,Arial\" font-size=\"15\" font-weight=\"700\" fill=\"{textColor}\">{Encode(title)}</text>");
        builder.AppendLine($"<text x=\"{x + width / 2}\" y=\"{y + 48}\" text-anchor=\"middle\" font-family=\"Segoe UI,Arial\" font-size=\"12\" fill=\"{textColor}\">{Encode(subtitle)}</text>");
    }

    private static IReadOnlyList<string> BuildRecommendations(TroubleshootingReportContext context)
    {
        var recommendations = context.Findings
            .OrderBy(FindingOrder)
            .Select(finding => finding.RecommendedAction)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (context.Routes.Count < 2)
        {
            recommendations.Add("Improve route diversity before acceptance; reposition the antenna or radio and repeat the routing-table capture.");
        }
        if (context.Routes.Any(route => route.PeerNetCon >= 6))
        {
            recommendations.Add("Routes through N6/N7 peers indicate upstream mesh weakness; compare alternate first-hop radios before changing the local antenna.");
        }
        if (context.Routes.Any(route => route.Quality is "81" or "82" or "83"))
        {
            recommendations.Add("Weak-carrier Q values require a controlled antenna/location comparison and verification of coax, connectors, ground, supply voltage, and reflected power.");
        }
        recommendations.Add("After corrective work, rerun local status and the complete routing table, then confirm alarms, troubles, and restorals at the central station.");
        return recommendations.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string AssessRoute(RouteEntry route) =>
        route.PeerNetCon switch
        {
            >= 7 => "Critical upstream path",
            6 => "Upstream investigation required",
            _ when route.Quality is "81" or "82" or "83" => "Local RF weakness",
            _ when route.Quality is "01" => "Degraded local readability",
            _ => "Usable candidate path"
        };

    private static string RouteColor(RouteEntry route) =>
        route.PeerNetCon >= 7 || route.Quality == "81" ? "#b42318" :
        route.PeerNetCon == 6 || route.Quality is "82" or "83" or "01" ? "#b7791f" :
        "#18794e";

    private static int FindingOrder(DiagnosticFinding finding) =>
        finding.Severity switch
        {
            DiagnosticSeverity.Critical => 0,
            DiagnosticSeverity.Warning => 1,
            DiagnosticSeverity.Information => 2,
            _ => 3
        };

    private static AesMapCoverageResult? SelectMappedCoverage(
        AesMapCoverageAnalysis coverage) =>
        coverage.Recommended ??
        coverage.Results
            .Where(result => result.ExpectedNetCon.HasValue)
            .OrderBy(result => result.ExpectedNetCon)
            .ThenBy(result => result.GainDb)
            .FirstOrDefault() ??
        coverage.Results.LastOrDefault();

    private static string ModelName(AesModel model) => model == AesModel.Aes7744F ? "7744F" : "7788F";
    private static void AddCard(StringBuilder builder, string label, string value) =>
        builder.AppendLine($"<div class=\"card\"><div class=\"label\">{Encode(label)}</div><div class=\"value\">{Encode(value)}</div></div>");
    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}

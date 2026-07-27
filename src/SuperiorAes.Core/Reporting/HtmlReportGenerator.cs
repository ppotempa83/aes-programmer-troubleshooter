using System.Net;
using System.Text;
using SuperiorAes.Core.Models;

namespace SuperiorAes.Core.Reporting;

public static class HtmlReportGenerator
{
    public static string Generate(ReportContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Superior AES Field Report</title>
              <style>
                :root { --navy:#10253d; --red:#c5202f; --ink:#1d2935; --muted:#657487; --line:#dce3e9; --paper:#fff; --bg:#f2f5f7; }
                * { box-sizing:border-box; }
                body { margin:0; color:var(--ink); background:var(--bg); font-family:Segoe UI,Arial,sans-serif; }
                main { width:min(1050px,calc(100% - 32px)); margin:32px auto; background:var(--paper); padding:42px; box-shadow:0 8px 30px #1d29351a; }
                header { border-bottom:4px solid var(--red); padding-bottom:18px; display:flex; justify-content:space-between; gap:24px; }
                h1 { color:var(--navy); margin:0; font-size:30px; }
                h2 { color:var(--navy); margin-top:30px; padding-bottom:7px; border-bottom:1px solid var(--line); }
                .eyebrow { color:var(--red); text-transform:uppercase; font-size:12px; font-weight:700; letter-spacing:.12em; }
                .meta { color:var(--muted); line-height:1.6; text-align:right; }
                .grid { display:grid; grid-template-columns:repeat(4,1fr); gap:12px; }
                .card { border:1px solid var(--line); border-radius:8px; padding:14px; }
                .label { color:var(--muted); font-size:12px; text-transform:uppercase; }
                .value { margin-top:5px; font-size:20px; font-weight:700; color:var(--navy); }
                table { width:100%; border-collapse:collapse; font-size:13px; }
                th,td { border-bottom:1px solid var(--line); padding:9px 8px; text-align:left; vertical-align:top; }
                th { color:var(--navy); background:#f7f9fa; }
                .Pass { color:#18794e; } .Warning { color:#9a6700; } .Critical { color:#b42318; } .Information { color:#175cd3; }
                pre { white-space:pre-wrap; background:#101820; color:#d7f4e8; padding:18px; border-radius:8px; font:12px Consolas,monospace; max-height:480px; overflow:auto; }
                footer { margin-top:34px; padding-top:16px; border-top:1px solid var(--line); color:var(--muted); font-size:12px; }
                @media print { body { background:#fff; } main { width:100%; margin:0; padding:18px; box-shadow:none; } }
              </style>
            </head>
            <body><main>
            """);

        builder.AppendLine("<header><div>");
        builder.AppendLine("<div class=\"eyebrow\">Superior Fire &amp; Security</div>");
        builder.AppendLine("<h1>AES Subscriber Field Report</h1>");
        builder.AppendLine("</div><div class=\"meta\">");
        builder.AppendLine($"{Encode(context.SiteName)}<br>{Encode(context.AccountNumber)}<br>{Encode(context.Technician)}<br>{DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}");
        builder.AppendLine("</div></header>");

        builder.AppendLine("<h2>Subscriber summary</h2><section class=\"grid\">");
        AddCard(builder, "Selected model", context.SelectedModel == AesModel.Aes7744F ? "7744F" : "7788F");
        AddCard(builder, "Subscriber ID", context.Status?.SubscriberId ?? "Not captured");
        AddCard(builder, "NETCON", context.Status?.NetCon.ToString() ?? "—");
        AddCard(builder, "STAT", context.Status?.StatCode ?? "—");
        AddCard(builder, "RT1", context.Status?.RouteOne ?? "—");
        AddCard(builder, "LEVEL", context.Status?.Level.ToString() ?? "—");
        AddCard(builder, "Routes", context.Routes.Count.ToString());
        AddCard(builder, "Enrollment", context.Status?.IsEnrolled == true ? "Enrolled" : "Not verified");
        builder.AppendLine("</section>");

        builder.AppendLine("<h2>Diagnostic findings</h2>");
        builder.AppendLine("<table><thead><tr><th>Severity</th><th>Finding</th><th>Evidence</th><th>Action</th></tr></thead><tbody>");
        foreach (var finding in context.Findings)
        {
            builder.AppendLine($"<tr><td class=\"{finding.Severity}\">{finding.Severity}</td><td>{Encode(finding.Title)}</td><td>{Encode(finding.Detail)}</td><td>{Encode(finding.RecommendedAction)}</td></tr>");
        }
        builder.AppendLine("</tbody></table>");

        builder.AppendLine("<h2>Routing table</h2>");
        builder.AppendLine("<table><thead><tr><th>Pref.</th><th>ID</th><th>Layer</th><th>Peer N</th><th>Q</th><th>Interpretation</th></tr></thead><tbody>");
        foreach (var route in context.Routes)
        {
            builder.AppendLine($"<tr><td>{route.Preference}</td><td>{Encode(route.Id)}</td><td>{route.LinkLayer}</td><td>{route.PeerNetCon}</td><td>{Encode(route.Quality)}</td><td>{Encode(route.QualityLabel)}</td></tr>");
        }
        builder.AppendLine("</tbody></table>");

        builder.AppendLine("<h2>Zone status</h2>");
        builder.AppendLine("<table><thead><tr><th>Zone</th><th>Raw</th><th>Status</th></tr></thead><tbody>");
        foreach (var zone in context.Zones)
        {
            builder.AppendLine($"<tr><td>{zone.Zone}</td><td>{Encode(zone.State.ToString())}</td><td>{Encode(zone.Label)}</td></tr>");
        }
        builder.AppendLine("</tbody></table>");

        if (context.Coverage is not null)
        {
            builder.AppendLine("<h2>New radio planning evidence</h2>");
            if (context.RadioRecommendation is not null)
            {
                builder.AppendLine("<section class=\"grid\">");
                AddCard(builder, "Recommended antenna", context.RadioRecommendation.Antenna);
                AddCard(builder, "Recommended location", context.RadioRecommendation.Location);
                AddCard(builder, "Latitude", context.Coverage.Latitude.ToString("0.000000"));
                AddCard(builder, "Longitude", context.Coverage.Longitude.ToString("0.000000"));
                builder.AppendLine("</section>");
                builder.AppendLine($"<p>{Encode(context.RadioRecommendation.Rationale)}</p>");
                builder.AppendLine($"<p><strong>Limitations:</strong> {Encode(context.RadioRecommendation.Limitations)}</p>");
            }

            if (context.Building is not null)
            {
                builder.AppendLine(
                    $"<p><strong>Geoapify address/elevation evidence:</strong> {Encode(context.Building.FormattedAddress)}; ground {FormatMeters(context.Building.GroundElevationMeters)}; provider detail {Encode(context.Building.ImageryQuality)}.</p>");
            }

            builder.AppendLine("<table><thead><tr><th>Antenna layer</th><th>Gain</th><th>Expected NETCON</th><th>N5 peers</th><th>N6/7 peers</th></tr></thead><tbody>");
            foreach (var layer in context.Coverage.Results)
            {
                builder.AppendLine(
                    $"<tr><td>{Encode(layer.Antenna)}</td><td>{layer.GainDb:0.#} dB</td><td>{layer.ExpectedNetCon?.ToString() ?? "No signal"}</td><td>{layer.NetConFivePeers}</td><td>{layer.NetConSixOrSevenPeers}</td></tr>");
            }
            builder.AppendLine("</tbody></table>");
        }

        builder.AppendLine("<h2>Site survey trials</h2>");
        builder.AppendLine("<table><thead><tr><th>Location</th><th>Antenna / cable</th><th>NETCON / Q</th><th>Routes / ACK</th><th>Power</th><th>Voltage</th><th>Notes</th></tr></thead><tbody>");
        foreach (var trial in context.SurveyTrials)
        {
            builder.AppendLine(
                $"<tr><td>{Encode(trial.Location)}</td><td>{Encode(trial.Antenna)}<br>{Encode(trial.Cable)}</td><td>{trial.NetCon} / Q{Encode(trial.BestQuality)}</td><td>{trial.RouteCount} / {trial.AckSuccessPercent:0.#}%</td><td>{trial.ForwardPowerWatts:0.00} W fwd<br>{trial.ReflectedPowerWatts:0.00} W refl ({trial.ReflectedPowerPercent:0.#}%)</td><td>{trial.DcVoltageIdle:0.00} → {trial.DcVoltageKeyed:0.00} V</td><td>{Encode(trial.Notes)}</td></tr>");
        }
        builder.AppendLine("</tbody></table>");

        builder.AppendLine("<h2>Session transcript</h2>");
        builder.AppendLine($"<pre>{Encode(context.Transcript)}</pre>");
        builder.AppendLine("<footer>This report supports technician diagnosis; it does not replace AES instructions, listing requirements, the authority having jurisdiction, or end-to-end central-station testing.</footer>");
        builder.AppendLine("</main></body></html>");
        return builder.ToString();
    }

    private static void AddCard(StringBuilder builder, string label, string value) =>
        builder.AppendLine($"<div class=\"card\"><div class=\"label\">{Encode(label)}</div><div class=\"value\">{Encode(value)}</div></div>");

    private static string FormatMeters(double? value) =>
        value.HasValue ? $"{value.Value:0.#} m" : "not available";

    private static string FormatSquareMeters(double? value) =>
        value.HasValue ? $"{value.Value:0.#} m²" : "not available";

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}

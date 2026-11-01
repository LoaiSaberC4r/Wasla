using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Wasla.Application.Features.Reporting;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

internal static class PdfPrintChartRenderer
{
    private static readonly XNamespace Ns = "http://www.w3.org/2000/svg";
    // Every element is constructed as XML. No source markup, URLs, styles or event handlers are accepted.
    public static string? Render(PdfPrintSection s, bool ar)
    {
        var rows = s.Points;
        if (s.Table || rows.Count == 0 || rows.Count > 800 || rows.Any(p => !PdfPrintPresentation.Measured(p.Metric)) ||
            rows.Select(p => p.Metric.Unit).Distinct().Count() != 1 || rows.Any(p => p.Metric.Unit == "percent" && p.Metric.Value is < 0 or > 100) ||
            rows.Any(p => p.Metric.Unit == "count" && p.Metric.Value < 0)) return null;
        var temporal = s.Temporal;
        var keys = rows.Select(p => p.Group).Distinct().ToArray();
        var codes = rows.Select(p => p.Metric.MetricCode).Distinct().ToArray();
        // Missing observations must not become zeroes or connected trend segments.
        if (temporal && (keys.Length < 2 || codes.Length > 3 || rows.Count != keys.Length * codes.Length ||
            keys.Any(k => !DateKey(k)) || keys.Any(k => codes.Any(c => rows.Count(p => p.Group == k && p.Metric.MetricCode == c) != 1)))) return null;
        var ranked = s.Source is "Governorate:" or "City:" or "Specialization:";
        if (!temporal && rows.Count > 10 && !ranked) return null;
        if (ranked) rows = rows.OrderByDescending(p => p.Metric.Value).Take(10).ToArray();
        var min = Math.Min(0m, rows.Min(p => p.Metric.Value!.Value));
        var max = Math.Max(0m, rows.Max(p => p.Metric.Value!.Value));
        // Decimal values stay authoritative; floating point is used only for bounded drawing coordinates.
        var span = (double)max - (double)min; if (span == 0) span = 1;
        double Scale(decimal value) => ((double)value - (double)min) / span;
        var height = temporal ? 290 : 65 + rows.Count * 34;
        var svg = new XElement(Ns + "svg", new XAttribute("viewBox", $"0 0 760 {height}"), new XAttribute("width", 760), new XAttribute("height", height));
        svg.Add(new XElement(Ns + "title", Short(s.Title)), new XElement(Ns + "rect", new XAttribute("width", 760), new XAttribute("height", height), new XAttribute("fill", "#ffffff")));
        void Text(double x, double y, string text, string anchor = "start", int size = 12) => svg.Add(new XElement(Ns + "text",
            new XAttribute("x", F(x)), new XAttribute("y", F(y)), new XAttribute("text-anchor", anchor), new XAttribute("font-family", "Tahoma,Arial,sans-serif"),
            new XAttribute("font-size", size), new XAttribute("fill", "#143c50"), Short(text)));
        void Line(double x1, double y1, double x2, double y2, string color) => svg.Add(new XElement(Ns + "line", new XAttribute("x1", F(x1)), new XAttribute("y1", F(y1)),
            new XAttribute("x2", F(x2)), new XAttribute("y2", F(y2)), new XAttribute("stroke", color), new XAttribute("stroke-width", 1)));
        Text(12, 17, ReportPresentationText.Unit(rows[0].Metric.Unit, ar));
        if (temporal)
        {
            double Y(decimal value) => 205 - Scale(value) * 145;
            double X(int index) => 65 + index * 665.0 / (keys.Length - 1);
            for (var tick = 0; tick <= 4; tick++)
            {
                var y = 205 - tick * 145.0 / 4; Line(65, y, 730, y, "#dce5e9");
                Text(58, y + 4, ((double)min + span * tick / 4).ToString("0.##", CultureInfo.InvariantCulture), "end", 11);
            }
            string[] colors = ["#143c50", "#087d82", "#666666"];
            for (var i = 0; i < codes.Length; i++)
            {
                var values = keys.Select(k => rows.Single(p => p.Group == k && p.Metric.MetricCode == codes[i])).ToArray();
                var polyline = new XElement(Ns + "polyline", new XAttribute("fill", "none"), new XAttribute("stroke", colors[i]), new XAttribute("stroke-width", 2.5),
                    new XAttribute("stroke-dasharray", i == 0 ? "none" : i == 1 ? "7 4" : "2 4"),
                    new XAttribute("points", string.Join(" ", values.Select((p, j) => F(X(j)) + "," + F(Y(p.Metric.Value!.Value))))));
                svg.Add(polyline); Text(65 + i * 225, 37, (i + 1) + ". " + PdfPrintPresentation.Name(values[0].Metric, ar), size: 11);
                // Numbered endpoint labels preserve series identity in monochrome print.
                Text(735, Y(values[^1].Metric.Value!.Value) + 4, (i + 1).ToString(CultureInfo.InvariantCulture), size: 10);
            }
            for (var i = 0; i < keys.Length; i++)
                if (i == 0 || i == keys.Length - 1 || i % Math.Max(1, (keys.Length - 1) / 4) == 0)
                    Text(X(i), 228, keys[i], i == 0 ? "start" : i == keys.Length - 1 ? "end" : "middle", 10);
            Text(65, 258, ar ? "الفترة · حسب تجميع المصدر" : "Period · source grouping");
        }
        else
        {
            var zero = 310 + Scale(0) * 350; Line(zero, 31, zero, height - 20, "#64748b");
            Text(310, 32, PdfPrintPresentation.Number(min), size: 10); Text(660, 32, PdfPrintPresentation.Number(max), "end", 10);
            for (var i = 0; i < rows.Count; i++)
            {
                var p = rows[i]; var y = 43 + i * 34; var x = 310 + Scale(p.Metric.Value!.Value) * 350;
                Text(298, y + 14, (p.Group.Length == 0 ? "" : p.Group + " · ") + PdfPrintPresentation.Name(p.Metric, ar), "end");
                svg.Add(new XElement(Ns + "rect", new XAttribute("x", F(Math.Min(zero, x))), new XAttribute("y", y), new XAttribute("width", F(Math.Abs(x - zero))),
                    new XAttribute("height", 19), new XAttribute("fill", "#087d82"), new XAttribute("stroke", "#143c50")));
                Text(675, y + 14, PdfPrintPresentation.Value(p.Metric));
            }
            if (s.Points.Count > rows.Count) Text(12, height - 5, ar ? "أعلى ١٠ قيم؛ المصدر الكامل في الملحق" : "Top 10 values; complete source in appendix", size: 10);
        }
        return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg.ToString(SaveOptions.DisableFormatting)));
    }
    private static bool DateKey(string key) => DateOnly.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
        DateTime.TryParseExact(key, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
        (key.Length == 8 && key[4..6] == "-W" && int.TryParse(key[..4], out var year) && year is >= 1 and <= 9999 && int.TryParse(key[6..], out var week) && week is >= 1 and <= 53);
    private static string F(double n) => n.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Short(string text) => text.Length <= 42 ? text : text[..39] + "…";
}

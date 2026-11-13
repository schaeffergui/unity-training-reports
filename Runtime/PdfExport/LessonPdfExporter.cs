using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using TrainingReports;
using UnityEngine;

namespace TrainingReports.PdfExport
{
    public sealed class LessonPdfExporter : MonoBehaviour
    {
        private static readonly BaseColor Ink = new BaseColor(33, 41, 48);
        private static readonly BaseColor Muted = new BaseColor(100, 110, 120);
        private static readonly BaseColor Border = new BaseColor(205, 212, 220);
        private static readonly BaseColor Green = new BaseColor(54, 125, 76);
        private static readonly BaseColor Blue = new BaseColor(41, 128, 185);
        private static readonly BaseColor Yellow = new BaseColor(220, 165, 30);
        private static readonly BaseColor Red = new BaseColor(192, 57, 55);
        private static readonly BaseColor Light = new BaseColor(245, 247, 249);
        [SerializeField] private string outputDirectory;
        public static event Action<string> PdfGenerated;

        public string GeneratePdf(ITrainingContext context, TrainingReportInput report, IRoutePainter routePainter = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (report == null) throw new ArgumentNullException(nameof(report));
            report.Operation = context.Operation;
            report.Crop = context.Crop;
            report.FarmName = context.FarmName;
            report.SceneName = context.SceneName;
            report.StudentName = context.StudentName;
            report.InstructorName = context.InstructorName;
            report.VehicleModel = context.VehicleModel;
            TrainingRouteMap route = null;
            if (routePainter != null) routePainter.TryGetRouteMap(out route);
            return GeneratePdf(report, route);
        }

        public string GeneratePdf(TrainingReportInput report, TrainingRouteMap routeMap = null)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            string directory = string.IsNullOrWhiteSpace(outputDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Training Reports")
                : outputDirectory;
            Directory.CreateDirectory(directory);
            string stamp = string.IsNullOrWhiteSpace(report.CompletedAt)
                ? DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm", CultureInfo.InvariantCulture)
                : report.CompletedAt;
            string name = SafeFileName(string.IsNullOrWhiteSpace(report.StudentName) ? "Student" : report.StudentName);
            string path = Path.Combine(directory, name + "_Training_Report_" + SafeFileName(stamp) + ".pdf");
            TrainingReportTelemetryStats stats = TrainingReportInsights.CalculateTelemetry(report.Telemetry);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var document = new Document(PageSize.A4, 42f, 42f, 48f, 44f))
            {
                PdfWriter writer = PdfWriter.GetInstance(document, stream);
                writer.PageEvent = new PageFooter();
                document.Open();
                AddSummaryPage(document, report, stats);
                document.NewPage();
                AddTelemetryPage(writer, document, report, stats);
                document.NewPage();
                AddRoutePage(writer, document, report, stats, routeMap);
                document.NewPage();
                AddSignaturePage(document);
                document.Close();
            }
            PdfGenerated?.Invoke(path);
            return path;
        }

        private static void AddSummaryPage(Document document, TrainingReportInput report, TrainingReportTelemetryStats stats)
        {
            AddPageHeading(document, "TRAINING PERFORMANCE REPORT", "Lesson summary and observations");
            var identity = new PdfPTable(2) { WidthPercentage = 100f, SpacingAfter = 12f };
            identity.SetWidths(new[] { 1f, 1f });
            AddInfo(identity, "Student", report.StudentName); AddInfo(identity, "Instructor", report.InstructorName);
            AddInfo(identity, "Training", OperationName(report.Operation)); AddInfo(identity, "Crop", report.Crop);
            AddInfo(identity, "Vehicle model", report.VehicleModel); AddInfo(identity, "Farm / scene", FirstNonEmpty(report.FarmName, report.SceneName));
            AddInfo(identity, "Date", report.CompletedAt); AddInfo(identity, "Duration", report.Duration);
            document.Add(identity);

            int completed = CountTasks(report, true);
            int errors = CountTasks(report, false);
            int attempts = completed + errors;
            var cards = new PdfPTable(3) { WidthPercentage = 100f, SpacingAfter = 12f };
            AddMetric(cards, "COMPLETED", completed.ToString(CultureInfo.InvariantCulture), Green);
            AddMetric(cards, "ATTENTION", errors.ToString(CultureInfo.InvariantCulture), Red);
            AddMetric(cards, "SCORE", report.ScorePercent.ToString("0", CultureInfo.InvariantCulture) + "%", Blue);
            document.Add(cards);

            AddSectionHeading(document, "Lesson summary");
            AddLine(document, "Completed tasks", completed + " of " + attempts, false);
            AddLine(document, "Rework / inefficiency", FormatPercent(report.ReworkPercent), false);
            AddLine(document, "Out-of-area operation", FormatPercent(report.OutOfAreaPercent), false);
            AddLine(document, "Score penalty", FormatPercent(report.ScorePenaltyPercent), false);
            if (report.Operation == TrainingOperation.Planting)
                AddLine(document, "Estimated waste", report.WastedSeeds.ToString("0.0", CultureInfo.InvariantCulture) + " seed units; " + report.WastedFuelLiters.ToString("0.00", CultureInfo.InvariantCulture) + " L fuel", false);
            if (report.Operation == TrainingOperation.Spraying)
                AddLine(document, "Estimated waste", report.WastedSprayLiters.ToString("0.00", CultureInfo.InvariantCulture) + " L spray mix; " + report.WastedFuelLiters.ToString("0.00", CultureInfo.InvariantCulture) + " L fuel", false);
            if (IsHarvest(report.Operation))
            {
                AddLine(document, "Grains in truck", stats.GrainsInTruck.ToString("0", CultureInfo.InvariantCulture), false);
                AddLine(document, "Grain loss", stats.GrainLoss.ToString("0", CultureInfo.InvariantCulture), false);
            }

            AddSectionHeading(document, "Tasks completed successfully");
            bool any = AddTaskList(document, report, true);
            if (!any) AddLine(document, "No task was recorded as completed.", null, false);
            AddSectionHeading(document, "Attention points");
            any = AddTaskList(document, report, false);
            if (!any) AddLine(document, "No errors were recorded during the lesson.", null, false);
            AddSectionHeading(document, "Observations and advice");
            foreach (string observation in TrainingReportInsights.BuildObservations(report)) AddBullet(document, observation, false);
        }

        private static void AddTelemetryPage(PdfWriter writer, Document document, TrainingReportInput report, TrainingReportTelemetryStats stats)
        {
            AddPageHeading(document, "TELEMETRY ANALYSIS", "Speed, engine and control signals over time");
            if (stats.SampleCount == 0)
            {
                AddLine(document, "No telemetry samples were supplied for this lesson.", null, false);
                return;
            }
            AddOperationStats(document, report.Operation, report, stats);
            float top = document.PageSize.Height - document.TopMargin - 98f;
            DrawDualChart(writer.DirectContent, document, report.Telemetry, "SPEED AND RPM",
                "Speed (km/h)", "RPM", p => p.Speed, p => p.Rpm, Blue, Green, stats.MaximumSpeed, stats.MaximumRpm, top - 25f, 165f);
            DrawDualChart(writer.DirectContent, document, report.Telemetry, "THROTTLE AND BRAKE",
                "Throttle (%)", "Brake (%)", p => p.Throttle * 100f, p => p.Brake * 100f, Yellow, Red, 100f, 100f, top - 235f, 165f);
        }

        private static void DrawDualChart(PdfContentByte cb, Document document, IList<TrainingTelemetryPoint> points, string title,
            string leftLabel, string rightLabel, Func<TrainingTelemetryPoint, float> leftValue, Func<TrainingTelemetryPoint, float> rightValue,
            BaseColor leftColor, BaseColor rightColor, float leftMaximum, float rightMaximum, float top, float height)
        {
            float left = document.LeftMargin + 34f;
            float width = document.PageSize.Width - document.LeftMargin - document.RightMargin - 68f;
            float bottom = top - height;
            float plotHeight = height - 43f;
            cb.BeginText(); cb.SetFontAndSize(BaseFont.CreateFont(), 9f); cb.SetColorFill(Ink); cb.SetTextMatrix(left, top - 12f); cb.ShowText(title); cb.EndText();
            cb.SetLineWidth(0.4f); cb.SetColorStroke(Border);
            for (int i = 0; i <= 4; i++)
            {
                float y = bottom + plotHeight * i / 4f; cb.MoveTo(left, y); cb.LineTo(left + width, y); cb.Stroke();
            }
            DrawChartAxisLabels(cb, left, bottom, width, plotHeight, leftLabel, rightLabel, leftMaximum, rightMaximum, leftColor, rightColor);
            if (points.Count >= 2)
            {
                int stride = Math.Max(1, points.Count / 160);
                float startTime = points[0].TimeSeconds;
                float endTime = points[points.Count - 1].TimeSeconds;
                float range = Math.Max(0.001f, endTime - startTime);
                DrawSeries(cb, points, stride, left, bottom, width, plotHeight, startTime, range, leftMaximum, leftValue, leftColor);
                DrawSeries(cb, points, stride, left, bottom, width, plotHeight, startTime, range, rightMaximum, rightValue, rightColor);
                DrawTimeAxis(cb, left, bottom - 15f, width, startTime, endTime);
            }
            else
            {
                cb.BeginText(); cb.SetFontAndSize(BaseFont.CreateFont(), 8f); cb.SetColorFill(Muted); cb.SetTextMatrix(left + 8f, bottom + plotHeight * 0.5f); cb.ShowText("At least two samples are required to draw a chart."); cb.EndText();
            }
            DrawLegend(cb, left, bottom - 30f, leftLabel, leftColor, rightLabel, rightColor);
        }

        private static void DrawSeries(PdfContentByte cb, IList<TrainingTelemetryPoint> points, int stride, float left, float bottom, float width, float height, float startTime, float timeRange, float maximum, Func<TrainingTelemetryPoint, float> selector, BaseColor color)
        {
            maximum = Math.Max(1f, maximum); cb.SetColorStroke(color); cb.SetLineWidth(1.5f);
            int previous = -1;
            for (int i = 0; i < points.Count; i += stride)
            {
                TrainingTelemetryPoint point = points[i];
                if (previous >= 0)
                {
                    TrainingTelemetryPoint prior = points[previous];
                    float x1 = left + width * Clamp01((prior.TimeSeconds - startTime) / timeRange);
                    float x2 = left + width * Clamp01((point.TimeSeconds - startTime) / timeRange);
                    float y1 = bottom + height * Clamp01(selector(prior) / maximum);
                    float y2 = bottom + height * Clamp01(selector(point) / maximum);
                    cb.MoveTo(x1, y1); cb.LineTo(x2, y2); cb.Stroke();
                }
                previous = i;
            }
            if (previous != points.Count - 1 && previous >= 0)
            {
                TrainingTelemetryPoint prior = points[previous], point = points[points.Count - 1];
                cb.MoveTo(left + width * Clamp01((prior.TimeSeconds - startTime) / timeRange), bottom + height * Clamp01(selector(prior) / maximum));
                cb.LineTo(left + width, bottom + height * Clamp01(selector(point) / maximum)); cb.Stroke();
            }
        }

        private static void DrawChartAxisLabels(PdfContentByte cb, float left, float bottom, float width, float height,
            string leftLabel, string rightLabel, float leftMaximum, float rightMaximum, BaseColor leftColor, BaseColor rightColor)
        {
            cb.BeginText();
            cb.SetFontAndSize(BaseFont.CreateFont(), 6.5f);
            cb.SetColorFill(leftColor);
            cb.SetTextMatrix(left - 29f, bottom + height - 2f);
            cb.ShowText(Math.Max(1f, leftMaximum).ToString("0.#", CultureInfo.InvariantCulture));
            cb.SetTextMatrix(left - 29f, bottom);
            cb.ShowText("0");
            cb.SetColorFill(rightColor);
            cb.SetTextMatrix(left + width + 4f, bottom + height - 2f);
            cb.ShowText(Math.Max(1f, rightMaximum).ToString("0.#", CultureInfo.InvariantCulture));
            cb.SetTextMatrix(left + width + 4f, bottom);
            cb.ShowText("0");
            cb.EndText();
        }

        private static void DrawTimeAxis(PdfContentByte cb, float left, float y, float width, float start, float end)
        {
            cb.BeginText(); cb.SetFontAndSize(BaseFont.CreateFont(), 7f); cb.SetColorFill(Muted);
            for (int i = 0; i <= 4; i++)
            {
                float t = start + (end - start) * i / 4f;
                cb.SetTextMatrix(left + width * i / 4f - 8f, y); cb.ShowText(FormatTime(t));
            }
            cb.EndText();
        }

        private static void DrawLegend(PdfContentByte cb, float x, float y, string first, BaseColor firstColor, string second, BaseColor secondColor)
        {
            cb.SetColorFill(firstColor); cb.Circle(x + 4f, y + 3f, 3f); cb.Fill();
            cb.SetColorFill(secondColor); cb.Circle(x + 130f, y + 3f, 3f); cb.Fill();
            cb.BeginText(); cb.SetFontAndSize(BaseFont.CreateFont(), 7f); cb.SetColorFill(Ink); cb.SetTextMatrix(x + 11f, y); cb.ShowText(first); cb.SetTextMatrix(x + 137f, y); cb.ShowText(second); cb.EndText();
        }

        private static void AddRoutePage(PdfWriter writer, Document document, TrainingReportInput report, TrainingReportTelemetryStats stats, TrainingRouteMap route)
        {
            AddPageHeading(document, "OPERATION ROUTE", "Route and operation events");
            var facts = new PdfPTable(3) { WidthPercentage = 100f, SpacingAfter = 14f };
            AddStat(facts, "Map", route == null ? report.SceneName : FirstNonEmpty(route.MapName, report.SceneName));
            AddStat(facts, "Route distance", RouteDistance(route).ToString("0.0", CultureInfo.InvariantCulture) + " m");
            AddStat(facts, "Average speed", stats.AverageSpeed.ToString("0.0", CultureInfo.InvariantCulture) + " km/h");
            document.Add(facts);
            if (route == null || route.ImagePng == null || route.ImagePng.Length == 0)
            {
                AddLine(document, "No route image was provided by the route painter.", null, false);
                DrawRouteLegend(writer.DirectContent, document, report.Operation);
                return;
            }
            iTextImage image = iTextImage.GetInstance(route.ImagePng);
            float maxWidth = document.PageSize.Width - document.LeftMargin - document.RightMargin;
            float maxHeight = document.PageSize.Height - document.TopMargin - document.BottomMargin - 170f;
            image.ScaleToFit(maxWidth, maxHeight);
            float x = (document.PageSize.Width - image.ScaledWidth) * 0.5f;
            float y = document.PageSize.Height - document.TopMargin - 20f - image.ScaledHeight;
            image.SetAbsolutePosition(x, y); document.Add(image);
            PdfContentByte cb = writer.DirectContent;
            cb.SetColorStroke(Green); cb.SetLineWidth(2f);
            for (int i = 0; route.Path != null && i + 1 < route.Path.Count; i++)
            {
                TrainingRoutePoint a = route.Path[i], b = route.Path[i + 1];
                cb.MoveTo(x + a.X * image.ScaledWidth, y + a.Y * image.ScaledHeight);
                cb.LineTo(x + b.X * image.ScaledWidth, y + b.Y * image.ScaledHeight); cb.Stroke();
            }
            if (route.Path != null && route.Path.Count > 0)
            {
                DrawMapDot(cb, x + route.Path[0].X * image.ScaledWidth, y + route.Path[0].Y * image.ScaledHeight, Green, 6f);
                TrainingRoutePoint last = route.Path[route.Path.Count - 1];
                DrawMapDot(cb, x + last.X * image.ScaledWidth, y + last.Y * image.ScaledHeight, Red, 6f);
            }
            if (route.Markers != null)
                foreach (TrainingRouteMarker marker in route.Markers) DrawMapDot(cb, x + marker.Position.X * image.ScaledWidth, y + marker.Position.Y * image.ScaledHeight, MarkerColor(marker.Type), 3.5f);
            DrawRouteLegend(cb, document, report.Operation);
        }

        private static void DrawRouteLegend(PdfContentByte cb, Document document, TrainingOperation operation)
        {
            float x = document.LeftMargin;
            float y = document.BottomMargin + 16f;
            var entries = new List<KeyValuePair<string, BaseColor>>
            {
                new KeyValuePair<string, BaseColor>("Start", Green),
                new KeyValuePair<string, BaseColor>("End", Red)
            };
            if (IsHarvest(operation))
            {
                entries.Add(new KeyValuePair<string, BaseColor>("Pipe open", Blue));
                entries.Add(new KeyValuePair<string, BaseColor>("Cutter on", Yellow));
                entries.Add(new KeyValuePair<string, BaseColor>("Cutter off", Red));
            }
            else if (operation == TrainingOperation.Spraying)
            {
                entries.Add(new KeyValuePair<string, BaseColor>("Arms open", Blue));
                entries.Add(new KeyValuePair<string, BaseColor>("Spraying on", Yellow));
                entries.Add(new KeyValuePair<string, BaseColor>("Spraying off", Red));
            }
            else if (operation == TrainingOperation.Planting)
            {
                entries.Add(new KeyValuePair<string, BaseColor>("Planting on", Yellow));
                entries.Add(new KeyValuePair<string, BaseColor>("Planting off", Red));
            }
            cb.BeginText(); cb.SetFontAndSize(BaseFont.CreateFont(), 7f); cb.SetColorFill(Ink);
            cb.SetTextMatrix(x, y + 9f); cb.ShowText("Map legend"); cb.EndText();
            float entryX = x;
            foreach (KeyValuePair<string, BaseColor> entry in entries)
            {
                cb.SetColorFill(entry.Value); cb.Circle(entryX + 3f, y, 2.5f); cb.Fill();
                cb.BeginText(); cb.SetFontAndSize(BaseFont.CreateFont(), 7f); cb.SetColorFill(Ink);
                cb.SetTextMatrix(entryX + 9f, y - 2f); cb.ShowText(entry.Key); cb.EndText();
                entryX += 82f;
            }
        }

        private static void AddSignaturePage(Document document)
        {
            AddPageHeading(document, "LESSON SIGN-OFF", "Review and acknowledge the training session");
            document.Add(Chunk.NEWLINE); document.Add(Chunk.NEWLINE);
            AddSignatureRow(document, "Student Signature");
            document.Add(Chunk.NEWLINE); document.Add(Chunk.NEWLINE);
            AddSignatureRow(document, "Instructor Signature");
        }

        private static void AddSignatureRow(Document document, string label)
        {
            var table = new PdfPTable(1) { WidthPercentage = 78, HorizontalAlignment = Element.ALIGN_CENTER, SpacingAfter = 16f };
            var line = new PdfPCell(new Phrase(" ")) { Border = Rectangle.BOTTOM, BorderColor = Border, FixedHeight = 42f }; table.AddCell(line);
            var caption = new PdfPCell(new Phrase(label, new Font(Font.FontFamily.HELVETICA, 10f, Font.NORMAL, Muted))) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, PaddingTop = 7f }; table.AddCell(caption); document.Add(table);
        }

        private static void AddPageHeading(Document document, string title, string subtitle)
        {
            var titleFont = new Font(Font.FontFamily.HELVETICA, 19f, Font.BOLD, Ink);
            var subtitleFont = new Font(Font.FontFamily.HELVETICA, 10f, Font.NORMAL, Muted);
            var heading = new Paragraph(title, titleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 5f }; document.Add(heading);
            var sub = new Paragraph(subtitle, subtitleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 17f }; document.Add(sub);
        }

        private static void AddSectionHeading(Document document, string title)
        {
            document.Add(new Paragraph(title, new Font(Font.FontFamily.HELVETICA, 11f, Font.BOLD, Ink)) { SpacingBefore = 7f, SpacingAfter = 4f, KeepTogether = true });
        }

        private static void AddInfo(PdfPTable table, string label, string value)
        {
            var cell = new PdfPCell { BorderColor = Border, Padding = 6f, BackgroundColor = Light };
            cell.AddElement(new Paragraph(label.ToUpperInvariant(), new Font(Font.FontFamily.HELVETICA, 7f, Font.BOLD, Muted)));
            cell.AddElement(new Paragraph(string.IsNullOrWhiteSpace(value) ? "Not provided" : value, new Font(Font.FontFamily.HELVETICA, 9f, Font.NORMAL, Ink))); table.AddCell(cell);
        }

        private static void AddMetric(PdfPTable table, string title, string value, BaseColor accent)
        {
            var cell = new PdfPCell { BorderColor = accent, BorderWidth = 1.2f, Padding = 9f, HorizontalAlignment = Element.ALIGN_CENTER };
            cell.AddElement(new Paragraph(title, new Font(Font.FontFamily.HELVETICA, 8f, Font.BOLD, Muted)) { Alignment = Element.ALIGN_CENTER });
            cell.AddElement(new Paragraph(value, new Font(Font.FontFamily.HELVETICA, 16f, Font.BOLD, accent)) { Alignment = Element.ALIGN_CENTER }); table.AddCell(cell);
        }

        private static void AddStat(PdfPTable table, string label, string value)
        {
            var cell = new PdfPCell { BorderColor = Border, Padding = 4f, HorizontalAlignment = Element.ALIGN_CENTER };
            cell.AddElement(new Paragraph(label, new Font(Font.FontFamily.HELVETICA, 7f, Font.NORMAL, Muted)) { Alignment = Element.ALIGN_CENTER });
            cell.AddElement(new Paragraph(value, new Font(Font.FontFamily.HELVETICA, 8f, Font.BOLD, Ink)) { Alignment = Element.ALIGN_CENTER }); table.AddCell(cell);
        }

        private static void AddLine(Document document, string label, string value, bool bullet)
        {
            string content = bullet ? "? " + label : string.IsNullOrEmpty(value) ? label : label + ": " + value;
            document.Add(new Paragraph(content, new Font(Font.FontFamily.HELVETICA, 9f, Font.NORMAL, Ink)) { SpacingAfter = 2f, KeepTogether = true });
        }

        private static void AddBullet(Document document, string content, bool ignored)
        { AddLine(document, content, null, true); }

        private static bool AddTaskList(Document document, TrainingReportInput report, bool passed)
        {
            bool found = false;
            for (int i = 0; i < report.Tasks.Count; i++)
            {
                TrainingReportTask task = report.Tasks[i]; if (task == null || task.Passed != passed) continue; found = true;
                string title = string.IsNullOrWhiteSpace(task.Title) ? "Task " + (task.TaskIndex + 1) : task.Title;
                AddBullet(document, title + (string.IsNullOrWhiteSpace(task.Result) ? string.Empty : ": " + task.Result), false);
            }
            return found;
        }

        private static void AddOperationStats(Document document, TrainingOperation operation, TrainingReportInput report, TrainingReportTelemetryStats stats)
        {
            var table = new PdfPTable(IsHarvest(operation) ? 9 : 7) { WidthPercentage = 100f, SpacingAfter = 12f };
            AddStat(table, "Avg speed", stats.AverageSpeed.ToString("0.0", CultureInfo.InvariantCulture) + " km/h");
            AddStat(table, "Max speed", stats.MaximumSpeed.ToString("0.0", CultureInfo.InvariantCulture) + " km/h");
            AddStat(table, "Avg RPM", stats.AverageRpm.ToString("0", CultureInfo.InvariantCulture));
            AddStat(table, "Max RPM", stats.MaximumRpm.ToString("0", CultureInfo.InvariantCulture));
            AddStat(table, "Avg throttle", (stats.AverageThrottle * 100f).ToString("0", CultureInfo.InvariantCulture) + "%");
            AddStat(table, "Avg brake", (stats.AverageBrake * 100f).ToString("0", CultureInfo.InvariantCulture) + "%");
            if (IsHarvest(operation))
            { AddStat(table, "Grains in truck", stats.GrainsInTruck.ToString("0", CultureInfo.InvariantCulture)); AddStat(table, "Grain loss", stats.GrainLoss.ToString("0", CultureInfo.InvariantCulture)); AddStat(table, "Efficiency", stats.HarvestEfficiencyPercent.ToString("0.0", CultureInfo.InvariantCulture) + "%"); }
            else AddStat(table, "Efficiency", (report.EfficiencyPercent > 0f ? report.EfficiencyPercent : SpeedEfficiency(operation, stats.AverageSpeed)).ToString("0.0", CultureInfo.InvariantCulture) + "%");
            document.Add(table);
        }

        private static float SpeedEfficiency(TrainingOperation operation, float speed)
        {
            float limit = TrainingReportInsights.GetRecommendedSpeed(operation);
            if (limit <= 0f || speed <= 0f) return 0f;
            return Math.Min(100f, Math.Min(speed / limit, limit / speed) * 100f);
        }

        private static bool IsHarvest(TrainingOperation operation)
        { return operation == TrainingOperation.Harvesting || operation == TrainingOperation.HarvestingCorn; }

        private static int CountTasks(TrainingReportInput report, bool passed)
        {
            int count = 0; foreach (TrainingReportTask task in report.Tasks) if (task != null && task.Passed == passed) count++; return count;
        }

        private static string OperationName(TrainingOperation operation)
        { return operation == TrainingOperation.HarvestingCorn ? "Corn harvesting" : operation.ToString(); }

        private static string FirstNonEmpty(string first, string second)
        { return string.IsNullOrWhiteSpace(first) ? (string.IsNullOrWhiteSpace(second) ? "Not provided" : second) : first; }

        private static string FormatPercent(float value)
        { return value.ToString("0.0", CultureInfo.InvariantCulture) + "%"; }

        private static string FormatTime(float seconds)
        {
            int whole = Math.Max(0, (int)seconds); return (whole / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (whole % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private static float RouteDistance(TrainingRouteMap route)
        {
            if (route == null || route.Path == null || route.Path.Count < 2) return 0f;
            float distance = 0f;
            for (int i = 0; i + 1 < route.Path.Count; i++)
            {
                float dx = (route.Path[i + 1].X - route.Path[i].X) * route.WidthMeters;
                float dy = (route.Path[i + 1].Y - route.Path[i].Y) * route.LengthMeters;
                distance += (float)Math.Sqrt(dx * dx + dy * dy);
            }
            return distance;
        }

        private static BaseColor MarkerColor(TrainingRouteMarkerType type)
        {
            switch (type)
            {
                case TrainingRouteMarkerType.PlantingOn:
                case TrainingRouteMarkerType.SprayOn:
                case TrainingRouteMarkerType.CutterOn: return Yellow;
                case TrainingRouteMarkerType.ArmsOpened:
                case TrainingRouteMarkerType.UnloadingPipeOpened: return Blue;
                default: return Red;
            }
        }

        private static void DrawMapDot(PdfContentByte cb, float x, float y, BaseColor color, float radius)
        { cb.SetColorFill(color); cb.Circle(x, y, radius); cb.Fill(); }

        private static float Clamp01(float value)
        { return value < 0f ? 0f : value > 1f ? 1f : value; }

        private static string SafeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid.ToString(), string.Empty);
            return value.Trim();
        }

        private sealed class PageFooter : PdfPageEventHelper
        {
            public override void OnEndPage(PdfWriter writer, Document document)
            {
                PdfContentByte cb = writer.DirectContent; cb.SetColorStroke(Border); cb.SetLineWidth(0.5f);
                cb.MoveTo(document.LeftMargin, document.BottomMargin - 12f); cb.LineTo(document.PageSize.Width - document.RightMargin, document.BottomMargin - 12f); cb.Stroke();
                cb.BeginText(); cb.SetFontAndSize(BaseFont.CreateFont(), 8f); cb.SetColorFill(Muted);
                cb.SetTextMatrix(document.LeftMargin, document.BottomMargin - 27f); cb.ShowText("Training report");
                cb.SetTextMatrix(document.PageSize.Width - document.RightMargin - 50f, document.BottomMargin - 27f); cb.ShowText("Page " + writer.PageNumber.ToString(CultureInfo.InvariantCulture)); cb.EndText();
            }
        }
    }
}

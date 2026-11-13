using System;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using TrainingReports;
using UnityEngine;

namespace TrainingReports.PdfExport
{
    public sealed class LessonPdfExporter : MonoBehaviour
    {
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
            if ((report.RouteImagePng == null || report.RouteImagePng.Length == 0) && routePainter != null &&
                routePainter.TryGetRouteImage(out byte[] routeImage, out _, out _))
                report.RouteImagePng = routeImage;
            return GeneratePdf(report);
        }

        public string GeneratePdf(TrainingReportInput report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            string directory = string.IsNullOrWhiteSpace(outputDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Training Reports")
                : outputDirectory;
            Directory.CreateDirectory(directory);
            string stamp = string.IsNullOrWhiteSpace(report.CompletedAt) ? DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm") : report.CompletedAt;
            string path = Path.Combine(directory, "Training_Report_" + SafeFileName(stamp) + ".pdf");
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var document = new Document(PageSize.A4, 42f, 42f, 48f, 42f))
            {
                PdfWriter.GetInstance(document, stream);
                document.Open();
                var title = new Font(Font.FontFamily.HELVETICA, 20f, Font.BOLD, new BaseColor(47, 69, 91));
                var body = new Font(Font.FontFamily.HELVETICA, 11f, Font.NORMAL, new BaseColor(35, 41, 48));
                var heading = new Paragraph("Training report", title) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 18f };
                document.Add(heading);
                AddField(document, body, "Student", report.StudentName);
                AddField(document, body, "Instructor", report.InstructorName);
                AddField(document, body, "Operation", report.Operation.ToString());
                AddField(document, body, "Crop", report.Crop);
                AddField(document, body, "Farm", report.FarmName);
                AddField(document, body, "Scene", report.SceneName);
                AddField(document, body, "Vehicle", report.VehicleModel);
                AddField(document, body, "Completed", report.CompletedAt);
                AddField(document, body, "Duration", report.Duration);
                AddField(document, body, "Score", report.ScorePercent.ToString("0.0") + "%");
                AddField(document, body, "Errors", report.ErrorCount.ToString());
                AddField(document, body, "Average speed", report.AverageSpeed.ToString("0.0"));
                AddField(document, body, "Average RPM", report.AverageRpm.ToString("0"));
                AddField(document, body, "Average throttle", report.AverageThrottle.ToString("0.0"));
                AddField(document, body, "Average brake", report.AverageBrake.ToString("0.0"));
                if (report.Tasks.Count > 0)
                {
                    document.Add(new Paragraph("Task results", new Font(body.BaseFont, 13f, Font.BOLD)) { SpacingBefore = 12f, SpacingAfter = 6f });
                    foreach (TrainingReportTask task in report.Tasks)
                        document.Add(new Paragraph((task.Passed ? "PASS  " : "REVIEW  ") + task.Title + ": " + task.Result, body));
                }
                if (report.Telemetry.Count > 0)
                {
                    document.Add(new Paragraph("Telemetry samples", new Font(body.BaseFont, 13f, Font.BOLD)) { SpacingBefore = 12f, SpacingAfter = 6f });
                    var table = new PdfPTable(5) { WidthPercentage = 100f, SpacingAfter = 10f };
                    table.SetWidths(new[] { 1.2f, 1f, 1f, 1f, 1f });
                    AddTelemetryCell(table, "Time", true);
                    AddTelemetryCell(table, "Speed", true);
                    AddTelemetryCell(table, "RPM", true);
                    AddTelemetryCell(table, "Throttle", true);
                    AddTelemetryCell(table, "Brake", true);
                    int stride = Mathf.Max(1, report.Telemetry.Count / 120);
                    for (int i = 0; i < report.Telemetry.Count; i += stride)
                    {
                        TrainingTelemetryPoint point = report.Telemetry[i];
                        AddTelemetryCell(table, point.TimeSeconds.ToString("0.0"), false);
                        AddTelemetryCell(table, point.Speed.ToString("0.0"), false);
                        AddTelemetryCell(table, point.Rpm.ToString("0"), false);
                        AddTelemetryCell(table, point.Throttle.ToString("0.0"), false);
                        AddTelemetryCell(table, point.Brake.ToString("0.0"), false);
                    }
                    document.Add(table);
                }
                if (report.RouteImagePng != null && report.RouteImagePng.Length > 0)
                {
                    document.NewPage();
                    document.Add(new Paragraph("Route map", title));
                    var image = Image.GetInstance(report.RouteImagePng);
                    image.ScaleToFit(document.PageSize.Width - document.LeftMargin - document.RightMargin, document.PageSize.Height - document.TopMargin - document.BottomMargin);
                    image.Alignment = Element.ALIGN_CENTER;
                    document.Add(image);
                }
                document.Close();
            }
            PdfGenerated?.Invoke(path);
            return path;
        }

        private static void AddField(Document document, Font font, string label, string value)
        {
            document.Add(new Paragraph(label + ": " + (string.IsNullOrWhiteSpace(value) ? "Not provided" : value), font));
        }

        private static void AddTelemetryCell(PdfPTable table, string value, bool header)
        {
            var cell = new PdfPCell(new Phrase(value, new Font(Font.FontFamily.HELVETICA, header ? 9f : 8f, header ? Font.BOLD : Font.NORMAL)));
            cell.Padding = 4f;
            if (header) cell.BackgroundColor = new BaseColor(232, 236, 240);
            table.AddCell(cell);
        }

        private static string SafeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid.ToString(), string.Empty);
            return value.Trim();
        }
    }
}

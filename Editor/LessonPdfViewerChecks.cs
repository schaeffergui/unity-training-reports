#if TRAINING_REPORTS_PDF && TRAINING_REPORTS_SIMULATOR
using Car2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class LessonPdfViewerChecks
{
    public static string Status { get; private set; } = "Not started";
    private static readonly List<string> Passed = new List<string>();
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string fixturePath;

    [MenuItem("Training/Tests/Visualizador de PDF")]
    public static async void Run()
    {
        if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu")
        {
            Debug.LogWarning("Run the PDF check in the MainMenu in Play Mode.");
            return;
        }
        if (Status == "Executando") return;
        Status = "Executando";
        Passed.Clear();
        string folder = Path.GetFullPath("Temp/LessonPdfViewerChecks");
        Directory.CreateDirectory(folder);
        try
        {
            fixturePath = Path.Combine(folder, "Relatorio_teste.pdf");
            CreateFixture(fixturePath);
            await Task.Run(() => CheckDocument(folder));
            await CheckViewer();
            await CheckExporter(folder);
            Status = "Passed: " + Passed.Count + " checks";
            Debug.Log(Status + "\n" + string.Join("\n", Passed));
        }
        catch (Exception exception)
        {
            Status = "Falhou: " + exception;
            Debug.LogException(exception);
        }
        finally
        {
            File.WriteAllText(Path.Combine(folder, "resultado.txt"), Status + "\n" + string.Join("\n", Passed));
            var viewer = UnityEngine.Object.FindAnyObjectByType<LessonPdfViewer>();
            if (viewer != null) viewer.Close();
        }
    }

    private static void CheckDocument(string folder)
    {
        using (var document = LessonPdfDocument.Load(fixturePath, CancellationToken.None))
        {
            Check(document.PageSizes.Length == 4, "Local read of four pages");
            var image = document.Render(0, 900, CancellationToken.None);
            Check(image.Width == 635 && image.Height == 900 && image.Pixels.Length == image.Width * image.Height * 4, "Proportional rendering and bounded buffer");
            Check(image.Pixels[0] == 255 && image.Pixels[1] == 255 && image.Pixels[2] == 255 && image.Pixels[3] == 255, "RGBA compositing over white paper");
            bool purple = false;
            for (int i = 0; i < image.Pixels.Length; i += 4)
            {
                if (image.Pixels[i] == 88 && image.Pixels[i + 1] == 22 && image.Pixels[i + 2] == 193) purple = true;
            }
            Check(purple, "BGRA conversion preserves the PDF colors");
            string first = document.SaveCopy(folder, CancellationToken.None);
            string second = document.SaveCopy(folder, CancellationToken.None);
            Check(first != second && File.ReadAllBytes(first).SequenceEqual(File.ReadAllBytes(fixturePath)) && File.ReadAllBytes(second).SequenceEqual(File.ReadAllBytes(fixturePath)), "Identical copies without overwriting files");
            ExpectFailure<ArgumentOutOfRangeException>(() => document.Render(4, 900, CancellationToken.None));
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                ExpectFailure<OperationCanceledException>(() => document.Render(0, 900, cancellation.Token));
            }
        }
        string[] forbidden = { "https://example.com/aluno.pdf", "file:///C:/aluno.pdf", @"\\servidor\aluno.pdf", "relativo.pdf", fixturePath + ":fluxo.pdf" };
        for (int i = 0; i < forbidden.Length; i++)
        {
            string path = forbidden[i];
            ExpectFailure<InvalidDataException>(() => LessonPdfDocument.Load(path, CancellationToken.None));
        }
        string invalid = Path.Combine(folder, "invalido.pdf");
        File.WriteAllText(invalid, "Not a PDF.");
        ExpectFailure<InvalidDataException>(() => LessonPdfDocument.Load(invalid, CancellationToken.None));
        string oversized = Path.Combine(folder, "grande.pdf");
        using (var stream = File.Create(oversized)) stream.SetLength(64L * 1024 * 1024 + 1);
        ExpectFailure<InvalidDataException>(() => LessonPdfDocument.Load(oversized, CancellationToken.None));
        File.Delete(oversized);
        using (var stream = new FileStream(fixturePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Check(stream.Length > 0, "Streams and handles released after rendering");
        }
        var disposed = LessonPdfDocument.Load(fixturePath, CancellationToken.None);
        disposed.Dispose();
        ExpectFailure<ObjectDisposedException>(() => disposed.Render(0, 900, CancellationToken.None));
        Check(true, "Validation of paths, size, cancellation, indices and disposal");
    }

    private static async Task CheckViewer()
    {
        EventSystem originalSystem = EventSystem.current;
        GameObject originalSelection = originalSystem != null ? originalSystem.currentSelectedGameObject : null;
        bool cursorVisible = Cursor.visible;
        CursorLockMode cursorLock = Cursor.lockState;
        float timeScale = Time.timeScale;
        LessonPdfViewer.Open(fixturePath);
        var viewer = UnityEngine.Object.FindAnyObjectByType<LessonPdfViewer>();
        await Until(() => Ready(viewer));
        Check(LessonPdfViewer.IsOpen && (originalSystem == null || !originalSystem.enabled), "Modal blocks the previous interface");
        var scroll = Get<ScrollRect>(viewer, "scroll");
        var image = Get<RawImage>(viewer, "pageImage");
        Check(image.rectTransform.rect.height <= scroll.viewport.rect.height && image.rectTransform.rect.width <= scroll.viewport.rect.width, "Fit page respects the reading area");
        viewer.ChangePage(1);
        await Until(() => Ready(viewer));
        Check(Get<TMP_Text>(viewer, "pageLabel").text == "Page 2 of 4", "Page navigation");
        viewer.SaveCopyAsync();
        await Until(() => !Get<bool>(viewer, "saving"));
        const string copyPrefix = "Copy saved to: ";
        string copyStatus = Get<TMP_Text>(viewer, "statusLabel").text;
        Check(copyStatus.StartsWith(copyPrefix, StringComparison.Ordinal), "Saving a copy from the UI completes in Documents");
        string savedCopy = copyStatus.Substring(copyPrefix.Length);
        Check(File.ReadAllBytes(savedCopy).SequenceEqual(File.ReadAllBytes(fixturePath)), "UI copy keeps every byte of the report");
        File.Delete(savedCopy);
        viewer.FitWidth();
        await Until(() => Ready(viewer));
        Check(Mathf.Abs(image.rectTransform.rect.width - (scroll.viewport.rect.width - 40f)) < 2f && scroll.content.rect.height > scroll.viewport.rect.height, "Ajustar largura permite rolagem vertical");
        try
        {
            Time.timeScale = 0f;
            for (int i = 0; i < 6; i++)
            {
                viewer.ChangePage(i % 2 == 0 ? 1 : -1);
                viewer.ChangeZoom(i % 2 == 0 ? 1 : -1);
            }
            await Until(() => Ready(viewer));
            Check(Get<TMP_Text>(viewer, "pageLabel").text == "Page 2 of 4", "Rapid requests keep the last page while the game is paused");
        }
        finally
        {
            Time.timeScale = timeScale;
        }
        Texture texture = image.texture;
        viewer.ChangeZoom(1);
        viewer.Close();
        await Task.Delay(300);
        Check(!LessonPdfViewer.IsOpen && texture == null && (originalSystem == null || originalSystem.enabled), "Closing cancels the work, releases the texture and restores the EventSystem");
        Check(Cursor.visible == cursorVisible && Cursor.lockState == cursorLock && (originalSystem == null || originalSystem.currentSelectedGameObject == originalSelection), "Cursor e foco restaurados");
        LessonPdfViewer.Open(fixturePath);
        viewer = UnityEngine.Object.FindAnyObjectByType<LessonPdfViewer>();
        await Until(() => Ready(viewer));
        viewer.Close();
        await Task.Delay(50);
        Check(true, "Reopening after PDFium disposal");
        var history = UnityEngine.Object.FindAnyObjectByType<RDAAdministrationController>(FindObjectsInactive.Include);
        string previousReport = Get<string>(history, "selectedReport");
        try
        {
            typeof(RDAAdministrationController).GetField("selectedReport", PrivateInstance).SetValue(history, fixturePath);
            typeof(RDAAdministrationController).GetMethod("OpenReport", PrivateInstance).Invoke(history, null);
            viewer = UnityEngine.Object.FindAnyObjectByType<LessonPdfViewer>();
            await Until(() => Ready(viewer));
            Check(LessonPdfViewer.IsOpen, "History opens the PDF inside the game");
            viewer.Close();
            await Task.Delay(50);
        }
        finally
        {
            typeof(RDAAdministrationController).GetField("selectedReport", PrivateInstance).SetValue(history, previousReport);
        }
        LessonPdfViewer.Open(Path.Combine(Path.GetDirectoryName(fixturePath), "ausente.pdf"));
        viewer = UnityEngine.Object.FindAnyObjectByType<LessonPdfViewer>();
        await Until(() => Get<Button>(viewer, "retryButton").gameObject.activeSelf);
        Check(Get<TMP_Text>(viewer, "loadingLabel").text == "The PDF file was not found." && Get<Button>(viewer, "closeButton").interactable, "Error message keeps the retry and close controls");
        viewer.Close();
        await Task.Delay(50);
    }

    private static async Task CheckExporter(string folder)
    {
        if (TrainingManager.Instance != null) throw new InvalidOperationException("The test MainMenu must not contain an active training.");
        var controller = RDALessonReportController.Instance;
        var student = controller.currentStudent;
        var instructor = controller.currentInstructor;
        var lesson = controller.currentLesson;
        bool started = controller.isLessonStarted;
        float duration = controller.lessonDurationTimer;
        string studentName = "Teste_PDF_" + Guid.NewGuid().ToString("N");
        var root = new GameObject("Exporter check");
        root.SetActive(false);
        var training = root.AddComponent<TrainingManager>();
        training.enabled = false;
        var minimap = root.AddComponent<MinimapController>();
        minimap.enabled = false;
        var mapRect = new GameObject("Mapa de teste", typeof(RectTransform)).GetComponent<RectTransform>();
        mapRect.SetParent(root.transform, false);
        typeof(MinimapController).GetField("scrollViewRectTransform", PrivateInstance).SetValue(minimap, mapRect);
        var exporter = root.AddComponent<LessonPdfExporter>();
        string reportPath = null;
        int finishCount = 0;
        UnityEngine.Events.UnityAction onFinish = () => finishCount++;
        try
        {
            root.SetActive(true);
            controller.currentStudent = new RDStudent { Name = studentName, Id = "TESTE" };
            controller.currentInstructor = new RDInstructor { Name = "Instrutor de teste", Id = "TESTE" };
            controller.currentLesson = null;
            controller.finishLessonEvents.AddListener(onFinish);
            bool completed = await controller.FinishLessonAsync(LessonStudentPerformance.None);
            reportPath = Get<string>(exporter, "lastGeneratedPdfPath");
            var viewer = UnityEngine.Object.FindAnyObjectByType<LessonPdfViewer>();
            await Until(() => viewer != null && Ready(viewer));
            Check(completed && finishCount == 1 && LessonPdfViewer.IsOpen && File.Exists(reportPath), "Finishing the lesson saves the report, keeps the events and opens the PDF automatically");
            byte[] original = File.ReadAllBytes(reportPath);
            using (var reader = new iTextSharp.text.pdf.PdfReader(original))
            {
                string text = iTextSharp.text.pdf.parser.PdfTextExtractor.GetTextFromPage(reader, 1);
                Check(reader.NumberOfPages >= 4 && text.Contains(studentName) && text.Contains("PERFORMANCE REPORT"), "LessonPdfExporter content and pages preserved");
            }
            viewer.ChangePage(2);
            viewer.FitWidth();
            await Until(() => Ready(viewer));
            Check(File.ReadAllBytes(reportPath).SequenceEqual(original), "Navigating and zooming does not modify the report");
            File.Copy(reportPath, Path.Combine(folder, "Relatorio_aula.pdf"), true);
            viewer.Close();
            await Task.Delay(50);
            exporter.OpenLastGeneratedPDF();
            viewer = UnityEngine.Object.FindAnyObjectByType<LessonPdfViewer>();
            await Until(() => viewer != null && Ready(viewer));
            Check(LessonPdfViewer.IsOpen, "Existing button reopens the report inside the game");
            viewer.Close();
        }
        finally
        {
            controller.finishLessonEvents.RemoveListener(onFinish);
            controller.currentStudent = student;
            controller.currentInstructor = instructor;
            controller.currentLesson = lesson;
            controller.isLessonStarted = started;
            controller.lessonDurationTimer = duration;
            UnityEngine.Object.Destroy(root);
            if (!string.IsNullOrEmpty(reportPath) && Path.GetFileName(Path.GetDirectoryName(reportPath)) == studentName)
            {
                File.Delete(reportPath);
                Directory.Delete(Path.GetDirectoryName(reportPath));
            }
        }
    }

    private static void CreateFixture(string path)
    {
        using (var stream = File.Create(path))
        using (var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4))
        {
            var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, stream);
            document.Open();
            for (int i = 0; i < 4; i++)
            {
                if (i > 0) document.NewPage();
                document.Add(new iTextSharp.text.Paragraph("LOCAL TEST REPORT - PAGE " + (i + 1)));
                writer.DirectContent.SetColorFill(new iTextSharp.text.BaseColor(88, 22, 193));
                writer.DirectContent.Rectangle(40, 500, 200, 60);
                writer.DirectContent.Fill();
            }
        }
    }

    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
    private static bool Ready(LessonPdfViewer viewer) => viewer != null && Get<Texture2D>(viewer, "pageTexture") != null && !Get<GameObject>(viewer, "loadingPanel").activeSelf;

    private static async Task Until(Func<bool> predicate)
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (!predicate())
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("The viewer did not finish the operation.");
            await Task.Delay(25);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Passed.Add(message);
    }

    private static void ExpectFailure<T>(System.Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException("Expected exception " + typeof(T).Name);
    }
}
#endif

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
    public static string Status { get; private set; } = "Não iniciado";
    private static readonly List<string> Passed = new List<string>();
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string fixturePath;

    [MenuItem("Training/Tests/Visualizador de PDF")]
    public static async void Run()
    {
        if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu")
        {
            Debug.LogWarning("Execute a verificação do PDF na MainMenu em Play Mode.");
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
            Status = "Aprovado: " + Passed.Count + " verificações";
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
            Check(document.PageSizes.Length == 4, "Leitura local de quatro páginas");
            var image = document.Render(0, 900, CancellationToken.None);
            Check(image.Width == 635 && image.Height == 900 && image.Pixels.Length == image.Width * image.Height * 4, "Renderização proporcional e buffer limitado");
            Check(image.Pixels[0] == 255 && image.Pixels[1] == 255 && image.Pixels[2] == 255 && image.Pixels[3] == 255, "Composição RGBA sobre papel branco");
            bool purple = false;
            for (int i = 0; i < image.Pixels.Length; i += 4)
            {
                if (image.Pixels[i] == 88 && image.Pixels[i + 1] == 22 && image.Pixels[i + 2] == 193) purple = true;
            }
            Check(purple, "Conversão BGRA preserva as cores do PDF");
            string first = document.SaveCopy(folder, CancellationToken.None);
            string second = document.SaveCopy(folder, CancellationToken.None);
            Check(first != second && File.ReadAllBytes(first).SequenceEqual(File.ReadAllBytes(fixturePath)) && File.ReadAllBytes(second).SequenceEqual(File.ReadAllBytes(fixturePath)), "Cópias idênticas sem sobrescrever arquivos");
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
        File.WriteAllText(invalid, "Não é um PDF.");
        ExpectFailure<InvalidDataException>(() => LessonPdfDocument.Load(invalid, CancellationToken.None));
        string oversized = Path.Combine(folder, "grande.pdf");
        using (var stream = File.Create(oversized)) stream.SetLength(64L * 1024 * 1024 + 1);
        ExpectFailure<InvalidDataException>(() => LessonPdfDocument.Load(oversized, CancellationToken.None));
        File.Delete(oversized);
        using (var stream = new FileStream(fixturePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Check(stream.Length > 0, "Streams e handles liberados após renderizar");
        }
        var disposed = LessonPdfDocument.Load(fixturePath, CancellationToken.None);
        disposed.Dispose();
        ExpectFailure<ObjectDisposedException>(() => disposed.Render(0, 900, CancellationToken.None));
        Check(true, "Validação de caminhos, tamanho, cancelamento, índices e descarte");
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
        Check(LessonPdfViewer.IsOpen && (originalSystem == null || !originalSystem.enabled), "Modal bloqueia a interface anterior");
        var scroll = Get<ScrollRect>(viewer, "scroll");
        var image = Get<RawImage>(viewer, "pageImage");
        Check(image.rectTransform.rect.height <= scroll.viewport.rect.height && image.rectTransform.rect.width <= scroll.viewport.rect.width, "Ajustar página respeita a área de leitura");
        viewer.ChangePage(1);
        await Until(() => Ready(viewer));
        Check(Get<TMP_Text>(viewer, "pageLabel").text == "Página 2 de 4", "Navegação de páginas");
        viewer.SaveCopyAsync();
        await Until(() => !Get<bool>(viewer, "saving"));
        const string copyPrefix = "Cópia salva em: ";
        string copyStatus = Get<TMP_Text>(viewer, "statusLabel").text;
        Check(copyStatus.StartsWith(copyPrefix, StringComparison.Ordinal), "Salvar cópia pela interface conclui em Documentos");
        string savedCopy = copyStatus.Substring(copyPrefix.Length);
        Check(File.ReadAllBytes(savedCopy).SequenceEqual(File.ReadAllBytes(fixturePath)), "Cópia da interface mantém todos os bytes do relatório");
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
            Check(Get<TMP_Text>(viewer, "pageLabel").text == "Página 2 de 4", "Solicitações rápidas mantêm a última página com o jogo pausado");
        }
        finally
        {
            Time.timeScale = timeScale;
        }
        Texture texture = image.texture;
        viewer.ChangeZoom(1);
        viewer.Close();
        await Task.Delay(300);
        Check(!LessonPdfViewer.IsOpen && texture == null && (originalSystem == null || originalSystem.enabled), "Fechar cancela o trabalho, libera a textura e restaura o EventSystem");
        Check(Cursor.visible == cursorVisible && Cursor.lockState == cursorLock && (originalSystem == null || originalSystem.currentSelectedGameObject == originalSelection), "Cursor e foco restaurados");
        LessonPdfViewer.Open(fixturePath);
        viewer = UnityEngine.Object.FindAnyObjectByType<LessonPdfViewer>();
        await Until(() => Ready(viewer));
        viewer.Close();
        await Task.Delay(50);
        Check(true, "Reabertura após descarte do PDFium");
        var history = UnityEngine.Object.FindAnyObjectByType<RDAAdministrationController>(FindObjectsInactive.Include);
        string previousReport = Get<string>(history, "selectedReport");
        try
        {
            typeof(RDAAdministrationController).GetField("selectedReport", PrivateInstance).SetValue(history, fixturePath);
            typeof(RDAAdministrationController).GetMethod("OpenReport", PrivateInstance).Invoke(history, null);
            viewer = UnityEngine.Object.FindAnyObjectByType<LessonPdfViewer>();
            await Until(() => Ready(viewer));
            Check(LessonPdfViewer.IsOpen, "Histórico abre o PDF dentro do jogo");
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
        Check(Get<TMP_Text>(viewer, "loadingLabel").text == "O arquivo PDF não foi encontrado." && Get<Button>(viewer, "closeButton").interactable, "Erro em português mantém os controles de tentar novamente e fechar");
        viewer.Close();
        await Task.Delay(50);
    }

    private static async Task CheckExporter(string folder)
    {
        if (TrainingManager.Instance != null) throw new InvalidOperationException("A MainMenu de teste não deve conter um treinamento ativo.");
        var controller = RDALessonReportController.Instance;
        var student = controller.currentStudent;
        var instructor = controller.currentInstructor;
        var lesson = controller.currentLesson;
        bool started = controller.isLessonStarted;
        float duration = controller.lessonDurationTimer;
        string studentName = "Teste_PDF_" + Guid.NewGuid().ToString("N");
        var root = new GameObject("Verificação do exportador");
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
            Check(completed && finishCount == 1 && LessonPdfViewer.IsOpen && File.Exists(reportPath), "Finalizar aula salva o relatório, mantém os eventos e abre o PDF automaticamente");
            byte[] original = File.ReadAllBytes(reportPath);
            using (var reader = new iTextSharp.text.pdf.PdfReader(original))
            {
                string text = iTextSharp.text.pdf.parser.PdfTextExtractor.GetTextFromPage(reader, 1);
                Check(reader.NumberOfPages >= 4 && text.Contains(studentName) && text.Contains("RELATÓRIO DE DESEMPENHO"), "Conteúdo e páginas do LessonPdfExporter preservados");
            }
            viewer.ChangePage(2);
            viewer.FitWidth();
            await Until(() => Ready(viewer));
            Check(File.ReadAllBytes(reportPath).SequenceEqual(original), "Navegar e ampliar não modifica o relatório");
            File.Copy(reportPath, Path.Combine(folder, "Relatorio_aula.pdf"), true);
            viewer.Close();
            await Task.Delay(50);
            exporter.OpenLastGeneratedPDF();
            viewer = UnityEngine.Object.FindAnyObjectByType<LessonPdfViewer>();
            await Until(() => viewer != null && Ready(viewer));
            Check(LessonPdfViewer.IsOpen, "Botão existente reabre o relatório dentro do jogo");
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
                document.Add(new iTextSharp.text.Paragraph("RELATORIO LOCAL DE TESTE - PAGINA " + (i + 1)));
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
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("O visualizador não concluiu a operação.");
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
        throw new InvalidOperationException("Era esperada a exceção " + typeof(T).Name);
    }
}

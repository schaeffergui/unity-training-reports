using Car2;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using iTextFont = iTextSharp.text.Font;
using iTextImage = iTextSharp.text.Image; 

[Serializable]

public class MapTrajectoryData
{
    public string mapName;
    public List<Vector2> trajectoryPoints = new List<Vector2>();
    public Vector2 mapSize;
    public Texture2D mapTexture;

    // harvester 
    public List<Vector2> pipeOpenPoints = new List<Vector2>();
    public List<Vector2> cutterOnPoints = new List<Vector2>();
    public List<Vector2> cutterOffPoints = new List<Vector2>();

    // spraying
    public List<Vector2> armsOpenPoints = new List<Vector2>();
    public List<Vector2> sprayingOnPoints = new List<Vector2>();
    public List<Vector2> sprayingOffPoints = new List<Vector2>();


    // planting
    public List<Vector2> plantingActivePoints = new List<Vector2>();   // Plantio ativo
    public List<Vector2> plantingOffPoints = new List<Vector2>();
}

public class LessonPdfExporter : MonoBehaviour
{
    private bool isCollecting = false;

    private static readonly BaseColor COLOR_JD_GREEN = new BaseColor(54, 101, 42); 
    private static readonly BaseColor COLOR_JD_YELLOW = new BaseColor(255, 199, 44);
    private static readonly BaseColor COLOR_JD_BLACK = new BaseColor(33, 33, 33);    
    private static readonly BaseColor COLOR_JD_GRAY = new BaseColor(128, 128, 128);     
    private static readonly BaseColor COLOR_JD_LIGHTGRAY = new BaseColor(240, 240, 240); 

    private static readonly BaseColor COLOR_SPEED = new BaseColor(41, 128, 185); 
    private static readonly BaseColor COLOR_RPM = new BaseColor(39, 174, 96);   
    private static readonly BaseColor COLOR_THROTTLE = new BaseColor(241, 196, 15); 
    private static readonly BaseColor COLOR_BRAKE = new BaseColor(192, 57, 43);  

    private iTextFont fontTitle;
    private iTextFont fontSubtitle;
    private iTextFont fontNormal;
    private iTextFont fontBold;
    private iTextFont fontSmall;
    private iTextFont fontSmallBold;
    private iTextFont fontChartLabel;
    private string lastGeneratedPdfPath = "";

    public void StartLessonTelemetryCollection()
    {
        if (!isCollecting)
        {
            TelemetryDataCollector.Instance.StartCollecting();
            isCollecting = true;
        }
    }

    public void StopLessonTelemetryCollection()
    {
        if (isCollecting)
        {
            TelemetryDataCollector.Instance.StopCollecting();
            isCollecting = false;
        }
    }


    private void InitializeFonts()
    {
        fontTitle = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 20, COLOR_JD_BLACK);
        fontSubtitle = FontFactory.GetFont(FontFactory.HELVETICA, 12, COLOR_JD_GRAY);
        fontNormal = FontFactory.GetFont(FontFactory.HELVETICA, 10, COLOR_JD_BLACK);
        fontBold = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, COLOR_JD_BLACK);
        fontSmall = FontFactory.GetFont(FontFactory.HELVETICA, 8, COLOR_JD_GRAY);
        fontSmallBold = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8, COLOR_JD_BLACK);
        fontChartLabel = FontFactory.GetFont(FontFactory.HELVETICA, 7, COLOR_JD_GRAY);
    }
    public void GeneratePDF(RDLessonReport savedLesson)
    {
        if (savedLesson == null) return;
        InitializeFonts();

        string studentName = savedLesson.Student.Name;
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            studentName = studentName.Replace(c.ToString(), "");
        }

        string documentosPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string folderPath = Path.Combine(documentosPath, "Relatorios Das Aulas", studentName);
        Directory.CreateDirectory(folderPath);

        string dataFormatada = savedLesson.EndDate.ToString("dd-MM-yyyy_HH-mm");
        string fileName = $"Relatorio_Aula_{dataFormatada}.pdf";
        string filePath = Path.Combine(folderPath, fileName);
        lastGeneratedPdfPath = filePath;

        StopLessonTelemetryCollection();
        var mapData = GetMapTrajectoryData();

        string timeClass = TrainingManager.Instance != null
            ? TrainingManager.Instance.ElapsedTimeFormatted
            : "00:00:00";

        using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        {
            using (Document document = new Document(PageSize.A4, 45, 45, 70, 50))
            {
                PdfWriter writer = PdfWriter.GetInstance(document, fileStream);
                writer.PageEvent = new RodapePDF(fontSmall);

                var manager = TrainingManager.Instance;
                var telemetryData = TelemetryDataCollector.Instance.TelemetryHistory;
                var stats = CalculateAdvancedStats(telemetryData);
                string trainingText = GetTrainingText();

                document.Open();

                CreateFirstPage(document, savedLesson, trainingText, timeClass, manager, stats);

                document.NewPage();
                CreateTelemetryPage(writer, document, manager);

                document.NewPage();
                CreateMapPage(document, mapData, stats, writer);

                document.NewPage();
                new AdicionarAssinaturas().Assinaturas(document, writer, fontSmall);

                document.Close();
            }
        }

        string logoPath = Path.Combine(Application.streamingAssetsPath, "logo.png");
        if (File.Exists(logoPath))
        {
            byte[] logoBytes = File.ReadAllBytes(logoPath);
            AdicionarMarcaDaguaImagem(filePath, logoBytes);
        }

        Debug.Log("PDF salvo automaticamente em: " + filePath);
        LessonPdfViewer.Open(filePath);
    }

    private void CreateFirstPage(Document document, RDLessonReport lesson,
            string trainingText, string timeClass, TrainingManager manager, AdvancedStats stats)
    {
        float p = manager.CorrectPercentage;

        string studentName = lesson.Student.Name;
        string instructorName = lesson.Instructor.Name;
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string farmName = sceneName == "Farm1" ? "Fazenda 1" : (sceneName == "Farm2" ? "Fazenda 2" : sceneName);

        PdfPTable headerTable = new PdfPTable(1);
        headerTable.WidthPercentage = 50;
        headerTable.SpacingAfter = 10;

        PdfPCell headerCell = new PdfPCell(new Phrase("Training Simulator", fontTitle));
        headerCell.Border = Rectangle.NO_BORDER;
        headerCell.HorizontalAlignment = Element.ALIGN_CENTER;
        headerTable.AddCell(headerCell);

        PdfPCell subCell = new PdfPCell(new Phrase("Treinamento Operacional", fontSubtitle));
        subCell.Border = Rectangle.NO_BORDER;
        subCell.HorizontalAlignment = Element.ALIGN_CENTER;
        headerTable.AddCell(subCell);

        document.Add(headerTable);

        Paragraph headerDate = new Paragraph($"Gerado em: {System.DateTime.Now:dd/MM/yyyy HH:mm:ss}", fontSubtitle);
        headerDate.Alignment = Element.ALIGN_CENTER;
        headerDate.SpacingAfter = 15;
        document.Add(headerDate);

        Paragraph title = new Paragraph("RELATÓRIO DE DESEMPENHO", fontTitle);
        title.Alignment = Element.ALIGN_CENTER;
        title.SpacingAfter = 15;
        document.Add(title);

        Paragraph Student = new Paragraph();
        Student.Add(new Chunk("Aluno: ", fontBold));
        Student.Add(new Chunk(studentName, fontNormal));
        document.Add(Student);

        Paragraph Instructor = new Paragraph();
        Instructor.Add(new Chunk("Instrutor: ", fontBold));
        Instructor.Add(new Chunk(instructorName, fontNormal));
        document.Add(Instructor);

        Paragraph Training = new Paragraph();
        Training.Add(new Chunk("Treinamento: ", fontBold));
        Training.Add(new Chunk(trainingText, fontNormal));
        document.Add(Training);

        Paragraph vehicleModel = new Paragraph();
        vehicleModel.Add(new Chunk("Modelo do veículo: ", fontBold));
        vehicleModel.Add(new Chunk(GetVehicleModelName(), fontNormal));
        document.Add(vehicleModel);

        Paragraph Fazenda = new Paragraph();
        Fazenda.Add(new Chunk("Local: ", fontBold));
        Fazenda.Add(new Chunk(farmName, fontNormal));
        document.Add(Fazenda);

        Paragraph DateTime = new Paragraph();
        DateTime.Add(new Chunk("Data: ", fontBold));
        DateTime.Add(new Chunk(System.DateTime.Now.ToString("dd/MM/yyyy"), fontNormal));
        document.Add(DateTime);

        Paragraph duracaoPara = new Paragraph();
        duracaoPara.Add(new Chunk("Duração: ", fontBold));
        duracaoPara.Add(new Chunk(timeClass, fontNormal));
        document.Add(duracaoPara);

        document.Add(Chunk.NEWLINE);

        PdfPTable metricsTable = new PdfPTable(3);
        metricsTable.WidthPercentage = 90;
        metricsTable.HorizontalAlignment = Element.ALIGN_CENTER;
        metricsTable.SpacingAfter = 15;

        AddMetricCard(metricsTable, "ACERTOS", manager.CompletedTasks.ToString(), COLOR_JD_GREEN);
        AddMetricCard(metricsTable, "ERROS", manager.ErrorCount.ToString(), COLOR_BRAKE);
        AddMetricCard(metricsTable, "APROVEITAMENTO", $"{p:F0}%", COLOR_JD_YELLOW);

        document.Add(metricsTable);

        document.Add(new Paragraph("Resumo da Aula:", fontBold));
        document.Add(new Paragraph($"Acertos: {manager.CompletedTasks}", fontNormal));
        document.Add(new Paragraph($"Erros: {manager.ErrorCount}", fontNormal));
        document.Add(new Paragraph($"Total de tarefas: {manager.TotalAttempts}", fontNormal));
        document.Add(new Paragraph($"Percentual de acertos: {p:0}%", fontNormal));
        document.Add(new Paragraph($"Retrabalho / ineficiência: {manager.RepeatedOperationPercent:0.0}%", fontNormal));
        document.Add(new Paragraph($"Operação fora da área: {manager.OutsideOperationPercent:0.0}%", fontNormal));
        document.Add(new Paragraph($"Desconto na pontuação: {manager.OperationPenaltyPercent:0.0} pontos percentuais", fontNormal));
        if (RDSceneManager.Operation == RDSceneManager.Operations.Planting)
        {
            document.Add(new Paragraph(
                $"Desperdício estimado no retrabalho: {manager.WastedSeeds:0.0} unid. de sementes e {manager.WastedFuelLiters:0.00} L de combustível.", fontNormal));
        }
        else if (RDSceneManager.Operation == RDSceneManager.Operations.Spraying)
        {
            document.Add(new Paragraph(
                $"Desperdício estimado no retrabalho: {manager.WastedSprayLiters:0.00} L de calda e {manager.WastedFuelLiters:0.00} L de combustível.", fontNormal));
        }
        document.Add(Chunk.NEWLINE);

        document.Add(new Paragraph("Tarefas Concluídas com Sucesso:", fontBold));
        bool hit = false;
        foreach (var task in manager.TaskResults.Where(t => t.Success))
        {
            document.Add(new Paragraph($"• {task.Message}", fontNormal));
            hit = true;
        }
        if (!hit) document.Add(new Paragraph("Nenhuma tarefa concluída com sucesso.", fontNormal));
        document.Add(Chunk.NEWLINE);

        document.Add(new Paragraph("Pontos de Atenção (Erros):", fontBold));
        bool errors = false;
        foreach (var task in manager.TaskResults.Where(t => !t.Success))
        {
            document.Add(new Paragraph($"• {task.Message}", fontNormal));
            errors = true;
        }
        if (!errors) document.Add(new Paragraph("Nenhum erro registrado durante a aula.", fontNormal));
        document.Add(Chunk.NEWLINE);

        System.Text.StringBuilder observacoesBuilder = new System.Text.StringBuilder();

        if (RDSceneManager.Operation == RDSceneManager.Operations.Spraying && stats.AvgSpeed >= 12)
        {
            observacoesBuilder.Append($"• A velocidade média registrada foi de {stats.AvgSpeed:F1} km/h, acima do recomendado (máx. 12 km/h). Velocidades elevadas comprometem a uniformidade da aplicação e aumentam o risco de deriva do produto, deixando a eficiência baixa.");
        }
        else if (RDSceneManager.Operation == RDSceneManager.Operations.Planting && stats.AvgSpeed >= 6)
        {
            observacoesBuilder.Append($"• A velocidade média registrada foi de {stats.AvgSpeed:F1} km/h, acima do recomendado (máx. 6 km/h). Velocidades elevadas podem causar falhas na semeadura, profundidade irregular e estande desuniforme, resultando em perdas de produtividade.");
        }
        else if ((RDSceneManager.Operation == RDSceneManager.Operations.Harvesting || RDSceneManager.Operation == RDSceneManager.Operations.HarvertingCorn) && stats.AvgSpeed >= 6)
        {
            observacoesBuilder.Append($"• A velocidade média registrada foi de {stats.AvgSpeed:F1} km/h, acima do recomendado (máx. 6 km/h). Velocidades elevadas impedem que a barra de corte e o molinete recolham o material corretamente, resultando em perdas que podem representar grande parte da colheita.");
        }

        foreach (var task in manager.TaskResults.Where(t => !t.Success))
        {
            string advice = GetTaskSpecificAdvice(RDSceneManager.Operation, task.TaskIndex);
            if (!string.IsNullOrEmpty(advice))
            {
                if (observacoesBuilder.Length > 0) observacoesBuilder.AppendLine();
                observacoesBuilder.Append($"• {advice}");
            }
        }

        if (manager.ErrorCount > 0)
        {
            if (observacoesBuilder.Length > 0) observacoesBuilder.AppendLine();
            observacoesBuilder.Append("• Foque em aprender mais sobre o procedimento dos veículos.");
        }
        if (observacoesBuilder.Length == 0)
        {
            observacoesBuilder.Append("Nenhuma observação adicional.");
        }

        document.Add(new Paragraph("Observações:", fontBold));
        document.Add(new Paragraph(observacoesBuilder.ToString(), fontNormal));
        document.Add(Chunk.NEWLINE);
    }
    private static string GetVehicleModelName()
    {
        var player = RDVehicles.Player;
        GameObject vehicle = player != null ? player.gameObject : RDSceneManager.VehicleToSpawn;
        if (vehicle == null)
        {
            return "Não informado";
        }

        if (vehicle.TryGetComponent(out RDVehicleConfiguration configuration))
        {
            var vehicleInfos = Resources.LoadAll<VehicleInfo>("Vehicles");
            foreach (var info in vehicleInfos)
            {
                if (info.VehiclePrefab != null && !string.IsNullOrWhiteSpace(info.VehicleName) &&
                    info.VehiclePrefab.TryGetComponent(out RDVehicleConfiguration prefabConfiguration) &&
                    prefabConfiguration.VehicleID == configuration.VehicleID)
                {
                    return info.VehicleName;
                }
            }
        }

        return vehicle.name.Replace("(Clone)", "").Replace('_', ' ').Trim();
    }

    private void CreateTelemetryPage(PdfWriter writer, Document document, TrainingManager manager)
    {
        Paragraph title = new Paragraph("ANÁLISE DE TELEMETRIA", fontTitle);
        title.Alignment = Element.ALIGN_CENTER;
        title.SpacingAfter = 10;
        document.Add(title);

        Paragraph subtitle = new Paragraph("Dados coletados durante a operação", fontSubtitle);
        subtitle.Alignment = Element.ALIGN_CENTER;
        subtitle.SpacingAfter = 20;
        document.Add(subtitle);

        var telemetryData = TelemetryDataCollector.Instance.TelemetryHistory;

        if (telemetryData == null)
        {
            document.Add(new Paragraph("Erro: Dados de telemetria não disponíveis.", fontNormal));
            return;
        }

        if (telemetryData.Count < 2)
        {
            document.Add(new Paragraph($"Dados de telemetria insuficientes para análise. Apenas {telemetryData.Count} ponto(s) coletado(s).", fontNormal));
            return;
        }

        var stats = CalculateAdvancedStats(telemetryData);
        CreateStatsLine(document, stats);

        document.Add(Chunk.NEWLINE);

        float availableHeight = document.PageSize.Height - document.TopMargin - document.BottomMargin - 200;
        float chartHeight = availableHeight / 2.5f;
        float startY = document.PageSize.Height - document.TopMargin - 120;

        float totalLessonSeconds = 0;
        if (TrainingManager.Instance != null)
        {
            string[] timeParts = TrainingManager.Instance.ElapsedTimeFormatted.Split(':');
            if (timeParts.Length == 3)
            {
                totalLessonSeconds = int.Parse(timeParts[0]) * 3600 +
                                     int.Parse(timeParts[1]) * 60 +
                                     int.Parse(timeParts[2]);
            }
        }

        CreateDualChart(writer.DirectContent, document, telemetryData,
            "VELOCIDADE E RPM",
            new string[] { "km/h", "RPM" },
            new Func<TelemetryDataPoint, float>[] { d => d.Speed, d => d.EngineRpm },
            new BaseColor[] { COLOR_SPEED, COLOR_RPM },
            new float[] { stats.MaxSpeed, stats.MaxRpm },
            startY, chartHeight, totalLessonSeconds);

        CreateDualChart(writer.DirectContent, document, telemetryData,
            "COMANDOS (%)",
            new string[] { "Acelerador", "Freio" },
            new Func<TelemetryDataPoint, float>[] { d => d.Throttle * 100, d => d.Brake * 100 },
            new BaseColor[] { COLOR_THROTTLE, COLOR_BRAKE },
            new float[] { 100, 100 },
            startY - chartHeight - 30, chartHeight, totalLessonSeconds);
    }
    private void CreateMapPage(Document document, MapTrajectoryData mapData, AdvancedStats stats, PdfWriter writer)
    {
        Paragraph title = new Paragraph("TRAJETÓRIA DA OPERAÇÃO", fontTitle);
        title.Alignment = Element.ALIGN_CENTER;
        title.SpacingAfter = 10;
        document.Add(title);

        Paragraph subtitle = new Paragraph("Percurso realizado durante a aula", fontSubtitle);
        subtitle.Alignment = Element.ALIGN_CENTER;
        subtitle.SpacingAfter = 20;
        document.Add(subtitle);

        PdfPTable statsTable = new PdfPTable(3);
        statsTable.WidthPercentage = 80;
        statsTable.HorizontalAlignment = Element.ALIGN_CENTER;
        statsTable.SpacingAfter = 20;

        string sceneName = SceneManager.GetActiveScene().name;
        float distanciaPercorrida = CalcularDistanciaPercorrida(mapData.trajectoryPoints, sceneName);
        float tempoTotalSegundos = TrainingManager.Instance != null ?
            TrainingManager.Instance.ElapsedTime : 0f;

        AddStatCell(statsTable, "Distância Total", $"{distanciaPercorrida:F1} m");
        AddStatCell(statsTable, "Tempo de Operação", $"{FormatTimeMMSS(tempoTotalSegundos)} min");
        AddStatCell(statsTable, "Velocidade Média", $"{stats.AvgSpeed:F1} km/h");

        document.Add(statsTable);

        if (mapData.mapTexture != null)
        {
            try
            {
                byte[] mapBytes = mapData.mapTexture.EncodeToPNG();
                iTextImage mapImage = iTextImage.GetInstance(mapBytes);

                float maxWidth = document.PageSize.Width - document.LeftMargin - document.RightMargin - 50;
                float maxHeight = document.PageSize.Height * 0.5f;

                mapImage.ScaleToFit(maxWidth, maxHeight);
                mapImage.Alignment = Element.ALIGN_CENTER;
                mapImage.SpacingAfter = 10;

                document.Add(mapImage);

                PdfContentByte cb = writer.DirectContent;

                float mapX = (document.PageSize.Width - mapImage.ScaledWidth) / 2;
                float mapY = document.PageSize.Height - document.TopMargin - mapImage.ScaledHeight - 136;

                cb.SetColorStroke(COLOR_JD_GREEN);
                cb.SetLineWidth(2f);

                for (int i = 0; i < mapData.trajectoryPoints.Count - 1; i++)
                {
                    Vector2 point1 = mapData.trajectoryPoints[i];
                    Vector2 point2 = mapData.trajectoryPoints[i + 1];

                    float x1 = mapX + (point1.x * mapImage.ScaledWidth);
                    float y1 = mapY + (point1.y * mapImage.ScaledHeight);
                    float x2 = mapX + (point2.x * mapImage.ScaledWidth);
                    float y2 = mapY + (point2.y * mapImage.ScaledHeight);

                    cb.MoveTo(x1, y1);
                    cb.LineTo(x2, y2);
                    cb.Stroke();
                }

                if (mapData.pipeOpenPoints.Count > 0)
                {
                    cb.SetColorFill(COLOR_SPEED);
                    foreach (var point in mapData.pipeOpenPoints)
                    {
                        float x = mapX + (point.x * mapImage.ScaledWidth);
                        float y = mapY + (point.y * mapImage.ScaledHeight);
                        cb.Circle(x, y, 4f);
                        cb.Fill();
                    }
                }

                if (mapData.cutterOnPoints.Count > 0)
                {
                    cb.SetColorFill(COLOR_THROTTLE);
                    foreach (var point in mapData.cutterOnPoints)
                    {
                        float x = mapX + (point.x * mapImage.ScaledWidth);
                        float y = mapY + (point.y * mapImage.ScaledHeight);
                        cb.Circle(x, y, 4f);
                        cb.Fill();
                    }
                }

                if (mapData.cutterOffPoints.Count > 0)
                {
                    cb.SetColorFill(COLOR_BRAKE);
                    foreach (var point in mapData.cutterOffPoints)
                    {
                        float x = mapX + (point.x * mapImage.ScaledWidth);
                        float y = mapY + (point.y * mapImage.ScaledHeight);
                        cb.Circle(x, y, 4f);
                        cb.Fill();
                    }
                }

                if (mapData.pipeOpenPoints.Count > 0)
                {
                    cb.SetColorFill(COLOR_SPEED);
                    foreach (var point in mapData.pipeOpenPoints)
                    {
                        float x = mapX + (point.x * mapImage.ScaledWidth);
                        float y = mapY + (point.y * mapImage.ScaledHeight);
                        cb.Circle(x, y, 4f);
                        cb.Fill();
                    }
                }

                if (mapData.cutterOnPoints.Count > 0)
                {
                    cb.SetColorFill(COLOR_THROTTLE);
                    foreach (var point in mapData.cutterOnPoints)
                    {
                        float x = mapX + (point.x * mapImage.ScaledWidth);
                        float y = mapY + (point.y * mapImage.ScaledHeight);
                        cb.Circle(x, y, 4f);
                        cb.Fill();
                    }
                }

                if (mapData.cutterOffPoints.Count > 0)
                {
                    cb.SetColorFill(COLOR_BRAKE);
                    foreach (var point in mapData.cutterOffPoints)
                    {
                        float x = mapX + (point.x * mapImage.ScaledWidth);
                        float y = mapY + (point.y * mapImage.ScaledHeight);
                        cb.Circle(x, y, 4f);
                        cb.Fill();
                    }
                }

                if (mapData.armsOpenPoints.Count > 0)
                {
                    cb.SetColorFill(COLOR_SPEED);
                    foreach (var point in mapData.armsOpenPoints)
                    {
                        float x = mapX + (point.x * mapImage.ScaledWidth);
                        float y = mapY + (point.y * mapImage.ScaledHeight);
                        cb.Circle(x, y, 4f);
                        cb.Fill();
                    }
                }

                if (mapData.sprayingOnPoints.Count > 0)
                {
                    cb.SetColorFill(COLOR_THROTTLE);
                    foreach (var point in mapData.sprayingOnPoints)
                    {
                        float x = mapX + (point.x * mapImage.ScaledWidth);
                        float y = mapY + (point.y * mapImage.ScaledHeight);
                        cb.Circle(x, y, 4f);
                        cb.Fill();
                    }
                }

                if (mapData.sprayingOffPoints.Count > 0)
                {
                    cb.SetColorFill(COLOR_BRAKE);
                    foreach (var point in mapData.sprayingOffPoints)
                    {
                        float x = mapX + (point.x * mapImage.ScaledWidth);
                        float y = mapY + (point.y * mapImage.ScaledHeight);
                        cb.Circle(x, y, 4f);
                        cb.Fill();
                    }
                }

                if (mapData.plantingActivePoints.Count > 0)
                {
                    cb.SetColorFill(COLOR_THROTTLE);
                    foreach (var point in mapData.plantingActivePoints)
                    {
                        float x = mapX + (point.x * mapImage.ScaledWidth);
                        float y = mapY + (point.y * mapImage.ScaledHeight);
                        cb.Circle(x, y, 4f);
                        cb.Fill();
                    }
                }

                if (mapData.plantingOffPoints.Count > 0)
                {
                    cb.SetColorFill(COLOR_BRAKE);
                    foreach (var point in mapData.plantingOffPoints)
                    {
                        float x = mapX + (point.x * mapImage.ScaledWidth);
                        float y = mapY + (point.y * mapImage.ScaledHeight);
                        cb.Circle(x, y, 4f);
                        cb.Fill();
                    }
                }


                if (mapData.trajectoryPoints.Count > 0)
                {
                    Vector2 startPoint = mapData.trajectoryPoints[0];
                    float startX = mapX + (startPoint.x * mapImage.ScaledWidth);
                    float startY = mapY + (startPoint.y * mapImage.ScaledHeight);

                    cb.SetColorFill(COLOR_JD_GREEN);
                    cb.Circle(startX, startY, 6f);
                    cb.Fill();

                    cb.BeginText();
                    cb.SetFontAndSize(BaseFont.CreateFont(), 8);
                    cb.SetColorFill(COLOR_JD_BLACK);
                    cb.SetTextMatrix(startX - 15, startY + 10);
                    cb.ShowText("INÍCIO");
                    cb.EndText();
                }

                if (mapData.trajectoryPoints.Count > 0)
                {
                    Vector2 endPoint = mapData.trajectoryPoints[mapData.trajectoryPoints.Count - 1];
                    float endX = mapX + (endPoint.x * mapImage.ScaledWidth);
                    float endY = mapY + (endPoint.y * mapImage.ScaledHeight);

                    cb.SetColorFill(COLOR_BRAKE);
                    cb.Circle(endX, endY, 6f);
                    cb.Fill();

                    cb.BeginText();
                    cb.SetFontAndSize(BaseFont.CreateFont(), 8);
                    cb.SetColorFill(COLOR_JD_BLACK);
                    cb.SetTextMatrix(endX - 10, endY + 10);
                    cb.ShowText("FIM");
                    cb.EndText();
                }
            }
            catch (Exception e)
            {
                document.Add(new Paragraph($"Erro ao gerar mapa: {e.Message}", fontNormal));
            }
        }

        Paragraph legend = new Paragraph();

        legend.Add(new Chunk("Trajetória percorrida representada em verde.\n", fontSmall));

        RDSceneManager.Operations currentOp = RDSceneManager.Operation;

        if (currentOp == RDSceneManager.Operations.Harvesting ||
            currentOp == RDSceneManager.Operations.HarvertingCorn)
        {
            legend.Add(new Chunk("• Ponto verde maior: Início da operação\n", fontSmall));
            legend.Add(new Chunk("• Ponto vermelho maior: Fim da operação\n", fontSmall));
            legend.Add(new Chunk("• Ponto azul: Cano aberto\n", fontSmall));
            legend.Add(new Chunk("• Ponto amarelo: Ceifadora ligada\n", fontSmall));
            legend.Add(new Chunk("• Ponto vermelho: Ceifadora desligada\n", fontSmall));
        }
        else if (currentOp == RDSceneManager.Operations.Spraying)
        {
            legend.Add(new Chunk("• Ponto verde maior: Início da operação\n", fontSmall));
            legend.Add(new Chunk("• Ponto azul: Braços abertos\n", fontSmall));
            legend.Add(new Chunk("• Ponto amarelo: Pulverizando\n", fontSmall));
            legend.Add(new Chunk("• Ponto vermelho menor: Pulverização desligada\n", fontSmall));
            legend.Add(new Chunk("• Ponto vermelho maior: Fim da operação\n", fontSmall));
        }
        else if (currentOp == RDSceneManager.Operations.Planting)
        {
            legend.Add(new Chunk("• Ponto verde maior: Início da operação\n", fontSmall));
            legend.Add(new Chunk("• Ponto amarelo: Plantio ativado\n", fontSmall));
            legend.Add(new Chunk("• Ponto vermelho: Plantio desativado\n", fontSmall));

        }
        else if (currentOp == RDSceneManager.Operations.Tractor)
        {  
            legend.Add(new Chunk("• Ponto verde maior: Início da operação\n", fontSmall));
            legend.Add(new Chunk("• Ponto vermelho maior: Fim da operação\n", fontSmall));
        }

        legend.SpacingBefore = 10;
        legend.Alignment = Element.ALIGN_CENTER;
        document.Add(legend);
    }

    public void OpenLastGeneratedPDF()
    {
        if (!string.IsNullOrEmpty(lastGeneratedPdfPath) && File.Exists(lastGeneratedPdfPath))
        {
            LessonPdfViewer.Open(lastGeneratedPdfPath);
        }
        else
        {
            Debug.LogWarning("O PDF não foi encontrado ou ainda não foi gerado.");
        }
    }

    private MapTrajectoryData GetMapTrajectoryData()
    {
        var trajectoryData = new MapTrajectoryData();

        string sceneName = SceneManager.GetActiveScene().name;
        trajectoryData.mapName = sceneName == "Farm1" ? "Mapa Farm 1" : "Mapa Farm 2";

        if (sceneName == "Farm1")
        {
            trajectoryData.mapSize = new Vector2(233f, 501f);
        }
        else
        {
            trajectoryData.mapSize = new Vector2(647f, 647f);
        }

        var mapPainter = FindObjectOfType<RDMapPainter>();
        if (mapPainter != null)
        {
            if (sceneName == "Farm1" && mapPainter.texture1 != null)
            {
                trajectoryData.mapTexture = mapPainter.texture1 as Texture2D;
            }
            else if (sceneName == "Farm2" && mapPainter.texture2 != null)
            {
                trajectoryData.mapTexture = mapPainter.texture2 as Texture2D;
            }
        }

        var minimapController = FindObjectOfType<MinimapController>();

        if (minimapController == null)
        {
            Debug.LogError("[MapTrajectory] MinimapController não encontrado!");
            return trajectoryData;
        }

        var telemetryData = TelemetryDataCollector.Instance.TelemetryHistory;
        if (telemetryData != null && telemetryData.Count > 0)
        {
            int startIndex = 0;
            RDSceneManager.Operations currentOp = RDSceneManager.Operation;

            for (int i = 0; i < telemetryData.Count; i++)
            {
                bool isOperating = false;

                switch (currentOp)
                {
                    case RDSceneManager.Operations.Harvesting:
                    case RDSceneManager.Operations.HarvertingCorn:
                    case RDSceneManager.Operations.Tractor:
                        isOperating = telemetryData[i].EngineRpm > 0;
                        break;
                    case RDSceneManager.Operations.Spraying:
                        isOperating = telemetryData[i].Throttle > 0.1f;
                        break;
                    case RDSceneManager.Operations.Planting:
                        isOperating = telemetryData[i].Throttle > 0.1f;
                        break;
                    default:
                        isOperating = true;
                        break;
                }

                if (isOperating)
                {
                    startIndex = i;
                    break;
                }
            }

            bool lastPipeState = false;
            bool lastCutterState = false;
            bool lastArmsOpenedState = false;
            bool lastSprayingState = false;
            bool lastPlantingState = false;

            bool isHarvesting = currentOp == RDSceneManager.Operations.Harvesting ||
                               currentOp == RDSceneManager.Operations.HarvertingCorn;
            bool isSpraying = currentOp == RDSceneManager.Operations.Spraying;
            bool isPlanting = currentOp == RDSceneManager.Operations.Planting;
            bool isTractor = currentOp == RDSceneManager.Operations.Tractor;

            int step = Mathf.Max(1, (telemetryData.Count - startIndex) / 600);

            for (int i = startIndex; i < telemetryData.Count; i += step)
            {
                var data = telemetryData[i];
                Vector3 worldPos = new Vector3(data.PositionX, 0, data.PositionZ);
                Vector2 mapPos = minimapController.GetNormalizedMapPosition(worldPos);

                trajectoryData.trajectoryPoints.Add(mapPos);

                if (isHarvesting)
                {
                    if (data.PipeOpened != lastPipeState)
                    {
                        if (data.PipeOpened)
                        {
                            trajectoryData.pipeOpenPoints.Add(mapPos);
                        }
                        lastPipeState = data.PipeOpened;
                    }

                    bool cutterIsOn = data.CurrentRotationSpeed > 1f;
                    if (cutterIsOn != lastCutterState)
                    {
                        if (cutterIsOn)
                        {
                            trajectoryData.cutterOnPoints.Add(mapPos);
                        }
                        else
                        {
                            trajectoryData.cutterOffPoints.Add(mapPos);
                        }
                        lastCutterState = cutterIsOn;
                    }
                }

                if (isSpraying)
                {
                    if (data.ArmsOpened && !lastArmsOpenedState)
                        {
                            trajectoryData.armsOpenPoints.Add(mapPos);
                        }

                    if (!data.ArmsOpened && lastArmsOpenedState)
                    {
                        // Se quiser adicionar uma lista para braços fechados
                        // trajectoryData.armsClosedPoints.Add(mapPos);
                    }

                        lastArmsOpenedState = data.ArmsOpened;

                    if (data.IsSpraying != lastSprayingState)
                    {
                        if (data.IsSpraying)
                        {
                            trajectoryData.sprayingOnPoints.Add(mapPos);
                        }
                        else
                        {
                            trajectoryData.sprayingOffPoints.Add(mapPos);
                        }
                        lastSprayingState = data.IsSpraying;
                    }
                }
                if (isPlanting)
                {
                    if (data.IsPlanting != lastPlantingState)
                    {
                        if (data.IsPlanting)
                        {
                            trajectoryData.plantingActivePoints.Add(mapPos);
                        }
                        else
                        {
                            trajectoryData.plantingOffPoints.Add(mapPos);
                        }
                        lastPlantingState = data.IsPlanting;
                    }
                }
                if (isTractor){}
            }
        }

        return trajectoryData;
    }

    private float CalcularDistanciaPercorrida(List<Vector2> pontos, string sceneName)
    {
        if (pontos == null || pontos.Count < 2) return 0;

        float terrainWidth = sceneName == "Farm1" ? 233f : 647f;
        float terrainLength = sceneName == "Farm1" ? 501f : 647f;

        float distanciaTotal = 0;
        for (int i = 0; i < pontos.Count - 1; i++)
        {
            float worldX1, worldZ1, worldX2, worldZ2;

            if (sceneName == "Farm1")
            {
                worldX1 = (1f - pontos[i].x) * terrainWidth;
                worldZ1 = (1f - pontos[i].y) * terrainLength;
                worldX2 = (1f - pontos[i + 1].x) * terrainWidth;
                worldZ2 = (1f - pontos[i + 1].y) * terrainLength;
            }
            else
            {
                worldX1 = (1f - pontos[i].x) * terrainWidth;
                worldZ1 = (1f - pontos[i].y) * terrainLength;
                worldX2 = (1f - pontos[i + 1].x) * terrainWidth;
                worldZ2 = (1f - pontos[i + 1].y) * terrainLength;
            }

            float deltaX = worldX2 - worldX1;
            float deltaZ = worldZ2 - worldZ1;

            distanciaTotal += Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        }

        return distanciaTotal;
    }
    private void AddMetricCard(PdfPTable table, string title, string value, BaseColor color)
    {
        PdfPCell cell = new PdfPCell();
        cell.Border = Rectangle.BOX;
        cell.BorderColor = color;
        cell.BorderWidth = 1.5f;
        cell.Padding = 15;
        cell.HorizontalAlignment = Element.ALIGN_CENTER;

        Paragraph titleP = new Paragraph(title, fontSmall);
        titleP.SpacingAfter = 5;
        cell.AddElement(titleP);

        Paragraph valueP = new Paragraph(value, fontTitle);
        valueP.SpacingAfter = 5;
        cell.AddElement(valueP);

        table.AddCell(cell);
    }
    private void CreateDualChart(PdfContentByte cb, Document document,
        List<TelemetryDataPoint> data, string title,
        string[] yLabels,
        Func<TelemetryDataPoint, float>[] valueSelectors,
        BaseColor[] colors, float[] maxValues,
        float y, float chartHeight, float totalLessonSeconds)
    {
        float chartWidth = document.PageSize.Width - document.LeftMargin - document.RightMargin - 60;
        float left = document.LeftMargin + 30;
        float bottom = y - chartHeight;
        float top = y;

        var sampledData = GetSampledData(data, 80);
        float minTime = sampledData[0].Timestamp;
        float maxTime = sampledData[sampledData.Count - 1].Timestamp;
        float timeRange = maxTime - minTime;
        if (timeRange <= 0) timeRange = 1;

        cb.BeginText();
        cb.SetFontAndSize(BaseFont.CreateFont(), 10);
        cb.SetColorFill(COLOR_JD_BLACK);
        cb.SetTextMatrix(left, top - 23);
        cb.ShowText(title);
        cb.EndText();

        DrawProfessionalGrid(cb, left, bottom, chartWidth, chartHeight - 30, maxValues);

        for (int s = 0; s < valueSelectors.Length; s++)
        {
            cb.SetColorStroke(colors[s]);
            cb.SetLineWidth(1.5f);

            for (int i = 0; i < sampledData.Count - 1; i++)
            {
                float t1 = (sampledData[i].Timestamp - minTime) / timeRange;
                float t2 = (sampledData[i + 1].Timestamp - minTime) / timeRange;

                float x1 = left + t1 * chartWidth;
                float x2 = left + t2 * chartWidth;

                float y1 = bottom + (valueSelectors[s](sampledData[i]) / maxValues[s]) * (chartHeight - 30);
                float y2 = bottom + (valueSelectors[s](sampledData[i + 1]) / maxValues[s]) * (chartHeight - 30);

                y1 = Math.Max(bottom, Math.Min(y1, bottom + chartHeight - 30));
                y2 = Math.Max(bottom, Math.Min(y2, bottom + chartHeight - 30));

                cb.MoveTo(x1, y1);
                cb.LineTo(x2, y2);
                cb.Stroke();
            }
        }

        float timeY = bottom - 5 ; 

        cb.BeginText();
        cb.SetFontAndSize(BaseFont.CreateFont(), 7);
        cb.SetColorFill(COLOR_JD_GRAY);
        cb.SetTextMatrix(left + chartWidth / 2 - 25, timeY -2);
        cb.ShowText("Tempo (min)");
        cb.EndText();

        int numMarkers = 8;
        for (int i = 0; i < numMarkers; i++)
        {
            float t = i / (float)(numMarkers - 1);
            float x = left + t * chartWidth;


            cb.SetColorStroke(COLOR_JD_GRAY);
            cb.SetLineWidth(0.5f);
            cb.MoveTo(x, bottom);
            cb.LineTo(x, bottom - 6); 
            cb.Stroke();

            float timeInSeconds = t * totalLessonSeconds;
            string timeText = FormatTimeMMSS(timeInSeconds);

            cb.BeginText();
            cb.SetFontAndSize(BaseFont.CreateFont(), 7);
            cb.SetColorFill(COLOR_JD_GRAY);
            cb.SetTextMatrix(x - 6, timeY - 7);
            cb.ShowText(timeText);
            cb.EndText();
        }

        float legendSpacing = 100;
        float startLegendX = left + (chartWidth / 2) - ((valueSelectors.Length - 1) * legendSpacing / 2);
        float legendY = bottom - 28; 

        for (int s = 0; s < valueSelectors.Length; s++)
        {
            float legendX = startLegendX + (s * legendSpacing);

            cb.SetColorFill(colors[s]);
            cb.Rectangle(legendX, legendY, 12, 12);
            cb.Fill();

            cb.BeginText();
            cb.SetFontAndSize(BaseFont.CreateFont(), 8);
            cb.SetColorFill(COLOR_JD_BLACK);
            cb.SetTextMatrix(legendX + 17, legendY + 2);
            cb.ShowText($"{yLabels[s]} (max: {maxValues[s]:F0})");
            cb.EndText();
        }
    }

    private string FormatTimeMMSS(float seconds)
    {
        int totalSeconds = Mathf.RoundToInt(seconds);
        int minutes = totalSeconds / 60;
        int secs = totalSeconds % 60;
        return $"{minutes}:{secs:D2}";
    }

    private void DrawProfessionalGrid(PdfContentByte cb, float left, float bottom,
        float width, float height, float[] maxValues)
    {
        cb.SetColorStroke(COLOR_JD_LIGHTGRAY);
        cb.SetLineWidth(0.3f);

        for (int i = 0; i <= 5; i++)
        {
            float y = bottom + (i * height / 5);
            cb.MoveTo(left, y);
            cb.LineTo(left + width, y);
            cb.Stroke();
        }

        cb.SetColorStroke(COLOR_JD_BLACK);
        cb.SetLineWidth(1f);
        cb.Rectangle(left, bottom, width, height);
        cb.Stroke();

        for (int i = 0; i <= 5; i++)
        {
            float y = bottom + (i * height / 5);
            float value = (i / 5f) * maxValues[0];
            cb.BeginText();
            cb.SetFontAndSize(BaseFont.CreateFont(), 6);
            cb.SetColorFill(COLOR_JD_GRAY);
            cb.SetTextMatrix(left - 25, y - 3);
            cb.ShowText($"{value:F0}");
            cb.EndText();
        }

        if (maxValues.Length > 1)
        {
            for (int i = 0; i <= 5; i++)
            {
                float y = bottom + (i * height / 5);
                float value = (i / 5f) * maxValues[1];
                cb.BeginText();
                cb.SetFontAndSize(BaseFont.CreateFont(), 6);
                cb.SetColorFill(COLOR_JD_GRAY);
                cb.SetTextMatrix(left + width + 5, y - 3);
                cb.ShowText($"{value:F0}");
                cb.EndText();
            }
        }
    }
    private void CreateStatsLine(Document document, AdvancedStats stats)
    {
        if (stats == null) return;

        bool isHarvesting = RDSceneManager.Operation == RDSceneManager.Operations.Harvesting ||
                            RDSceneManager.Operation == RDSceneManager.Operations.HarvertingCorn;
        bool isSprayer = RDSceneManager.Operation == RDSceneManager.Operations.Spraying;
        bool isPlanting = RDSceneManager.Operation == RDSceneManager.Operations.Planting;

        int columnCount;
        if (isHarvesting)
            columnCount = 8;
        else if (isSprayer)
            columnCount = 7;
        else if (isPlanting)
            columnCount = 7;
        else
            columnCount = 6;


        PdfPTable lineTable = new PdfPTable(columnCount);
        lineTable.WidthPercentage = 100;
        lineTable.SpacingAfter = 10;

        if (isHarvesting)
        {
            float grainsInBucket = stats.GrainsInTheBucket;
            float grainsLost = stats.GrainsLostInTheGround;
            float totalGrains = grainsInBucket + grainsLost;
            float eficiencia = totalGrains > 0 ? (grainsInBucket / totalGrains) * 100 : 0;

            BaseColor eficienciaColor = eficiencia >= 95 ? COLOR_JD_GREEN :
                                        (eficiencia >= 85 ? COLOR_JD_YELLOW : COLOR_BRAKE);

            AddStatCell(lineTable, "Vel. Média", $"{stats.AvgSpeed:F1} km/h");
            AddStatCell(lineTable, "Vel. Máx", $"{stats.MaxSpeed:F1} km/h");
            AddStatCell(lineTable, "RPM Médio", $"{stats.AvgRpm:F0}");
            AddStatCell(lineTable, "RPM Máx", $"{stats.MaxRpm:F0}");
            AddStatCell(lineTable, "Grãos no caminhão", $"{grainsInBucket:F0}");
            AddStatCell(lineTable, "Perda de Grãos", $"{grainsLost:F0}");
            AddStatCell(lineTable, "Acel. Médio", $"{stats.AvgThrottle * 100:F0}%");
            AddStatCell(lineTable, "Eficiência", $"{eficiencia:F1}%", eficienciaColor);
        }

        else if (isSprayer)
        {
            float avgSpeed = stats.AvgSpeed;
            float speedIdeal = 12f;

            float eficiencia;

            if (avgSpeed <= 0)
            {
                eficiencia = 0;
            }
            else if (avgSpeed <= speedIdeal)
            {
                eficiencia = (avgSpeed / speedIdeal) * 100;
            }
            else
            {
                eficiencia = (speedIdeal / avgSpeed) * 100;
            }

            eficiencia = Mathf.Clamp(eficiencia, 0, 100);

            BaseColor eficienciaColor = eficiencia >= 90 ? COLOR_JD_GREEN :
                                        (eficiencia >= 70 ? COLOR_JD_YELLOW : COLOR_BRAKE);

            AddStatCell(lineTable, "Vel. Média", $"{stats.AvgSpeed:F1} km/h");
            AddStatCell(lineTable, "Vel. Máx", $"{stats.MaxSpeed:F1} km/h");
            AddStatCell(lineTable, "RPM Médio", $"{stats.AvgRpm:F0}");
            AddStatCell(lineTable, "RPM Máx", $"{stats.MaxRpm:F0}");
            AddStatCell(lineTable, "Acel. Médio", $"{stats.AvgThrottle * 100:F0}%");
            AddStatCell(lineTable, "Freio Médio", $"{stats.AvgBrake * 100:F0}%");
            AddStatCell(lineTable, "Eficiência", $"{eficiencia:F1}%", eficienciaColor);
        }

        else if (isPlanting)
        {
            float avgSpeed = stats.AvgSpeed;
            float speedIdeal = 6f;

            float eficiencia;

            if (avgSpeed <= 0)
            {
                eficiencia = 0;
            }
            else if (avgSpeed <= speedIdeal)
            {
                eficiencia = (avgSpeed / speedIdeal) * 100;
            }
            else
            {
                eficiencia = (speedIdeal / avgSpeed) * 100;     
            }

            eficiencia = Mathf.Clamp(eficiencia, 0, 100);

            BaseColor eficienciaColor = eficiencia >= 90 ? COLOR_JD_GREEN :
                                        (eficiencia >= 70 ? COLOR_JD_YELLOW : COLOR_BRAKE);

            AddStatCell(lineTable, "Vel. Média", $"{stats.AvgSpeed:F1} km/h");
            AddStatCell(lineTable, "Vel. Máx", $"{stats.MaxSpeed:F1} km/h");
            AddStatCell(lineTable, "RPM Médio", $"{stats.AvgRpm:F0}");
            AddStatCell(lineTable, "RPM Máx", $"{stats.MaxRpm:F0}");
            AddStatCell(lineTable, "Acel. Médio", $"{stats.AvgThrottle * 100:F0}%");
            AddStatCell(lineTable, "Freio Médio", $"{stats.AvgBrake * 100:F0}%");
            AddStatCell(lineTable, "Eficiência", $"{eficiencia:F1}%", eficienciaColor);
        }

        else
        {
            AddStatCell(lineTable, "Vel. Média", $"{stats.AvgSpeed:F1} km/h");
            AddStatCell(lineTable, "Vel. Máx", $"{stats.MaxSpeed:F1} km/h");
            AddStatCell(lineTable, "RPM Médio", $"{stats.AvgRpm:F0}");
            AddStatCell(lineTable, "RPM Máx", $"{stats.MaxRpm:F0}");
            AddStatCell(lineTable, "Acel. Médio", $"{stats.AvgThrottle * 100:F0}%");
            AddStatCell(lineTable, "Freio Médio", $"{stats.AvgBrake * 100:F0}%");
        }

        document.Add(lineTable);
    }

    private void AddStatCell(PdfPTable table, string label, string value, BaseColor bgColor = null)
    {
        PdfPCell cell = new PdfPCell();
        cell.Border = Rectangle.NO_BORDER;
        cell.Padding = 3;

        Paragraph labelP = new Paragraph(label, fontChartLabel);
        labelP.Alignment = Element.ALIGN_CENTER;
        cell.AddElement(labelP);

        Paragraph valueP = new Paragraph(value, fontSmallBold);
        valueP.Alignment = Element.ALIGN_CENTER;
        cell.AddElement(valueP);

        if (bgColor != null)
        {
            cell.BackgroundColor = bgColor;
        }

        table.AddCell(cell);
    }

    private class AdvancedStats
    {
        public float MaxSpeed { get; set; }
        public float AvgSpeed { get; set; }
        public float MinSpeed { get; set; }
        public float MaxRpm { get; set; }
        public float AvgRpm { get; set; }
        public float MinRpm { get; set; }
        public float AvgThrottle { get; set; }
        public float AvgBrake { get; set; }
        public float ThrottleTime { get; set; }
        public float BrakeTime { get; set; }
        public float GrainsInTheBucket { get; set; }
        public float GrainsLostInTheGround { get; set; } 
    }

    private AdvancedStats CalculateAdvancedStats(List<TelemetryDataPoint> data)
    {
        var stats = new AdvancedStats();

        if (data == null || data.Count == 0)
        {
            return stats;
        }


        stats.MaxSpeed = data.Max(d => d.Speed);
        stats.MinSpeed = data.Min(d => d.Speed);
        stats.AvgSpeed = (float)data.Average(d => d.Speed);

        stats.MaxRpm = data.Max(d => d.EngineRpm);
        stats.MinRpm = data.Min(d => d.EngineRpm);
        stats.AvgRpm = (float)data.Average(d => d.EngineRpm);

        stats.AvgThrottle = (float)data.Average(d => d.Throttle);
        stats.AvgBrake = (float)data.Average(d => d.Brake);

        float timeStep = 0.5f;
        stats.ThrottleTime = data.Count(d => d.Throttle > 0.1f) * timeStep;
        stats.BrakeTime = data.Count(d => d.Brake > 0.1f) * timeStep;

        if (data.Count > 0)
        {
            var lastData = data.Last();
            stats.GrainsInTheBucket = lastData.GrainsInTheBucket;
            stats.GrainsLostInTheGround = lastData.GrainsLostInTheGround;

            var firstData = data.First();
        }

        return stats;
    }

    #region Helper Methods

    private string GetTrainingText()
    {
        switch (RDSceneManager.Operation)
        {
            case RDSceneManager.Operations.Planting: return "Plantio";
            case RDSceneManager.Operations.Tractor: return "Trator";
            case RDSceneManager.Operations.Spraying: return "Pulverização";
            case RDSceneManager.Operations.Harvesting:
            case RDSceneManager.Operations.HarvertingCorn: return "Colheita";
            default: return "Treinamento";
        }
    }

    private List<TelemetryDataPoint> GetSampledData(List<TelemetryDataPoint> data, int maxPoints)
    {
        if (data.Count <= maxPoints) return data;

        List<TelemetryDataPoint> sampled = new List<TelemetryDataPoint>();
        float step = (float)data.Count / maxPoints;

        for (int i = 0; i < maxPoints; i++)
        {
            int index = (int)Math.Floor(i * step);
            index = Math.Min(index, data.Count - 1);
            sampled.Add(data[index]);
        }

        return sampled;
    }

    #endregion

    #region Existing Classes

    public class RodapePDF : PdfPageEventHelper
    {
        private int pageCount = 0;
        private iTextFont font;

        public RodapePDF(iTextFont font)
        {
            this.font = font;
        }

        public override void OnEndPage(PdfWriter writer, Document document)
        {
            pageCount++;

            PdfPTable footer = new PdfPTable(2);
            footer.TotalWidth = document.PageSize.Width - document.LeftMargin - document.RightMargin;

            PdfPCell cell1 = new PdfPCell(new Phrase("Treinamento Profissional", font));
            cell1.Border = Rectangle.NO_BORDER;
            cell1.HorizontalAlignment = Element.ALIGN_LEFT;
            footer.AddCell(cell1);

            PdfPCell cell2 = new PdfPCell(new Phrase($"Página {pageCount}", font));
            cell2.Border = Rectangle.NO_BORDER;
            cell2.HorizontalAlignment = Element.ALIGN_RIGHT;
            footer.AddCell(cell2);

            footer.WriteSelectedRows(0, -1, document.LeftMargin, document.BottomMargin - 10, writer.DirectContent);
        }
    }

    private string GetTaskSpecificAdvice(RDSceneManager.Operations op, int taskIndex)
    {
        if (op == RDSceneManager.Operations.Harvesting || op == RDSceneManager.Operations.HarvertingCorn)
        {
            switch (taskIndex)
            {
                case 0: return "Certifique-se de que todos os sistemas de segurança estão checados antes de dar a partida no motor.";
                case 1: return "Atenção à altura da plataforma: manter a altura correta evita perdas de grãos e danos ao equipamento.";
                case 2: return "Mantenha o alinhamento constante durante a colheita para garantir que toda a área seja processada sem falhas.";
                case 3: return "Sempre desligue o molinete e os sistemas de trilha ao finalizar a faixa de colheita para economizar combustível e evitar desgaste.";
                case 4: return "Durante a descarga, certifique-se de que o cano está posicionado corretamente sobre o caminhão para evitar desperdício.";
                case 5: return "Lembre-se sempre de fechar o tubo de descarga antes de se deslocar ou guardar a máquina para evitar colisões.";
            }
        }
        else if (op == RDSceneManager.Operations.Planting)
        {
            switch (taskIndex)
            {
                case 0: return "Verifique o nível de sementes e fertilizantes no reservatório antes de iniciar a operação.";
                case 1: return "O implemento deve ser levantado totalmente antes de realizar manobras ou acelerar o trator para evitar danos mecânicos.";
                case 2: return "Mantenha a velocidade constante durante o plantio para garantir a profundidade e distribuição uniforme das sementes.";
                case 3: return "Ao finalizar o talhão, levante o implemento suavemente para não arrastar as linhas de plantio no solo.";
                case 4: return "Sempre verifique se há obstruções nos tubos condutores de sementes ao final de cada jornada.";
                case 5: return "Estacione o conjunto em local plano e alivie a pressão hidráulica do implemento ao guardar.";
            }
        }
        else if (op == RDSceneManager.Operations.Tractor)
        {
            switch (taskIndex)
            {
                case 0: return "A partida correta do motor garante a lubrificação ideal e aumenta a vida útil dos componentes internos.";
                case 1: return "Mantenha o foco na trajetória e utilize as balizas como referência para aprimorar sua percepção espacial com o implemento.";
                case 2: return "Utilize a marcha correta para a carga transportada, evitando sobrecarga no motor e consumo excessivo.";
                case 3: return "Ao realizar manobras em espaços reduzidos, utilize os espelhos e mantenha uma velocidade reduzida de segurança.";
                case 4: return "Verifique sempre o engate do implemento e as conexões hidráulicas antes de iniciar qualquer deslocamento longo.";
                case 5: return "Ao estacionar, certifique-se de que o freio de mão está acionado e o implemento está devidamente apoiado no solo.";
            }
        }
        else if (op == RDSceneManager.Operations.Spraying)
        {
            switch (taskIndex)
            {
                case 0: return "Verifique a pressão do sistema e a integridade dos bicos antes de iniciar a aplicação do defensivo.";
                case 1: return "As barras de pulverização devem ser abertas apenas em áreas amplas e livres de obstáculos para evitar quebras.";
                case 2: return "Monitore a velocidade do vento e a pressão de trabalho para evitar a deriva do produto para fora do alvo.";
                case 3: return "Sempre desligue a bomba de pulverização ao realizar manobras de cabeceira para evitar sobreposição de produto.";
                case 4: return "Mantenha a altura das barras estável em relação à cultura para garantir a cobertura ideal das gotas.";
                case 5: return "Após a operação, realize a limpeza do sistema para evitar a cristalização de resíduos nos bicos e filtros.";
            }
        }
        return "";
    }

    public class AdicionarAssinaturas
    {
        public void Assinaturas(Document document, PdfWriter writer, iTextFont font)
        {
            Paragraph title = new Paragraph("ASSINATURAS", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14));
            title.Alignment = Element.ALIGN_CENTER;
            title.SpacingAfter = 30;
            document.Add(title);

            PdfPTable assinaturaTable = new PdfPTable(3);
            assinaturaTable.TotalWidth = document.PageSize.Width - document.LeftMargin - document.RightMargin;
            assinaturaTable.SpacingBefore = 20;

            for (int i = 0; i < 3; i++)
            {
                PdfPCell cell = new PdfPCell(new Phrase("______________________", font));
                cell.Border = Rectangle.NO_BORDER;
                cell.HorizontalAlignment = Element.ALIGN_CENTER;
                cell.PaddingBottom = 5;
                assinaturaTable.AddCell(cell);
            }

            AddSignatureRow(assinaturaTable, "Assinatura do Aluno", font);
            AddSignatureRow(assinaturaTable, "Assinatura do Instrutor", font);
            AddSignatureRow(assinaturaTable, "Assinatura do Responsável", font);

            float x = document.LeftMargin;
            float y = document.BottomMargin + 100;
            assinaturaTable.WriteSelectedRows(0, -1, x, y, writer.DirectContent);
        }

        private void AddSignatureRow(PdfPTable table, string text, iTextFont font)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, font));
            cell.Border = Rectangle.NO_BORDER;
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            table.AddCell(cell);
        }
    }

    private static void AdicionarMarcaDaguaImagem(string caminhoPDF, byte[] imagemBytes)
    {
        string tempPath = caminhoPDF.Replace(".pdf", "_temp.pdf");

        using (PdfReader reader = new PdfReader(caminhoPDF))
        using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
        using (PdfStamper stamper = new PdfStamper(reader, fs))
        {
            // Usa o alias iTextImage em vez de Image
            iTextImage imagem = iTextImage.GetInstance(imagemBytes);
            imagem.ScaleToFit(400, 400);

            PdfGState gState = new PdfGState { FillOpacity = 0.03f };

            for (int i = 1; i <= reader.NumberOfPages; i++)
            {
                PdfContentByte content = stamper.GetOverContent(i);
                float x = (reader.GetPageSize(i).Width - imagem.ScaledWidth) / 2;
                float y = (reader.GetPageSize(i).Height - imagem.ScaledHeight) / 2;
                imagem.SetAbsolutePosition(x, y);

                content.SaveState();
                content.SetGState(gState);
                content.AddImage(imagem);
                content.RestoreState();
            }
        }

        File.Delete(caminhoPDF);
        File.Move(tempPath, caminhoPDF);
    }

    #endregion
}

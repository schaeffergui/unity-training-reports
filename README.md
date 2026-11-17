# Training Reports for Unity

Training task UI, score calculation, and configurable lesson performance reports for Unity 6 projects. Simulator-specific behavior is supplied by the host through package interfaces; this package contains no simulator SDK or vehicle controller dependency.

## Installation

In Unity, open **Window > Package Manager > + > Add package from git URL...** and use one of these URLs:

- Stable branch: `https://github.com/schaeffergui/unity-training-reports.git#main`
- Development branch: `https://github.com/schaeffergui/unity-training-reports.git#dev`

You can also add the package to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.schaeffergui.training-reports": "https://github.com/schaeffergui/unity-training-reports.git#main"
  }
}
```

## Requirements and optional libraries

- Unity 6 (`6000.0` or newer).
- `com.unity.ugui`, declared as a package dependency. The UI scripts also use Unity's TextMeshPro assembly.
- No simulator SDK, Rewired, or Vehicle Physics Pro dependency is required by the package assemblies.
- The in-game PDF viewer is optional. Add `Docnet.Core.dll` to the host project and define `TRAINING_REPORTS_PDF` to compile `TrainingReports.Pdf`.
- PDF export is optional. Add the iTextSharp 5.x assembly as `itextsharp.dll` and define `TRAINING_REPORTS_PDF_EXPORT` to compile `TrainingReports.PdfExport`. iTextSharp is not included in this package; review its LGPL/AGPL licensing terms before distributing it.

The PDF viewer and exporter are disabled when their scripting symbols are absent, so projects can use the Core, Reports, and Training assemblies without those optional libraries.

## Package architecture

```mermaid
flowchart TD
    Core[TrainingReports.Core<br/>Pure data and host interfaces]
    Reports[TrainingReports.Reports<br/>Report UI and context asset]
    Training[TrainingReports.Training<br/>Task UI and lesson orchestration]
    Viewer[TrainingReports.Pdf<br/>Optional viewer, Docnet]
    Export[TrainingReports.PdfExport<br/>Optional report export, iTextSharp]
    Tests[TrainingReports.Core.Tests<br/>EditMode tests]
    Sample[TrainingReports.SimulatorAdapter<br/>Imported sample]
    Core --> Reports
    Core --> Training
    Reports --> Training
    Core --> Viewer
    Core --> Export
    Core --> Tests
    Reports --> Tests
    Core --> Sample
    Training --> Sample
```

## Connect a host project

`TrainingManager` accepts a host component implementing `ITrainingContext`, `ITrainingOperationRunner`, and the relevant vehicle, theme, metrics, and report interfaces. The runner receives an `ITrainingTaskSink` and `IVehicleProbe`; it owns the host's operation-specific checks and reports task results through the sink. `TrainingTaskUI` renders those results with the supplied `ITrainingTheme`.

Import **Samples > Training Reports > Simulator Adapter** in Package Manager for a plain C# example. It demonstrates the interface wiring with sample data; replace its illustrative lesson checks and report presenter with the host's real operation logic and reporting integration.

## Tests

The package includes EditMode tests for report inputs, observation thresholds, telemetry statistics, and score calculation. The latest Unity 6 consumer-project run completed **25/25 EditMode tests**.

## Verification status

- Unity `6000.2.1f1` compiled the package in the consumer project with both `TRAINING_REPORTS_PDF` and `TRAINING_REPORTS_PDF_EXPORT` enabled. The Console reported no compile errors after the exporter fixes.
- All **25/25 EditMode tests passed** with both optional symbols enabled.
- The exporter generated a four-page sample PDF in the consumer project's ignored `Temp/TrainingReportsValidation` folder.
- The viewer assembly compiled, but opening/rendering the generated PDF failed during Docnet initialization with `DllNotFoundException`: the native PDFium library (`pdfium.dll`) is missing from the consumer project. The `Assets/Plugins/PDF` folder contains the three managed DLLs only.
- The package does not include the `Resources/UI/LessonPdfViewer` prefab required by `LessonPdfViewer.Open`, so the in-game viewer UI was not opened or visually checked.
- The sample adapter has not been exercised in a real simulator. No `MinimalLesson` scene is included yet.
- The consumer's Scripting Define Symbols were changed locally for this validation only; the three third-party DLLs remain in the separate consumer project and are not included in the package repository.

# Training Reports (Unity package)

Lessons, scoring and **PDF performance reports** for agricultural machine simulators in Unity (C#).
Extracted from a professional training simulator; company names and branding were removed.

## Installation

1. In Unity: **Window > Package Manager > + > Add package from git URL...**
2. Paste:

```
https://github.com/schaeffergui/unity-training-reports.git
```

For the development version, append `#dev`:
`https://github.com/schaeffergui/unity-training-reports.git#dev`

Or add it to `Packages/manifest.json`:

```json
"com.schaeffergui.training-reports": "https://github.com/schaeffergui/unity-training-reports.git"
```

## Requirements
- Unity 6 (6000.0 or newer) and `com.unity.ugui` (installed automatically).
- Install in your project: [Rewired](https://assetstore.unity.com/packages/tools/utilities/rewired-21676), Vehicle Physics Pro, iTextSharp and Docnet (third-party, not included).
- The code uses simulator classes (`RDSceneManager`, `RDStudent`, machine controllers). The package has no `asmdef`, so it compiles together with your project and finds those classes. Decoupling them through interfaces is on the roadmap.

## What the package does

| Part | Files | Description |
|---|---|---|
| Lessons and tasks | `Runtime/Training/TrainingManager.cs` | Tractor, planting, spraying and harvesting lessons: sequenced tasks, scoring and performance tracking |
| Task panel | `Runtime/Training/TrainingTaskUI.cs` | In-lesson task interface |
| PDF report | `Runtime/Reports/LessonPdfExporter.cs` | PDF with grades, speed and RPM charts, route map, observations and recommendations, grain loss and efficiency |
| End-of-lesson screen | `Runtime/Reports/LessonReportUI.cs`, `LessonReportData.cs` | Summary shown when the lesson ends |
| PDF viewer | `Runtime/UI/LessonPdfViewer.cs`, `Runtime/Reports/LessonPdfDocument.cs` | Opens the PDF in-game: asynchronous rendering with cancellation, path validation and safe copy |
| Tests | `Tests/`, `Editor/` | NUnit tests for implement-footprint scoring and PDF viewer checks |

## Technical highlights
- Scoring that penalizes repeated or out-of-zone operation, proportional to the implement footprint.
- Observations generated from the student's mistakes and an estimate of waste caused by rework.
- A PDF viewer that rejects URLs, network paths and invalid or oversized files.

## Notes
- All text is in English. A few UI object lookups in `LessonReportUI.cs` and `TrainingManager.cs` still match scene object names such as `Treinamento`, `Erro` and `BraçoL`, because those names live in the simulator prefabs, which are not included.
- Logos and report images are not included.
- No reuse license: all rights reserved to the author unless stated otherwise.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TrainingReports;

[DefaultExecutionOrder(-1000)]
public sealed class LessonPdfViewer : MonoBehaviour
{
    [SerializeField] private Sprite roundedSprite;
    [SerializeField] private TMP_FontAsset font;

    public static ITrainingTheme Theme { get; set; } = DefaultTrainingTheme.Instance;

    private enum FitMode { Page, Width, Manual }
    private static LessonPdfViewer instance;
    private static int closedFrame = -1;
    private readonly List<EventSystem> suspendedSystems = new List<EventSystem>();
    private readonly List<Button> controls = new List<Button>(10);
    private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
    private CancellationTokenSource renderCancellation;
    private LessonPdfDocument document;
    private Texture2D pageTexture;
    private Canvas canvas;
    private RectTransform panel;
    private RectTransform pageRow;
    private RectTransform zoomRow;
    private RectTransform footer;
    private RectTransform content;
    private ScrollRect scroll;
    private RawImage pageImage;
    private TMP_Text fileLabel;
    private TMP_Text pageLabel;
    private TMP_Text zoomLabel;
    private TMP_Text statusLabel;
    private TMP_Text loadingLabel;
    private GameObject loadingPanel;
    private Button previousButton;
    private Button nextButton;
    private Button zoomOutButton;
    private Button zoomInButton;
    private Button fitPageButton;
    private Button fitWidthButton;
    private Button saveButton;
    private Button closeButton;
    private Button retryButton;
    private EventSystem eventSystem;
    private EventSystem previousSystem;
    private GameObject previousSelection;
    private ITrainingTheme theme;
    private FitMode fitMode;
    private int pageIndex;
    private int requestVersion;
    private int navigationDirection;
    private float nextNavigationTime;
    private float renderAt = -1f;
    private float zoom = 1f;
    private bool rendering;
    private bool saving;
    private bool closed;
    private bool oldCursorVisible;
    private CursorLockMode oldCursorLock;
    private Rect lastSafeArea;
    private Vector2 lastResolution;
    private float lastCanvasScale;
    private string sourcePath;

    public static bool IsOpen => instance != null && !instance.closed;
    public static bool BlocksInput => IsOpen || closedFrame == Time.frameCount;

    public static void Open(string path)
    {
        if (IsOpen && string.Equals(instance.sourcePath, path, StringComparison.OrdinalIgnoreCase)) return;
        if (IsOpen) instance.Close();
        var prefab = Resources.Load<LessonPdfViewer>("UI/LessonPdfViewer");
        if (prefab == null)
        {
            Debug.LogError("The PDF viewer was not included in the game.");
            return;
        }
        var viewer = Instantiate(prefab);
        viewer.sourcePath = path;
        viewer.LoadAsync();
    }

    private void Awake()
    {
        theme = Theme ?? DefaultTrainingTheme.Instance;
        instance = this;
        DontDestroyOnLoad(gameObject);
        previousSystem = EventSystem.current;
        previousSelection = previousSystem != null ? previousSystem.currentSelectedGameObject : null;
        oldCursorVisible = Cursor.visible;
        oldCursorLock = Cursor.lockState;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SuspendEventSystems();
        BuildUI();
        SceneManager.sceneLoaded += OnSceneLoaded;
        eventSystem.SetSelectedGameObject(closeButton.gameObject);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SuspendEventSystems();
        EventSystem.current = eventSystem;
    }

    private void SuspendEventSystems()
    {
        var systems = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == eventSystem || !systems[i].enabled) continue;
            suspendedSystems.Add(systems[i]);
            systems[i].enabled = false;
        }
    }

    private async void LoadAsync()
    {
        CancellationToken token = lifetime.Token;
        LessonPdfDocument loaded = null;
        SetLoading("Loading report...");
        retryButton.gameObject.SetActive(false);
        try
        {
            loaded = await Task.Run(() => LessonPdfDocument.Load(sourcePath, token), token);
            if (token.IsCancellationRequested || this == null || closed) return;
            document = loaded;
            loaded = null;
            fileLabel.text = document.FileName;
            Canvas.ForceUpdateCanvases();
            RefreshLayout();
            RequestRender();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested && this != null && !closed) ShowError(exception);
        }
        finally
        {
            loaded?.Dispose();
        }
    }

    private void Update()
    {
        if (closed) return;
        RefreshLayout();
        UpdateInput();
        if (closed) return;
        if (document != null && !rendering && renderAt >= 0f && Time.unscaledTime >= renderAt) RenderAsync();
    }

    private void LateUpdate()
    {
        if (closed) return;
        GameObject selection = eventSystem.currentSelectedGameObject;
        if (selection != null && selection.transform.IsChildOf(transform) && selection.activeInHierarchy) return;
        if (selection != null && !selection.transform.IsChildOf(transform)) previousSelection = selection;
        eventSystem.SetSelectedGameObject(closeButton.gameObject);
    }

    private void UpdateInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }
        bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        scroll.scrollSensitivity = control ? 0f : 48f;
        if (Input.GetKeyDown(KeyCode.Tab)) SelectControl(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);

        float horizontal = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        float vertical = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        int direction = Mathf.Abs(horizontal) > 0.5f ? (horizontal > 0f ? 1 : -1) : Mathf.Abs(vertical) > 0.5f ? (vertical > 0f ? -1 : 1) : 0;
        if (direction != navigationDirection || (direction != 0 && Time.unscaledTime >= nextNavigationTime))
        {
            if (direction != 0) SelectControl(direction);
            nextNavigationTime = Time.unscaledTime + (direction != navigationDirection ? 0.35f : 0.15f);
            navigationDirection = direction;
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
        {
            GameObject selected = eventSystem.currentSelectedGameObject;
            for (int i = 0; i < controls.Count; i++)
            {
                if (controls[i].gameObject != selected || !controls[i].IsInteractable()) continue;
                controls[i].onClick.Invoke();
                break;
            }
        }
        if (closed || document == null) return;

        if (Input.GetKeyDown(KeyCode.PageUp)) ChangePage(-1);
        if (Input.GetKeyDown(KeyCode.PageDown)) ChangePage(1);
        if (Input.GetKeyDown(KeyCode.Home)) ChangePage(-pageIndex);
        if (Input.GetKeyDown(KeyCode.End)) ChangePage(document.PageSizes.Length - pageIndex - 1);
        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus)) ChangeZoom(1);
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) ChangeZoom(-1);
        if (control && Input.mouseScrollDelta.y != 0f) ChangeZoom(Input.mouseScrollDelta.y > 0f ? 1 : -1);
        if (control && Input.GetKeyDown(KeyCode.Alpha0)) FitPage();
        if (control && Input.GetKeyDown(KeyCode.Alpha1)) FitWidth();
        if (control && Input.GetKeyDown(KeyCode.S) && !saving) SaveCopyAsync();

        float x = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        float y = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        Vector2 overflow = content.rect.size - scroll.viewport.rect.size;
        float step = 700f * Time.unscaledDeltaTime;
        if (overflow.x > 0f && x != 0f) scroll.horizontalNormalizedPosition = Mathf.Clamp01(scroll.horizontalNormalizedPosition + x * step / overflow.x);
        if (overflow.y > 0f && y != 0f) scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + y * step / overflow.y);
    }

    private void SelectControl(int direction)
    {
        int current = -1;
        for (int i = 0; i < controls.Count; i++)
        {
            if (controls[i].gameObject == eventSystem.currentSelectedGameObject) current = i;
        }
        for (int offset = 1; offset <= controls.Count; offset++)
        {
            int index = (current + direction * offset + controls.Count * 2) % controls.Count;
            if (!controls[index].gameObject.activeInHierarchy || !controls[index].IsInteractable()) continue;
            eventSystem.SetSelectedGameObject(controls[index].gameObject);
            return;
        }
    }

    public void ChangePage(int offset)
    {
        if (document == null) return;
        int target = Mathf.Clamp(pageIndex + offset, 0, document.PageSizes.Length - 1);
        if (target == pageIndex) return;
        pageIndex = target;
        pageImage.enabled = false;
        RequestRender();
        scroll.normalizedPosition = new Vector2(0.5f, 1f);
    }

    public void ChangeZoom(int direction)
    {
        if (document == null) return;
        fitMode = FitMode.Manual;
        zoom = Mathf.Clamp(zoom * (direction > 0 ? 1.25f : 0.8f), 0.25f, 4f);
        RequestRender();
    }

    public void FitPage()
    {
        fitMode = FitMode.Page;
        RequestRender();
    }

    public void FitWidth()
    {
        fitMode = FitMode.Width;
        RequestRender();
    }

    private void RequestRender()
    {
        if (document == null || closed) return;
        requestVersion++;
        renderCancellation?.Cancel();
        Vector2 available = scroll.viewport.rect.size - Vector2.one * 40f;
        Vector2 size = document.PageSizes[pageIndex];
        if (fitMode != FitMode.Manual)
        {
            zoom = fitMode == FitMode.Page ? Mathf.Min(available.x / size.x, available.y / size.y) : available.x / size.x;
            zoom = Mathf.Max(0.05f, zoom);
        }
        pageImage.rectTransform.sizeDelta = size * zoom;
        content.sizeDelta = Vector2.Max(size * zoom + Vector2.one * 40f, scroll.viewport.rect.size);
        pageLabel.text = "Page " + (pageIndex + 1) + " of " + document.PageSizes.Length;
        zoomLabel.text = Mathf.RoundToInt(zoom * 100f) + "%";
        previousButton.interactable = pageIndex > 0;
        nextButton.interactable = pageIndex < document.PageSizes.Length - 1;
        zoomOutButton.interactable = zoom > 0.25f;
        zoomInButton.interactable = zoom < 4f;
        fitPageButton.interactable = fitWidthButton.interactable = true;
        saveButton.interactable = !saving;
        retryButton.gameObject.SetActive(false);
        SetLoading("Rendering page " + (pageIndex + 1) + "...");
        renderAt = Time.unscaledTime + 0.12f;
    }

    private async void RenderAsync()
    {
        rendering = true;
        renderAt = -1f;
        int version = requestVersion;
        int index = pageIndex;
        var currentDocument = document;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        renderCancellation = cancellation;
        CancellationToken token = cancellation.Token;
        int dimension = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(pageImage.rectTransform.sizeDelta.x, pageImage.rectTransform.sizeDelta.y) * canvas.scaleFactor), 64, Mathf.Min(3072, SystemInfo.maxTextureSize));
        try
        {
            var rendered = await Task.Run(() => currentDocument.Render(index, dimension, token), token);
            if (token.IsCancellationRequested || this == null || closed || version != requestVersion) return;
            ReleaseTexture();
            pageTexture = new Texture2D(rendered.Width, rendered.Height, TextureFormat.RGBA32, false);
            pageTexture.name = "Report page";
            pageTexture.filterMode = FilterMode.Bilinear;
            pageTexture.wrapMode = TextureWrapMode.Clamp;
            pageTexture.LoadRawTextureData(rendered.Pixels);
            pageTexture.Apply(false, true);
            pageImage.texture = pageTexture;
            pageImage.enabled = true;
            loadingPanel.SetActive(false);
            statusLabel.text = "Mouse wheel or arrows: scroll  ·  Tab: controls  ·  + / −: zoom  ·  Esc: close";
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested && this != null && !closed && version == requestVersion) ShowError(exception);
        }
        finally
        {
            if (renderCancellation == cancellation) renderCancellation = null;
            cancellation.Dispose();
            rendering = false;
        }
    }

    public async void SaveCopyAsync()
    {
        if (document == null || saving || closed) return;
        saving = true;
        saveButton.interactable = false;
        statusLabel.text = "Saving copy...";
        CancellationToken token = lifetime.Token;
        var currentDocument = document;
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Lesson Reports", "Copias");
        try
        {
            string destination = await Task.Run(() => currentDocument.SaveCopy(folder, token), token);
            if (!token.IsCancellationRequested && this != null && !closed) statusLabel.text = "Copy saved to: " + destination;
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (!token.IsCancellationRequested && this != null && !closed) statusLabel.text = "Could not save the copy. Check the free space and the permissions of the Documents folder.";
        }
        finally
        {
            saving = false;
            if (this != null && !closed) saveButton.interactable = true;
        }
    }

    private void Retry()
    {
        if (document == null) LoadAsync();
        else RequestRender();
    }

    private void SetLoading(string message)
    {
        loadingPanel.SetActive(true);
        loadingLabel.color = ThemeColor(TrainingThemeColor.Brand);
        loadingLabel.text = message;
    }

    private void ShowError(Exception exception)
    {
        ReleaseTexture();
        SetLoading(exception is FileNotFoundException || exception is DirectoryNotFoundException ? "The PDF file was not found."
            : exception is PlatformNotSupportedException ? "The viewer is available on 64-bit Windows."
            : exception is DllNotFoundException || exception is BadImageFormatException || exception is EntryPointNotFoundException ? "The local PDF renderer is not available in this installation."
            : exception is InvalidDataException ? exception.Message
            : "Could not open the PDF. The file may be damaged, password-protected or unreadable.");
        loadingLabel.color = ThemeColor(TrainingThemeColor.Warning);
        retryButton.gameObject.SetActive(true);
        statusLabel.text = "The original report remains saved in the Documents folder.";
        Debug.LogWarning("The local PDF viewer failed: " + exception.GetType().Name);
    }

    public void Close()
    {
        if (closed) return;
        Release();
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDestroy() => Release();

    private void Release()
    {
        if (closed) return;
        closed = true;
        closedFrame = Time.frameCount;
        if (instance == this) instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        lifetime.Cancel();
        renderCancellation?.Cancel();
        document?.Dispose();
        document = null;
        ReleaseTexture();
        if (eventSystem != null) eventSystem.enabled = false;
        for (int i = 0; i < suspendedSystems.Count; i++)
        {
            if (suspendedSystems[i] != null) suspendedSystems[i].enabled = true;
        }
        if (previousSystem != null && previousSystem.isActiveAndEnabled) EventSystem.current = previousSystem;
        if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(previousSelection);
        }
        suspendedSystems.Clear();
        Cursor.lockState = oldCursorLock;
        Cursor.visible = oldCursorVisible;
        lifetime.Dispose();
    }

    private void ReleaseTexture()
    {
        if (pageImage != null)
        {
            pageImage.texture = null;
            pageImage.enabled = false;
        }
        if (pageTexture != null) Destroy(pageTexture);
        pageTexture = null;
    }

    private void RefreshLayout()
    {
        Vector2 resolution = new Vector2(Screen.width, Screen.height);
        Rect safeArea = Screen.safeArea;
        if (resolution == lastResolution && safeArea == lastSafeArea && Mathf.Approximately(lastCanvasScale, canvas.scaleFactor)) return;
        lastResolution = resolution;
        lastSafeArea = safeArea;
        float scale = canvas.scaleFactor;
        lastCanvasScale = scale;
        float width = Mathf.Min(1620f, safeArea.width / scale - 48f);
        panel.sizeDelta = new Vector2(width, safeArea.height / scale - 48f);
        panel.anchoredPosition = (safeArea.center - resolution * 0.5f) / scale;
        footer.sizeDelta = new Vector2(Mathf.Min(520f, width - 48f), 60f);
        ((RectTransform)loadingPanel.transform).sizeDelta = new Vector2(Mathf.Min(480f, width - 48f), 248f);
        bool narrow = width < 1200f;
        Place(pageRow, new Vector2(narrow ? 0.5f : 0f, 1f), new Vector2(narrow ? 0f : 24f, -120f), new Vector2(narrow ? 0.5f : 0f, 1f), new Vector2(434f, 56f));
        Place(zoomRow, new Vector2(narrow ? 0.5f : 1f, 1f), new Vector2(narrow ? 0f : -24f, narrow ? -188f : -120f), new Vector2(narrow ? 0.5f : 1f, 1f), new Vector2(Mathf.Min(660f, width - 48f), 56f));
        RectTransform scrollRect = (RectTransform)scroll.transform;
        Stretch(scrollRect, new Vector2(24f, 156f), new Vector2(-24f, narrow ? -264f : -196f));
        Canvas.ForceUpdateCanvases();
        if (document != null) RequestRender();
    }

    private void BuildUI()
    {
        gameObject.name = "Report viewer";
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        gameObject.AddComponent<GraphicRaycaster>();
        var scrim = Image("Screen scrim", transform, ThemeColor(TrainingThemeColor.Scrim), false);
        Stretch(scrim.rectTransform, Vector2.zero, Vector2.zero);
        panel = Image("Document", transform, ThemeColor(TrainingThemeColor.Window)).rectTransform;
        Place(panel, Vector2.one * 0.5f, Vector2.zero, Vector2.one * 0.5f, new Vector2(1620f, 1032f));
        TMP_Text title = Text("Lesson report", panel, 36f, ThemeColor(TrainingThemeColor.Brand));
        StretchTop(title.rectTransform, 24f, -24f, -200f, 48f);
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        fileLabel = Text("Local PDF document", panel, 21f, ThemeColor(TrainingThemeColor.SecondaryText));
        StretchTop(fileLabel.rectTransform, 24f, -76f, -24f, 32f);
        fileLabel.alignment = TextAlignmentOptions.MidlineLeft;
        fileLabel.overflowMode = TextOverflowModes.Ellipsis;
        closeButton = Button("CLOSE", panel, Close);
        Place((RectTransform)closeButton.transform, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(1f, 1f), new Vector2(150f, 56f));

        pageRow = Rect("Pages", panel);
        Row(pageRow, 8f);
        previousButton = Button("‹ Previous", pageRow, () => ChangePage(-1), 126f);
        pageLabel = Text("Page — of —", pageRow, 22f, ThemeColor(TrainingThemeColor.PrimaryText));
        Element(pageLabel.gameObject, 166f);
        nextButton = Button("Next ›", pageRow, () => ChangePage(1), 126f);
        zoomRow = Rect("Zoom", panel);
        Row(zoomRow, 8f);
        zoomOutButton = Button("−", zoomRow, () => ChangeZoom(-1), 56f);
        zoomLabel = Text("—%", zoomRow, 22f, ThemeColor(TrainingThemeColor.PrimaryText));
        Element(zoomLabel.gameObject, 80f);
        zoomInButton = Button("+", zoomRow, () => ChangeZoom(1), 56f);
        fitPageButton = Button("FIT PAGE", zoomRow, FitPage, 0f);
        fitWidthButton = Button("FIT WIDTH", zoomRow, FitWidth, 0f);

        var scrollObject = Image("Reading area", panel, ThemeColor(TrainingThemeColor.NeutralControl));
        scroll = scrollObject.gameObject.AddComponent<ScrollRect>();
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = false;
        var viewport = Image("Visible page", scroll.transform, Color.clear, false).rectTransform;
        Stretch(viewport, new Vector2(0f, 22f), new Vector2(-22f, 0f));
        viewport.gameObject.AddComponent<RectMask2D>();
        content = Rect("Content", viewport);
        Place(content, new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 1f), Vector2.zero);
        pageImage = Rect("PDF page", content).gameObject.AddComponent<RawImage>();
        Place(pageImage.rectTransform, Vector2.one * 0.5f, Vector2.zero, Vector2.one * 0.5f, Vector2.zero);
        pageImage.uvRect = new Rect(0f, 1f, 1f, -1f);
        pageImage.raycastTarget = false;
        pageImage.enabled = false;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.verticalScrollbar = Scrollbar(scroll.transform, true);
        scroll.horizontalScrollbar = Scrollbar(scroll.transform, false);
        scroll.verticalScrollbarVisibility = scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        loadingPanel = Image("Loading status", panel, ThemeColor(TrainingThemeColor.Surface)).gameObject;
        Place((RectTransform)loadingPanel.transform, Vector2.one * 0.5f, Vector2.zero, Vector2.one * 0.5f, new Vector2(480f, 248f));
        loadingLabel = Text("Loading report...", loadingPanel.transform, 25f, ThemeColor(TrainingThemeColor.Brand));
        Stretch(loadingLabel.rectTransform, new Vector2(20f, 76f), new Vector2(-20f, -16f));
        retryButton = Button("TRY AGAIN", loadingPanel.transform, Retry);
        Place((RectTransform)retryButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(0.5f, 0f), new Vector2(300f, 52f));
        retryButton.gameObject.SetActive(false);

        statusLabel = Text("", panel, 20f, ThemeColor(TrainingThemeColor.SecondaryText));
        statusLabel.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(statusLabel.rectTransform, new Vector2(24f, 96f), new Vector2(-24f, 144f), true);
        footer = Rect("Actions", panel);
        Place(footer, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(0.5f, 0f), new Vector2(520f, 60f));
        Row(footer, 16f);
        Button("BACK", footer, Close);
        saveButton = Button("SAVE COPY", footer, SaveCopyAsync, 0f, true);
        previousButton.interactable = nextButton.interactable = zoomOutButton.interactable = zoomInButton.interactable = false;
        fitPageButton.interactable = fitWidthButton.interactable = saveButton.interactable = false;

        var inputObject = new GameObject("Viewer input system", typeof(EventSystem), typeof(StandaloneInputModule));
        inputObject.transform.SetParent(transform, false);
        eventSystem = inputObject.GetComponent<EventSystem>();
        eventSystem.sendNavigationEvents = false;
        EventSystem.current = eventSystem;
        Canvas.ForceUpdateCanvases();
        RefreshLayout();
    }

    private Scrollbar Scrollbar(Transform parent, bool vertical)
    {
        var track = Image(vertical ? "Vertical scrollbar" : "Horizontal scrollbar", parent, ThemeColor(TrainingThemeColor.NeutralControl));
        RectTransform trackRect = track.rectTransform;
        trackRect.anchorMin = vertical ? new Vector2(1f, 0f) : Vector2.zero;
        trackRect.anchorMax = vertical ? Vector2.one : new Vector2(1f, 0f);
        trackRect.offsetMin = vertical ? new Vector2(-18f, 22f) : Vector2.zero;
        trackRect.offsetMax = vertical ? Vector2.zero : new Vector2(-22f, 18f);
        var handle = Image("Scrollbar handle", trackRect, ThemeColor(TrainingThemeColor.Brand));
        Stretch(handle.rectTransform, Vector2.zero, Vector2.zero);
        var bar = track.gameObject.AddComponent<Scrollbar>();
        bar.direction = vertical ? UnityEngine.UI.Scrollbar.Direction.BottomToTop : UnityEngine.UI.Scrollbar.Direction.LeftToRight;
        bar.handleRect = handle.rectTransform;
        bar.targetGraphic = handle;
        bar.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = bar.colors;
        colors.highlightedColor = colors.selectedColor = Color.white;
        colors.pressedColor = ThemeColor(TrainingThemeColor.SelectedSurface);
        bar.colors = colors;
        return bar;
    }

    private Button Button(string label, Transform parent, UnityAction action, float width = 0f, bool primary = false)
    {
        TrainingThemeColor surface = primary ? TrainingThemeColor.PrimaryButtonSurface : TrainingThemeColor.SecondaryButtonSurface;
        TrainingThemeColor textColor = primary ? TrainingThemeColor.PrimaryButtonText : TrainingThemeColor.SecondaryButtonText;
        TrainingThemeColor pressed = primary ? TrainingThemeColor.PrimaryButtonPressed : TrainingThemeColor.SecondaryButtonPressed;
        var image = Image(label, parent, ThemeColor(surface));
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.Explicit };
        button.onClick.AddListener(action);
        var text = Text(label, image.transform, 22f, ThemeColor(textColor));
        Stretch(text.rectTransform, new Vector2(8f, 4f), new Vector2(-8f, -4f));
        text.fontStyle = FontStyles.Bold;
        var outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = ThemeColor(TrainingThemeColor.Brand);
        var colors = button.colors;
        colors.normalColor = ThemeColor(surface);
        colors.highlightedColor = colors.selectedColor = ThemeColor(TrainingThemeColor.SelectedSurface);
        colors.pressedColor = ThemeColor(pressed);
        button.colors = colors;
        Element(image.gameObject, width);
        controls.Add(button);
        return button;
    }

    private Color ThemeColor(TrainingThemeColor role)
    {
        TrainingColor color = theme.GetColor(role);
        return new Color32(color.Red, color.Green, color.Blue, color.Alpha);
    }

    private Image Image(string name, Transform parent, Color color, bool rounded = true)
    {
        var image = Rect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        if (rounded)
        {
            image.sprite = roundedSprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
        }
        return image;
    }

    private TMP_Text Text(string text, Transform parent, float size, Color color)
    {
        var label = Rect(text, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.richText = false;
        return label;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 pivot, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, bool bottom = false)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = bottom ? Vector2.right : Vector2.one;
        rect.offsetMin = min;
        rect.offsetMax = max;
    }

    private static void StretchTop(RectTransform rect, float left, float top, float right, float height)
    {
        rect.anchorMin = Vector2.up;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.up;
        rect.offsetMin = new Vector2(left, top - height);
        rect.offsetMax = new Vector2(right, top);
    }

    private static void Row(RectTransform rect, float spacing)
    {
        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleCenter;
    }

    private static void Element(GameObject target, float width)
    {
        var element = target.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.flexibleWidth = width > 0f ? 0f : 1f;
        element.minWidth = width > 0f ? width : 64f;
        element.minHeight = 56f;
        element.preferredHeight = 56f;
    }
}

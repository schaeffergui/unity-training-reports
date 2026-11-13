using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TrainingReports;

public class LessonReportUI : MonoBehaviour
{
    public static ITrainingTheme Theme { get; set; } = DefaultTrainingTheme.Instance;
    public ITrainingContext Context { get; set; }

    private static Color PanelColor => ThemeColor(TrainingThemeColor.Window);
    private static Color CardColor => ThemeColor(TrainingThemeColor.Surface);
    private static Color BorderColor => ThemeColor(TrainingThemeColor.Border);
    private static Color BackgroundScrim => ThemeColor(TrainingThemeColor.Scrim);

    private static Color ThemeColor(TrainingThemeColor role)
    {
        TrainingColor color = Theme.GetColor(role);
        return new Color32(color.Red, color.Green, color.Blue, color.Alpha);
    }
    private static Sprite roundedSprite;

    [SerializeField] private LessonReportData reportData;
    [SerializeField] private TMP_Text studentText;
    [SerializeField] private TMP_Text instructorText;

    private static readonly Vector2 BackButtonAnchorMin = new Vector2(0.215f, 0.105f);
    private static readonly Vector2 BackButtonAnchorMax = new Vector2(0.445f, 0.185f);
    private static readonly Vector2 ReportButtonAnchorMin = new Vector2(0.555f, 0.105f);
    private static readonly Vector2 ReportButtonAnchorMax = new Vector2(0.785f, 0.185f);

    private RectTransform backButtonRect;
    private RectTransform reportButtonRect;

    private void OnEnable()
    {
        ApplyThemeStyle();
        PauseGame();
    }

    private void OnDisable()
    {
        ResumeGame();
    }

    private void OnDestroy()
    {
        ResumeGame();
    }

    private void PauseGame()
    {
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    private void ResumeGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private void Start()
    {
        ApplyThemeStyle();

        ITrainingContext context = Context ?? reportData;
        string studentName = context != null ? context.StudentName : null;
        string instructorName = context != null ? context.InstructorName : null;

        if (studentText != null)
        {
            studentText.text = string.IsNullOrEmpty(studentName) ? "Guest" : studentName;
        }

        if (instructorText != null)
        {
            instructorText.text = string.IsNullOrEmpty(instructorName) ? "Guest" : instructorName;
        }
    }

    private void LateUpdate()
    {
        NormalizeButtonTransform(backButtonRect, BackButtonAnchorMin, BackButtonAnchorMax);
        NormalizeButtonTransform(reportButtonRect, ReportButtonAnchorMin, ReportButtonAnchorMax);
    }

    private void ApplyThemeStyle()
    {
        RectTransform rootRect = transform as RectTransform;
        if (rootRect == null)
        {
            return;
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3840f, 2160f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0.245f;
        }

        Stretch(rootRect);
        StyleBackground(transform.Find("Image"));
        StyleScrim(transform.Find("BackgroundBlue (1)"));

        Transform reportView = transform.Find("Vehicle Report");
        if (reportView == null)
        {
            return;
        }

        Stretch(reportView as RectTransform);
        CreatePanel(reportView);
        StyleHeader(reportView.Find("BlueFiltre"), reportView);
        StyleReportContent(reportView.Find("ScrollArea"));
        Transform backButton = reportView.Find("bntExit");
        Transform reportButton = reportView.Find("Report");
        backButtonRect = backButton as RectTransform;
        reportButtonRect = reportButton as RectTransform;

        StyleButton(backButton, "BACK", false, BackButtonAnchorMin, BackButtonAnchorMax);
        StyleButton(reportButton, "VIEW REPORT", true, ReportButtonAnchorMin, ReportButtonAnchorMax);
    }

    private void StyleBackground(Transform background)
    {
        if (background == null)
        {
            return;
        }

        Stretch(background as RectTransform);
        Image image = background.GetComponent<Image>();
        if (image != null)
        {
            image.color = Color.white;
            image.raycastTarget = false;
            image.preserveAspect = false;
        }
    }

    private void StyleScrim(Transform scrim)
    {
        if (scrim == null)
        {
            return;
        }

        Stretch(scrim as RectTransform);
        Image image = GetOrAdd<Image>(scrim.gameObject);
        image.sprite = null;
        image.color = BackgroundScrim;
        image.raycastTarget = false;
        scrim.SetSiblingIndex(Mathf.Min(1, scrim.parent.childCount - 1));
    }

    private void CreatePanel(Transform reportView)
    {
        Transform existing = reportView.Find("Modern Report Panel");
        GameObject panel = existing != null
            ? existing.gameObject
            : new GameObject("Modern Report Panel", typeof(RectTransform), typeof(Image));

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(reportView, false);
        SetRect(panelRect, new Vector2(0.175f, 0.075f), new Vector2(0.825f, 0.925f));

        Image panelImage = panel.GetComponent<Image>();
        SetRoundedImage(panelImage, PanelColor);
        panelImage.raycastTarget = false;

        Outline outline = GetOrAdd<Outline>(panel);
        outline.effectColor = ThemeColor(TrainingThemeColor.Brand);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        Shadow shadow = FindPlainShadow(panel);
        if (shadow == null)
        {
            shadow = panel.AddComponent<Shadow>();
        }
        shadow.effectColor = new Color(0f, 0f, 0f, 0.32f);
        shadow.effectDistance = new Vector2(0f, -12f);
        shadow.useGraphicAlpha = true;
        panelRect.SetSiblingIndex(0);

        Transform accent = panel.transform.Find("Accent");
        GameObject accentObject = accent != null
            ? accent.gameObject
            : new GameObject("Accent", typeof(RectTransform), typeof(Image));
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.SetParent(panelRect, false);
        SetRect(accentRect, new Vector2(0f, 0.985f), Vector2.one);
        Image accentImage = accentObject.GetComponent<Image>();
        accentImage.color = ThemeColor(TrainingThemeColor.Brand);
        accentImage.raycastTarget = false;
    }

    private void StyleHeader(Transform oldHeader, Transform reportView)
    {
        if (oldHeader != null)
        {
            oldHeader.gameObject.SetActive(false);
        }

        TMP_Text title = FindOrCreateText(reportView, "Modern Report Title");
        title.text = "LESSON REPORT";
        title.color = ThemeColor(TrainingThemeColor.Brand);
        title.fontStyle = FontStyles.Bold;
        title.fontSize = 58f;
        title.enableAutoSizing = true;
        title.fontSizeMin = 38f;
        title.fontSizeMax = 58f;
        title.alignment = TextAlignmentOptions.Center;
        title.raycastTarget = false;
        SetRect(title.rectTransform, new Vector2(0.25f, 0.825f), new Vector2(0.75f, 0.895f));

        TMP_Text subtitle = FindOrCreateText(reportView, "Modern Report Subtitle");
        subtitle.text = "Training performance summary";
        subtitle.color = ThemeColor(TrainingThemeColor.SecondaryText);
        subtitle.fontSize = 24f;
        subtitle.enableAutoSizing = true;
        subtitle.fontSizeMin = 18f;
        subtitle.fontSizeMax = 24f;
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.raycastTarget = false;
        SetRect(subtitle.rectTransform, new Vector2(0.25f, 0.78f), new Vector2(0.75f, 0.825f));
    }

    private void StyleReportContent(Transform scrollArea)
    {
        if (scrollArea == null)
        {
            return;
        }

        SetRect(scrollArea as RectTransform, new Vector2(0.215f, 0.215f), new Vector2(0.785f, 0.765f));
        Image scrollBackground = GetOrAdd<Image>(scrollArea.gameObject);
        SetRoundedImage(scrollBackground, ThemeColor(TrainingThemeColor.Window));
        scrollBackground.raycastTarget = false;

        Transform viewport = scrollArea.Find("Report Viewport");
        if (viewport != null)
        {
            SetRect(viewport as RectTransform, new Vector2(0.025f, 0.025f), new Vector2(0.96f, 0.975f));
            Image[] viewportImages = viewport.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < viewportImages.Length; i++)
            {
                viewportImages[i].color = Color.clear;
            }
        }

        Transform tasks = scrollArea.Find("Tasks");
        if (tasks != null)
        {
            StyleTasks(tasks);
        }

        ScrollRect scrollRect = scrollArea.GetComponent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.08f;
            scrollRect.scrollSensitivity = 36f;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        }

        StyleScrollbar(scrollArea.GetComponentInChildren<Scrollbar>(true));
    }

    private void StyleTasks(Transform tasks)
    {
        RectTransform tasksRect = tasks as RectTransform;
        tasksRect.anchorMin = new Vector2(0f, 1f);
        tasksRect.anchorMax = new Vector2(1f, 1f);
        tasksRect.pivot = new Vector2(0.5f, 1f);
        tasksRect.anchoredPosition = Vector2.zero;
        tasksRect.sizeDelta = new Vector2(0f, 780f);

        VerticalLayoutGroup layout = GetOrAdd<VerticalLayoutGroup>(tasks.gameObject);
        layout.padding = new RectOffset(22, 22, 18, 22);
        layout.spacing = 13f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.reverseArrangement = false;

        ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(tasks.gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Transform[] children = new Transform[tasks.childCount];
        for (int i = 0; i < children.Length; i++)
        {
            children[i] = tasks.GetChild(i);
        }

        int rowIndex = 0;
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child.name.StartsWith("Title", StringComparison.OrdinalIgnoreCase))
            {
                StyleSectionTitle(child);
                continue;
            }

            if (child.name.StartsWith("Task", StringComparison.OrdinalIgnoreCase))
            {
                StyleDataRow(child, rowIndex++);
            }
        }

        Array.Sort(children, (left, right) => GetReportRowOrder(left.name).CompareTo(GetReportRowOrder(right.name)));
        for (int i = 0; i < children.Length; i++)
        {
            children[i].SetSiblingIndex(i);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(tasksRect);
    }

    private void StyleSectionTitle(Transform section)
    {
        section.localScale = Vector3.one;
        section.localRotation = Quaternion.identity;
        LayoutElement layout = GetOrAdd<LayoutElement>(section.gameObject);
        layout.minHeight = 80f;
        layout.preferredHeight = 80f;

        Image[] backgrounds = section.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < backgrounds.Length; i++)
        {
            backgrounds[i].color = Color.clear;
            backgrounds[i].raycastTarget = false;
        }

        TMP_Text[] texts = section.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            text.text = "EVALUATION";
            text.color = ThemeColor(TrainingThemeColor.Brand);
            text.fontStyle = FontStyles.Bold;
            text.fontSize = 36f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.margin = new Vector4(18f, 0f, 0f, 0f);
            text.raycastTarget = false;
            Stretch(text.rectTransform);
        }
    }

    private void StyleDataRow(Transform row, int index)
    {
        row.localScale = Vector3.one;
        row.localRotation = Quaternion.identity;
        LayoutElement layout = GetOrAdd<LayoutElement>(row.gameObject);
        layout.minHeight = 90f;
        layout.preferredHeight = 90f;

        Image rowImage = GetOrAdd<Image>(row.gameObject);
        SetRoundedImage(rowImage, index % 2 == 0 ? CardColor : ThemeColor(TrainingThemeColor.Window));
        rowImage.raycastTarget = false;

        Outline outline = GetOrAdd<Outline>(row.gameObject);
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            bool isResult = text.name.IndexOf("Result", StringComparison.OrdinalIgnoreCase) >= 0;
            text.text = text.text.Trim();
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = isResult ? 34f : 30f;
            text.fontStyle = isResult ? FontStyles.Bold : FontStyles.Normal;
            text.color = isResult ? ThemeColor(TrainingThemeColor.Brand) : ThemeColor(TrainingThemeColor.SecondaryText);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.margin = Vector4.zero;
            text.raycastTarget = false;

            if (isResult)
            {
                SetRect(text.rectTransform, new Vector2(0.43f, 0f), new Vector2(0.965f, 1f), new Vector2(8f, 0f), Vector2.zero);
            }
            else
            {
                SetRect(text.rectTransform, new Vector2(0.035f, 0f), new Vector2(0.43f, 1f), Vector2.zero, new Vector2(-8f, 0f));
            }
        }
    }

    private void StyleScrollbar(Scrollbar scrollbar)
    {
        if (scrollbar == null)
        {
            return;
        }

        SetRect(scrollbar.transform as RectTransform, new Vector2(0.973f, 0.045f), new Vector2(0.98f, 0.955f));

        Image track = scrollbar.GetComponent<Image>();
        if (track != null)
        {
            SetRoundedImage(track, ThemeColor(TrainingThemeColor.NeutralControl));
        }

        if (scrollbar.handleRect != null)
        {
            Image handle = scrollbar.handleRect.GetComponent<Image>();
            if (handle != null)
            {
                SetRoundedImage(handle, ThemeColor(TrainingThemeColor.Brand));
            }
        }
    }

    private void StyleButton(Transform buttonTransform, string label, bool primary, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (buttonTransform == null)
        {
            return;
        }

        DisableLegacyButtonAnimation(buttonTransform);

        RectTransform buttonRect = buttonTransform as RectTransform;
        NormalizeButtonTransform(buttonRect, anchorMin, anchorMax);

        Image image = GetOrAdd<Image>(buttonTransform.gameObject);
        SetRoundedImage(image, primary ? ThemeColor(TrainingThemeColor.Brand) : ThemeColor(TrainingThemeColor.SecondaryButtonSurface));

        Outline outline = GetOrAdd<Outline>(buttonTransform.gameObject);
        outline.effectColor = ThemeColor(TrainingThemeColor.Brand);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;

        Button button = buttonTransform.GetComponent<Button>();
        if (button != null)
        {
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = ThemeColor(TrainingThemeColor.SelectedSurface);
            colors.pressedColor = primary ? ThemeColor(TrainingThemeColor.PrimaryButtonPressed) : ThemeColor(TrainingThemeColor.SelectedSurface);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = ThemeColor(TrainingThemeColor.NeutralControl);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.12f;
            button.colors = colors;
        }

        TMP_Text[] labels = buttonTransform.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text text = labels[i];
            text.text = label;
            text.color = primary ? Color.white : ThemeColor(TrainingThemeColor.Brand);
            text.fontStyle = FontStyles.Bold;
            text.fontSize = 26f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = 26f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            Stretch(text.rectTransform);
        }

        Transform oldTiles = buttonTransform.Find("Tiles");
        if (oldTiles != null)
        {
            oldTiles.gameObject.SetActive(false);
        }
    }

    private static void DisableLegacyButtonAnimation(Transform buttonTransform)
    {
        MonoBehaviour[] behaviours = buttonTransform.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            Type behaviourType = behaviour.GetType();
            if (behaviourType.FullName == "Doozy.Engine.UI.UIButton")
            {
                behaviour.enabled = false;
                if (Application.isPlaying)
                {
                    Destroy(behaviour);
                }
                else
                {
                    DestroyImmediate(behaviour);
                }
            }
        }
    }

    private static void NormalizeButtonTransform(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static TMP_Text FindOrCreateText(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out TMP_Text existingText))
        {
            return existingText;
        }

        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        return textObject.GetComponent<TextMeshProUGUI>();
    }

    private static int GetReportRowOrder(string objectName)
    {
        if (objectName.StartsWith("Title", StringComparison.OrdinalIgnoreCase)) return 0;
        if (objectName.IndexOf("Student", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
        if (objectName.IndexOf("Instructor", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
        if (objectName.IndexOf("Data", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
        if (objectName.IndexOf("Training", StringComparison.OrdinalIgnoreCase) >= 0) return 4;
        if (objectName.IndexOf("Duration", StringComparison.OrdinalIgnoreCase) >= 0) return 5;
        if (objectName.IndexOf("Percentage", StringComparison.OrdinalIgnoreCase) >= 0) return 6;
        if (objectName.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0) return 7;
        return 100;
    }

    private static void SetRoundedImage(Image image, Color color)
    {
        image.sprite = GetRoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
    }

    private static Sprite GetRoundedSprite()
    {
        if (roundedSprite != null)
        {
            return roundedSprite;
        }

        const int size = 32;
        const float radius = 8f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Lesson Report Rounded Rectangle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cornerX = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                float cornerY = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                float distance = Mathf.Sqrt(cornerX * cornerX + cornerY * cornerY);
                float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        roundedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        roundedSprite.name = "Lesson Report Rounded Rectangle";
        roundedSprite.hideFlags = HideFlags.HideAndDontSave;
        return roundedSprite;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static Shadow FindPlainShadow(GameObject gameObject)
    {
        Shadow[] shadows = gameObject.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i].GetType() == typeof(Shadow))
            {
                return shadows[i];
            }
        }

        return null;
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        SetRect(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static RDSceneManager;

public class TrainingTaskUI : MonoBehaviour
{
    private static readonly Color PanelColor = RDCompanyPalette.Hex(0xF7F5FC, 0.94f);
    private static readonly Color TaskCardColor = RDCompanyPalette.Hex(0xFFFFFF, 0.88f);
    private static readonly Color TaskBorderColor = RDCompanyPalette.Hex(0xCDBAF2, 0.9f);
    private static readonly Color PendingIconColor = RDCompanyPalette.Hex(0x7B55B7);
    private static readonly Color ActiveIconColor = RDCompanyPalette.Hex(0x9D53E0);
    private static readonly Color CompletedIconColor = RDCompanyPalette.Hex(0x22A447);
    private static readonly Color FailedIconColor = RDCompanyPalette.Hex(0xD83A4E);
    private static Sprite roundedPanelSprite;

    [System.Serializable]
    public class TaskItem
    {
        public TextMeshProUGUI label;
        public Image icon;

        [HideInInspector] public string originalText;

        [Header("Check-in Colors")]
        public Color pendingColor = Color.gray;
        public Color inProgressColor = Color.yellow;
        public Color completedColor = Color.green;
        public Color incompletedColor = Color.red;

        [Header("Check-in iconss")]
        public Sprite pendingSprite;
        public Sprite inProgressSprite;
        public Sprite completedSprite;
        public Sprite incompletedSprite;
    }

    public List<TaskItem> tasks = new List<TaskItem>();

    private enum TaskStatus { Pending, InProgress, Completed, Incompleted }
    private List<TaskStatus> taskStatuses = new List<TaskStatus>();

    void Start()
    {
        ApplyCompanyStyle();

        foreach (var task in tasks)
        {
            if (task.label != null)
            {
                task.originalText = task.label.text;
            }
        }

        taskStatuses = new List<TaskStatus>(new TaskStatus[tasks.Count]);
        ResetTasks();
    }

    private void ApplyCompanyStyle()
    {
        RectTransform panelRect = transform as RectTransform;
        if (panelRect == null)
        {
            return;
        }

        Sprite roundedSprite = GetRoundedPanelSprite();
        Image panelImage = GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.sprite = roundedSprite;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = PanelColor;
            panelImage.raycastTarget = false;
        }

        Outline panelOutline = GetComponent<Outline>();
        if (panelOutline == null)
        {
            panelOutline = gameObject.AddComponent<Outline>();
        }
        panelOutline.effectColor = RDCompanyPalette.Hex(0x5816C1, 0.85f);
        panelOutline.effectDistance = new Vector2(2f, -2f);
        panelOutline.useGraphicAlpha = true;

        Shadow panelShadow = null;
        Shadow[] shadowEffects = GetComponents<Shadow>();
        for (int i = 0; i < shadowEffects.Length; i++)
        {
            if (shadowEffects[i].GetType() == typeof(Shadow))
            {
                panelShadow = shadowEffects[i];
                break;
            }
        }
        if (panelShadow == null)
        {
            panelShadow = gameObject.AddComponent<Shadow>();
        }
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
        panelShadow.effectDistance = new Vector2(0f, -8f);
        panelShadow.useGraphicAlpha = true;

        VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(28, 28, 24, 28);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        ContentSizeFitter fitter = GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = gameObject.AddComponent<ContentSizeFitter>();
        }
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        StyleHeader();
        StyleAccent(panelRect);

        for (int i = 0; i < tasks.Count; i++)
        {
            StyleTask(tasks[i], i, roundedSprite);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
    }

    private void StyleHeader()
    {
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text.transform.parent != transform || !text.text.Trim().StartsWith("Tasks"))
            {
                continue;
            }

            text.text = "TASKS";
            text.color = RDCompanyPalette.BrandPurple;
            text.fontSize = 38f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.margin = new Vector4(12f, 0f, 0f, 0f);
            text.raycastTarget = false;

            LayoutElement headerLayout = text.GetComponent<LayoutElement>();
            if (headerLayout == null)
            {
                headerLayout = text.gameObject.AddComponent<LayoutElement>();
            }
            headerLayout.minHeight = 58f;
            headerLayout.preferredHeight = 58f;
            text.transform.SetSiblingIndex(0);
            return;
        }
    }

    private void StyleAccent(RectTransform panelRect)
    {
        Transform existing = transform.Find("Company Accent");
        GameObject accent = existing != null ? existing.gameObject : new GameObject("Company Accent", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.SetParent(panelRect, false);
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(0f, 8f);

        Image accentImage = accent.GetComponent<Image>();
        accentImage.color = RDCompanyPalette.BrandPurple;
        accentImage.raycastTarget = false;
        accent.GetComponent<LayoutElement>().ignoreLayout = true;
        accentRect.SetAsLastSibling();
    }

    private void StyleTask(TaskItem task, int index, Sprite roundedSprite)
    {
        if (task == null || task.label == null)
        {
            return;
        }

        RectTransform rowRect = task.label.transform.parent as RectTransform;
        if (rowRect == null)
        {
            return;
        }

        Image rowImage = rowRect.GetComponent<Image>();
        if (rowImage != null)
        {
            rowImage.sprite = roundedSprite;
            rowImage.type = Image.Type.Sliced;
            rowImage.color = TaskCardColor;
            rowImage.raycastTarget = false;
        }

        Outline rowOutline = rowRect.GetComponent<Outline>();
        if (rowOutline == null)
        {
            rowOutline = rowRect.gameObject.AddComponent<Outline>();
        }
        rowOutline.effectColor = TaskBorderColor;
        rowOutline.effectDistance = new Vector2(1.25f, -1.25f);
        rowOutline.useGraphicAlpha = true;

        task.label.color = RDCompanyPalette.DenseText;
        task.label.fontSize = 32f;
        task.label.enableAutoSizing = true;
        task.label.fontSizeMin = 24f;
        task.label.fontSizeMax = 32f;
        task.label.alignment = TextAlignmentOptions.MidlineLeft;
        task.label.textWrappingMode = TextWrappingModes.Normal;
        task.label.overflowMode = TextOverflowModes.Overflow;
        task.label.margin = Vector4.zero;
        task.label.raycastTarget = false;

        RectTransform labelRect = task.label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(76f, 10f);
        labelRect.offsetMax = new Vector2(-18f, -10f);

        if (task.icon != null)
        {
            RectTransform iconRect = task.icon.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(38f, 0f);
            iconRect.sizeDelta = new Vector2(42f, 42f);
            iconRect.localScale = Vector3.one;
            task.icon.preserveAspect = true;
            task.icon.raycastTarget = false;
        }

        float availableWidth = Mathf.Max(300f, rowRect.rect.width - 104f);
        float preferredTextHeight = task.label.GetPreferredValues(task.label.text, availableWidth, 0f).y;
        float rowHeight = Mathf.Clamp(preferredTextHeight + 24f, 64f, 112f);
        rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, rowHeight);

        LayoutElement rowLayout = rowRect.GetComponent<LayoutElement>();
        if (rowLayout == null)
        {
            rowLayout = rowRect.gameObject.AddComponent<LayoutElement>();
        }
        rowLayout.minHeight = rowHeight;
        rowLayout.preferredHeight = rowHeight;
        rowRect.SetSiblingIndex(index + 1);

        task.pendingColor = PendingIconColor;
        task.inProgressColor = ActiveIconColor;
        task.completedColor = CompletedIconColor;
        task.incompletedColor = FailedIconColor;
    }

    private static Sprite GetRoundedPanelSprite()
    {
        if (roundedPanelSprite != null)
        {
            return roundedPanelSprite;
        }

        const int size = 32;
        const float radius = 8f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Task Panel Rounded Rectangle",
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
        roundedPanelSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        roundedPanelSprite.name = "Task Panel Rounded Rectangle";
        roundedPanelSprite.hideFlags = HideFlags.HideAndDontSave;
        return roundedPanelSprite;
    }

    public void SetTargetHeight(int index, float height)
    {
        if (IsValidIndex(index))
        {
            var task = tasks[index];
            if (task.label != null)
            {
                task.label.text = $"{task.originalText} - {height:0} mm";
            }
        }
    }


    public void SetOperation(Operations operation) { }

    public void SetTaskInProgress(int index)
    {
        if (IsValidIndex(index))
        {
            taskStatuses[index] = TaskStatus.InProgress;
            var task = tasks[index];
            SetTaskState(task, task.originalText, task.inProgressColor, task.inProgressSprite);
        }
    }

    public void SetTaskCompleted(int index, bool isCompleted)
    {
        if (isCompleted)
        {
            if (IsValidIndex(index))
            {
                taskStatuses[index] = TaskStatus.Completed;
                var task = tasks[index];
                SetTaskState(task, $"Task {index + 1}: Completed", task.completedColor, task.completedSprite);
            }
        }
        else
        {
            if (IsValidIndex(index))
            {
                taskStatuses[index] = TaskStatus.Incompleted;
                var task = tasks[index];
                SetTaskState(task, $"Task {index + 1}: Not completed", task.incompletedColor, task.incompletedSprite);
            }
        }
        TrainingManager.Instance.OnTaskFinished(index, isCompleted);
    }


    private void SetTaskState(TaskItem task, string labelText, Color color, Sprite iconSprite)
    {
        if (task.label != null)
        {
            task.label.text = labelText;
        }

        if (task.icon != null)
        {
            task.icon.color = color;
            task.icon.sprite = iconSprite;
        }
    }


    public void ResetTasks()
    {
        for (int i = 0; i < tasks.Count; i++)
        {
            if (IsValidIndex(i))
            {
                SetTaskState(tasks[i], tasks[i].originalText, tasks[i].pendingColor, tasks[i].pendingSprite);
                taskStatuses[i] = TaskStatus.Pending;
            }
        }
    }

    public void UpdateTaskProgress(int index)
    {
        if (IsValidIndex(index))
        {
            var task = tasks[index];
            if (task.label != null)
            {
                task.label.text = task.originalText;
            }
            if (task.icon != null)
            {
                task.icon.color = task.inProgressColor;
                task.icon.sprite = task.inProgressSprite;
            }
            taskStatuses[index] = TaskStatus.InProgress;
        }
    }

    public bool IsTaskCompleted(int index)
    {
        bool result = IsValidIndex(index) && taskStatuses[index] == TaskStatus.Completed;
        return result;
    }


    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < tasks.Count;
    }
}

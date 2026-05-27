using UnityEngine.UIElements;
using FFramework.Utility;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PlayAnima))]
public class PlayAnimaEditor : Editor
{
    private PlayAnima anima;

    private VisualElement progressSection;
    private VisualElement fill;
    private VisualElement marker;
    private VisualElement eventPointsLayer;
    private Label timeLabel;
    private Image clipIcon;
    private Label clipNameLabel;
    private Button playPauseButton;

    private bool isDragging = false;

    // 缓存：避免每帧重复设置图像触发布局重算
    private Texture2D cachedBtnIcon;
    private Texture2D cachedAnimIcon;
    private string lastClipName = "";
    private string lastBtnIconName = "";

    private IVisualElementScheduledItem updateTask;

    private static readonly Color FillColor = new Color(0f, 0.8f, 0f, 0.5f);
    private static readonly Color MarkerColor = new Color(0.1f, 0.9f, 0.4f, 1f);
    private static readonly Color EventPointColor = new Color(1f, 0.5f, 0f, 1f);

    public override VisualElement CreateInspectorGUI()
    {
        anima = target as PlayAnima;
        var root = new VisualElement();

        // Header
        root.Add(new Label("Animancer 动画播放器")
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                unityTextAlign = TextAnchor.MiddleCenter,
                fontSize = 13,
                marginTop = 6,
                marginBottom = 4,
                paddingTop = 4,
                paddingBottom = 4,
                borderTopWidth = 1,
                borderBottomWidth = 1,
                borderLeftWidth = 1,
                borderRightWidth = 1,
                borderTopColor = Color.black,
                borderBottomColor = Color.black,
                borderLeftColor = Color.black,
                borderRightColor = Color.black,
                backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.5f)
            }
        });

        // 进度面板
        BuildProgressPanel(root);

        updateTask = root.schedule.Execute(OnUpdate).Every(10);
        return root;
    }

    private void BuildProgressPanel(VisualElement root)
    {
        progressSection = new VisualElement();
        progressSection.style.marginTop = 8;
        progressSection.style.marginLeft = 0;
        progressSection.style.marginRight = 0;
        progressSection.style.display = DisplayStyle.None;

        progressSection.Add(new Label("播放进度")
        {
            style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 }
        });

        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

        var track = new VisualElement();
        track.style.flexGrow = 1;
        track.style.height = 20;
        track.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        track.style.marginRight = 1;
        track.style.overflow = Overflow.Hidden;
        track.style.position = Position.Relative;
        track.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (anima == null) return;
            isDragging = true;
            track.CapturePointer(evt.pointerId);
            float w = track.resolvedStyle.width;
            if (w > 0f)
                anima.PlaybackProgress = Mathf.Clamp01(evt.localPosition.x / w);
            evt.StopPropagation();
        });

        track.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!isDragging || anima == null) return;
            float w = track.resolvedStyle.width;
            if (w > 0f)
                anima.PlaybackProgress = Mathf.Clamp01(evt.localPosition.x / w);
            evt.StopPropagation();
        });

        track.RegisterCallback<PointerUpEvent>(evt =>
        {
            isDragging = false;
            track.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        });

        fill = new VisualElement
        {
            style = { position = Position.Absolute, left = 0, top = 0, bottom = 0, width = 0, backgroundColor = FillColor }
        };
        track.Add(fill);

        marker = new VisualElement
        {
            style = { position = Position.Absolute, width = 3, top = 0, bottom = 0, left = 0, backgroundColor = MarkerColor }
        };
        track.Add(marker);

        eventPointsLayer = new VisualElement();
        eventPointsLayer.style.position = Position.Absolute;
        eventPointsLayer.style.left = 0;
        eventPointsLayer.style.top = 0;
        eventPointsLayer.style.right = 0;
        eventPointsLayer.style.bottom = 0;
        eventPointsLayer.style.marginTop = 1;
        eventPointsLayer.style.marginBottom = 1;
        eventPointsLayer.style.overflow = Overflow.Hidden;
        track.Add(eventPointsLayer);

        row.Add(track);

        playPauseButton = new Button(() =>
        {
            if (anima == null || !anima.IsValid) return;
            if (anima.IsPlaying && !anima.IsPaused) anima.Pause();
            else if (anima.IsPaused) anima.Resume();
        });
        playPauseButton.style.width = 21;
        playPauseButton.style.height = 21;
        playPauseButton.style.paddingLeft = 1;
        playPauseButton.style.paddingRight = 1;
        playPauseButton.style.paddingTop = 1;
        playPauseButton.style.paddingBottom = 1;
        playPauseButton.style.marginRight = 0;
        playPauseButton.style.backgroundSize = new BackgroundSize(16, 16);
        row.Add(playPauseButton);

        progressSection.Add(row);

        var infoRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginTop = 2, alignItems = Align.Center } };

        var leftGroup = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
        clipIcon = new Image { style = { width = 12, height = 12, marginRight = 3 } };
        leftGroup.Add(clipIcon);
        clipNameLabel = new Label
        {
            style = { color = new Color(0.6f, 0.6f, 0.6f), fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold }
        };
        leftGroup.Add(clipNameLabel);
        infoRow.Add(leftGroup);

        timeLabel = new Label
        {
            style = { unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.7f, 0.9f, 0.7f), fontSize = 10 }
        };
        infoRow.Add(timeLabel);

        progressSection.Add(infoRow);
        root.Add(progressSection);
    }

    private void OnUpdate()
    {
        if (anima == null) { updateTask?.Pause(); return; }

        bool hasValidState = Application.isPlaying && anima.IsValid;
        if (hasValidState != (progressSection.style.display == DisplayStyle.Flex))
            progressSection.style.display = hasValidState ? DisplayStyle.Flex : DisplayStyle.None;
        if (!hasValidState) return;

        bool isPlaying = hasValidState;
        bool isPaused = anima.IsPaused;
        float total = anima.TotalDuration;
        float current = anima.CurrentTime;
        float pct = total > 0f ? Mathf.Clamp01(current / total) * 100f : 0f;

        fill.style.width = Length.Percent(pct);
        marker.style.left = Length.Percent(pct);
        timeLabel.text = anima.IsLooping
            ? $"{current:F2}s / {total:F2}s (循环)"
            : $"{current:F2}s / {total:F2}s";

        string clipName = anima.CurrentClipName;
        if (clipName != lastClipName)
        {
            lastClipName = clipName;
            if (!string.IsNullOrEmpty(clipName))
            {
                clipNameLabel.text = clipName;
                if (cachedAnimIcon == null)
                    cachedAnimIcon = EditorGUIUtility.IconContent("AnimationClip Icon").image as Texture2D;
                if (cachedAnimIcon != null) clipIcon.image = cachedAnimIcon;
            }
            else
            {
                clipNameLabel.text = "";
                clipIcon.image = null;
            }
        }

        string iconName = (isPlaying && !isPaused) ? "PauseButton" : "PlayButton";
        if (iconName != lastBtnIconName)
        {
            lastBtnIconName = iconName;
            if (cachedBtnIcon == null)
                cachedBtnIcon = EditorGUIUtility.IconContent(iconName).image as Texture2D;
            else if (iconName == "PlayButton" || iconName == "PauseButton")
                cachedBtnIcon = EditorGUIUtility.IconContent(iconName).image as Texture2D;

            if (cachedBtnIcon != null) playPauseButton.style.backgroundImage = cachedBtnIcon;
            playPauseButton.tooltip = (isPlaying && !isPaused) ? "暂停" : isPaused ? "继续播放" : "播放";
        }

        DrawEventPoints(total);
    }

    private void DrawEventPoints(float totalDuration)
    {
        var points = anima.EventPoints;
        if (points == null || points.Count == 0 || totalDuration <= 0f)
        {
            eventPointsLayer.Clear();
            return;
        }

        if (points.Count == eventPointsLayer.childCount)
        {
            for (int i = 0; i < points.Count; i++)
                eventPointsLayer[i].style.left = Length.Percent(Mathf.Clamp01(points[i]) * 100f);
            return;
        }

        eventPointsLayer.Clear();
        for (int i = 0; i < points.Count; i++)
        {
            var line = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    width = 2,
                    top = 0,
                    bottom = 0,
                    left = Length.Percent(Mathf.Clamp01(points[i]) * 100f),
                    backgroundColor = EventPointColor
                }
            };
            eventPointsLayer.Add(line);
        }
    }
}

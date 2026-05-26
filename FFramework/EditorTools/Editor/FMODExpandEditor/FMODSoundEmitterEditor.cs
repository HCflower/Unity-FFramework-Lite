using UnityEditor.UIElements;
using UnityEngine.UIElements;
using FFramework.Utility;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(FMODSoundEmitter))]
public class FMODSoundEmitterEditor : Editor
{
    private FMODSoundEmitter emitter;

    private VisualElement progressSection;
    private VisualElement fill;
    private VisualElement marker;
    private VisualElement triggerPointsLayer;
    private Label timeLabel;
    private Image eventPathIcon;
    private Label eventPathLabel;
    private Button playPauseButton;

    // 缓存：避免每帧重复设置图像触发布局重算
    private Texture2D cachedBtnIcon;
    private Texture2D cachedAudioIcon;
    private string lastEventPath = "";
    private string lastBtnIconName = "";

    private IVisualElementScheduledItem updateTask;

    private static readonly Color FillColor = new Color(0f, 0.8f, 0f, 0.5f);
    private static readonly Color MarkerColor = new Color(0.1f, 0.9f, 0.4f, 1f);
    private static readonly Color TriggerColor = new Color(1f, 0.5f, 0f, 1f);

    public override VisualElement CreateInspectorGUI()
    {
        emitter = target as FMODSoundEmitter;
        var root = new VisualElement();

        // Header: FMOD Event
        root.Add(new Label("FMOD Event")
        {
            style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6, marginBottom = 2, marginLeft = 0 }
        });
        root.Add(new PropertyField(serializedObject.FindProperty("fmodEvent")));

        // Header: Playback Settings
        root.Add(new Label("Playback Settings")
        {
            style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10, marginBottom = 2, marginLeft = 0 }
        });
        root.Add(new PropertyField(serializedObject.FindProperty("is3D")));
        root.Add(new PropertyField(serializedObject.FindProperty("volume")));
        root.Add(new PropertyField(serializedObject.FindProperty("playOnAwake")));
        root.Add(new PropertyField(serializedObject.FindProperty("loop")));

        root.Bind(serializedObject);

        // 进度面板（与字段区域左右对齐）
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
            if (emitter == null) return;
            float w = track.resolvedStyle.width;
            if (w > 0f)
                emitter.PlaybackProgress = Mathf.Clamp01(evt.localPosition.x / w);
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

        triggerPointsLayer = new VisualElement();
        triggerPointsLayer.style.position = Position.Absolute;
        triggerPointsLayer.style.left = 0;
        triggerPointsLayer.style.top = 0;
        triggerPointsLayer.style.right = 0;
        triggerPointsLayer.style.bottom = 0;
        triggerPointsLayer.style.marginTop = 1;
        triggerPointsLayer.style.marginBottom = 1;
        triggerPointsLayer.style.overflow = Overflow.Hidden;
        track.Add(triggerPointsLayer);

        row.Add(track);

        playPauseButton = new Button(() =>
        {
            if (emitter == null || !emitter.IsValid) return;
            if (emitter.IsPlaying && !emitter.IsPaused) emitter.Pause();
            else if (emitter.IsPaused) emitter.Resume();
            else emitter.Play();
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
        eventPathIcon = new Image { style = { width = 12, height = 12, marginRight = 3 } };
        leftGroup.Add(eventPathIcon);
        eventPathLabel = new Label
        {
            style = { color = new Color(0.6f, 0.6f, 0.6f), fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold }
        };
        leftGroup.Add(eventPathLabel);
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
        if (emitter == null) { updateTask?.Pause(); return; }

        bool show = Application.isPlaying && emitter.IsValid;
        if (show != (progressSection.style.display == DisplayStyle.Flex))
            progressSection.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (!show) return;

        bool isPlaying = emitter.IsPlaying;
        bool isPaused = emitter.IsPaused;
        int totalMs = emitter.EventLengthMs;
        int currentMs = emitter.TimelinePositionMs;
        float pct = totalMs > 0 ? Mathf.Clamp01((float)currentMs / totalMs) * 100f : 0f;

        fill.style.width = Length.Percent(pct);
        marker.style.left = Length.Percent(pct);
        timeLabel.text = $"{FormatTime(currentMs)} / {FormatTime(totalMs)}";

        string path = emitter.CurrentEventPath;
        if (path != lastEventPath)
        {
            lastEventPath = path;
            if (!string.IsNullOrEmpty(path))
            {
                eventPathLabel.text = path;
                if (cachedAudioIcon == null)
                    cachedAudioIcon = EditorGUIUtility.IconContent("AudioClip Icon").image as Texture2D;
                if (cachedAudioIcon != null) eventPathIcon.image = cachedAudioIcon;
            }
            else
            {
                eventPathLabel.text = "";
                eventPathIcon.image = null;
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

        DrawTriggerPoints(totalMs);
    }

    private void DrawTriggerPoints(int totalMs)
    {
        var points = emitter.TriggerPoints;
        if (points == null || points.Count == 0 || totalMs <= 0)
        {
            triggerPointsLayer.Clear();
            return;
        }

        if (points.Count == triggerPointsLayer.childCount)
        {
            for (int i = 0; i < points.Count; i++)
                triggerPointsLayer[i].style.left = Length.Percent(CalcPct(points[i], totalMs));
            return;
        }

        triggerPointsLayer.Clear();
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
                    left = Length.Percent(CalcPct(points[i], totalMs)),
                    backgroundColor = TriggerColor
                }
            };
            triggerPointsLayer.Add(line);
        }
    }

    private static float CalcPct(TriggerPoint point, int totalMs)
    {
        if (point.mode == AudioPlayTriggerMode.Progress)
            return Mathf.Clamp01(point.value) * 100f;
        return Mathf.Clamp01(point.value * 1000f / totalMs) * 100f;
    }

    private static string FormatTime(int ms)
    {
        if (ms <= 0) return "00:00.000";
        return $"{(ms / 60000):D2}:{(ms % 60000 / 1000):D2}.{(ms % 1000):D3}";
    }
}

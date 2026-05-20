using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System.IO;

namespace FFramework.Editor
{
    /// <summary>
    /// 音频压缩页面：状态、逻辑与 UI 构建。
    /// </summary>
    internal class ResourceCompressionAudioPage
    {
        // ================================================================
        // 状态
        // ================================================================

        private List<AudioCacheData> audioCacheList = new List<AudioCacheData>();
        private AudioCompressionSettings audioSettings = new AudioCompressionSettings();
        private long cachedAudioTotalMemory = 0;

        // 折叠状态
        private bool showAudioList = true;
        private bool showAudioSettings = true;
        private bool showAudioCompressionSettings = true;

        // UI 引用
        public VisualElement audioListContainer { get; set; }
        public VisualElement audioPageContent { get; set; }

        // 回调
        public System.Action onStateChanged { get; set; }
        public System.Action onRefreshPage { get; set; }

        // 属性
        public int ItemCount => audioCacheList.Count;
        public long TotalMemory => cachedAudioTotalMemory;
        public string CompressButtonText => "开始音频压缩";

        // ================================================================
        // 页面构建
        // ================================================================

        public VisualElement CreateAudioCompressionPage()
        {
            var page = new VisualElement();
            page.Add(CreateDragAndDropArea());
            page.Add(CreateAudioSettingsSection());
            page.Add(CreateAudioCompressionSettingsSection());
            var listSection = BuildAudioListSection();
            listSection.name = "audioListContainer";
            audioListContainer = listSection;
            page.Add(listSection);
            return page;
        }

        public void RebuildAudioList()
        {
            if (audioPageContent == null) return;
            var oldContainer = audioPageContent.Q<VisualElement>("audioListContainer");
            if (oldContainer != null && oldContainer.parent != null)
            {
                var parent = oldContainer.parent;
                int index = parent.IndexOf(oldContainer);
                var newContainer = BuildAudioListSection();
                newContainer.name = "audioListContainer";
                parent.Remove(oldContainer);
                parent.Insert(index, newContainer);
                audioListContainer = newContainer;
            }
        }

        public void ClearAudioCache()
        {
            audioCacheList.Clear();
            cachedAudioTotalMemory = 0;
        }

        public void RecalculateAudioCacheMemory()
        {
            long total = 0;
            foreach (var cache in audioCacheList)
            {
                if (cache.audio == null) continue;
                cache.memoryUsage = CalculateAudioMemoryUsage(cache.audio);
                total += cache.memoryUsage;
            }
            cachedAudioTotalMemory = total;
        }

        // ================================================================
        // 拖放
        // ================================================================

        private void SetupDragAndDrop(VisualElement dropArea)
        {
            dropArea.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            });

            dropArea.RegisterCallback<DragPerformEvent>(evt =>
            {
                DragAndDrop.AcceptDrag();
                ProcessDroppedAudioObjects(DragAndDrop.objectReferences);
                evt.StopPropagation();
            });
        }

        private void ProcessDroppedAudioObjects(UnityEngine.Object[] droppedObjects)
        {
            int addedCount = 0;
            foreach (var obj in droppedObjects)
            {
                if (obj == null) continue;

                if (obj is AudioClip audio)
                {
                    if (AddAudioToCache(audio)) addedCount++;
                }
                else if (AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)))
                {
                    addedCount += AddAudioFromFolderPath(AssetDatabase.GetAssetPath(obj));
                }
            }
            if (addedCount > 0)
            {
                Debug.Log($"成功添加 {addedCount} 个音频");
                RebuildAudioList();
                onStateChanged?.Invoke();
            }
        }

        private bool AddAudioToCache(AudioClip audio)
        {
            if (audio == null) return false;
            string path = AssetDatabase.GetAssetPath(audio);
            if (string.IsNullOrEmpty(path)) return false;

            if (audioCacheList.Exists(a => a.path == path)) return false;

            var cache = new AudioCacheData
            {
                audio = audio,
                path = path,
                length = audio.length,
                frequency = audio.frequency,
                channels = audio.channels,
                memoryUsage = CalculateAudioMemoryUsage(audio),
                foldout = false
            };

            audioCacheList.Add(cache);
            cachedAudioTotalMemory += cache.memoryUsage;
            return true;
        }

        private int AddAudioFromFolderPath(string folderPath)
        {
            int addedCount = 0;
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folderPath });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                AudioClip audio = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (AddAudioToCache(audio)) addedCount++;
            }
            return addedCount;
        }

        // ================================================================
        // 音频逻辑
        // ================================================================

        public void ApplyAudioSettings()
        {
            if (audioCacheList.Count == 0)
            {
                EditorUtility.DisplayDialog("警告", "没有选择任何音频", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认应用设置",
                    $"确定要将设置应用到 {audioCacheList.Count} 个音频吗？", "确定", "取消"))
            {
                return;
            }

            int processedCount = 0;
            try
            {
                EditorUtility.DisplayProgressBar("应用设置", "正在处理音频设置...", 0f);
                for (int i = 0; i < audioCacheList.Count; i++)
                {
                    var cache = audioCacheList[i];
                    var audio = cache.audio;
                    if (audio == null) continue;

                    string path = cache.path;
                    if (AssetImporter.GetAtPath(path) is AudioImporter importer)
                    {
                        importer.forceToMono = audioSettings.forceToMono;
                        importer.ambisonic = audioSettings.ambisonic;

                        var sampleSettings = importer.defaultSampleSettings;
                        sampleSettings.preloadAudioData = audioSettings.preloadAudioData;
                        sampleSettings.loadType = audioSettings.loadType;
                        sampleSettings.compressionFormat = audioSettings.compressionFormat;
                        sampleSettings.quality = audioSettings.quality;
                        sampleSettings.sampleRateSetting = audioSettings.sampleRateSetting;
                        if (audioSettings.sampleRateSetting == AudioSampleRateSetting.OverrideSampleRate)
                        {
                            sampleSettings.sampleRateOverride = (uint)audioSettings.sampleRateOverride;
                        }
                        importer.defaultSampleSettings = sampleSettings;

                        if (audioSettings.overridePlatformSettings)
                        {
                            importer.SetOverrideSampleSettings(audioSettings.targetPlatform.ToString(), sampleSettings);
                        }
                        else
                        {
                            var defaultSettings = importer.defaultSampleSettings;
                            importer.SetOverrideSampleSettings(audioSettings.targetPlatform.ToString(), defaultSettings);
                        }

                        importer.SaveAndReimport();
                        processedCount++;
                    }

                    EditorUtility.DisplayProgressBar(
                        "应用设置",
                        $"正在处理 {Path.GetFileName(path)}...",
                        (float)(i + 1) / audioCacheList.Count);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            RecalculateAudioCacheMemory();
            AssetDatabase.Refresh();
            RebuildAudioList();
            onStateChanged?.Invoke();
            EditorUtility.DisplayDialog("完成", $"成功应用设置到 {processedCount} 个音频", "确定");
        }

        public void ResetAudioSettings()
        {
            audioSettings = new AudioCompressionSettings();
            onRefreshPage?.Invoke();
        }

        public void LoadSettingsFromFirstAudio()
        {
            if (audioCacheList.Count == 0) return;
            var first = audioCacheList[0];
            if (first.audio == null) return;

            string path = first.path;
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                var sampleSettings = importer.defaultSampleSettings;
                audioSettings.loadType = sampleSettings.loadType;
                audioSettings.compressionFormat = sampleSettings.compressionFormat;
                audioSettings.quality = sampleSettings.quality;
                audioSettings.sampleRateSetting = sampleSettings.sampleRateSetting;
                audioSettings.sampleRateOverride = (int)sampleSettings.sampleRateOverride;
                audioSettings.preloadAudioData = sampleSettings.preloadAudioData;
                audioSettings.forceToMono = importer.forceToMono;
                audioSettings.ambisonic = importer.ambisonic;
                onRefreshPage?.Invoke();
            }
        }

        private long CalculateAudioMemoryUsage(AudioClip audio)
        {
            if (audio == null) return 0;
            return audio.samples * audio.channels * 2;
        }

        // ================================================================
        // UI 构建
        // ================================================================

        private VisualElement CreateDragAndDropArea()
        {
            var section = ResourceCompressionUIHelper.CreateSection();

            var titleLabel = ResourceCompressionUIHelper.CreateSectionLabel("拖拽区域");
            section.Add(titleLabel);

            var helpBox = new HelpBox("将音频文件或文件夹拖拽到下方区域", HelpBoxMessageType.Info);
            section.Add(helpBox);

            var dropArea = new VisualElement();
            dropArea.style.minHeight = 50;
            dropArea.style.justifyContent = Justify.Center;
            dropArea.style.alignItems = Align.Center;
            dropArea.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            dropArea.style.borderTopWidth = 1;
            dropArea.style.borderBottomWidth = 1;
            dropArea.style.borderLeftWidth = 1;
            dropArea.style.borderRightWidth = 1;
            dropArea.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
            dropArea.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            dropArea.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
            dropArea.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
            dropArea.style.borderTopLeftRadius = 3;
            dropArea.style.borderTopRightRadius = 3;
            dropArea.style.borderBottomLeftRadius = 3;
            dropArea.style.borderBottomRightRadius = 3;
            dropArea.style.marginTop = 4;

            var dropLabel = new Label("拖拽音频或文件夹到这里");
            dropLabel.style.fontSize = 11;
            dropLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            dropLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            dropArea.Add(dropLabel);

            SetupDragAndDrop(dropArea);

            section.Add(dropArea);
            return section;
        }

        private VisualElement CreateAudioSettingsSection()
        {
            var section = ResourceCompressionUIHelper.CreateSection();

            var headerRow = ResourceCompressionUIHelper.CreateRow();
            var foldout = new Foldout();
            foldout.text = "音频设置";
            foldout.value = showAudioSettings;
            foldout.style.fontSize = 13;
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            foldout.style.flexGrow = 1;
            foldout.RegisterValueChangedCallback(evt => showAudioSettings = evt.newValue);
            headerRow.Add(foldout);

            var applyBtn = new Button(ApplyAudioSettings);
            applyBtn.text = "应用设置";
            applyBtn.style.height = 20;
            applyBtn.style.width = 80;
            applyBtn.style.fontSize = 11;
            applyBtn.style.marginLeft = 4;
            applyBtn.style.flexShrink = 0;
            headerRow.Add(applyBtn);
            section.Add(headerRow);

            var content = new VisualElement();
            content.style.display = showAudioSettings ? DisplayStyle.Flex : DisplayStyle.None;

            foldout.RegisterValueChangedCallback(evt =>
            {
                content.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            content.Add(ResourceCompressionUIHelper.CreateSectionLabel("基础设置", 10));
            content.Add(ResourceCompressionUIHelper.CreateEnumRow("加载类型:", audioSettings.loadType, newValue => audioSettings.loadType = (AudioClipLoadType)newValue));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("预加载数据:", audioSettings.preloadAudioData, newValue => audioSettings.preloadAudioData = newValue));

            content.Add(ResourceCompressionUIHelper.CreateSpacer(3));
            content.Add(ResourceCompressionUIHelper.CreateSectionLabel("高级设置", 10));
            content.Add(ResourceCompressionUIHelper.CreateEnumRow("压缩格式:", audioSettings.compressionFormat, newValue => audioSettings.compressionFormat = (AudioCompressionFormat)newValue));
            content.Add(ResourceCompressionUIHelper.CreateSliderRow("质量:", audioSettings.quality, 0.01f, 1f, newValue => audioSettings.quality = newValue));
            content.Add(ResourceCompressionUIHelper.CreateEnumRow("采样率设置:", audioSettings.sampleRateSetting, newValue =>
            {
                audioSettings.sampleRateSetting = (AudioSampleRateSetting)newValue;
                onStateChanged?.Invoke();
            }));
            if (audioSettings.sampleRateSetting == AudioSampleRateSetting.OverrideSampleRate)
            {
                content.Add(ResourceCompressionUIHelper.CreateIntFieldRow("采样率:", audioSettings.sampleRateOverride, newValue => audioSettings.sampleRateOverride = newValue));
            }

            content.Add(ResourceCompressionUIHelper.CreateSpacer(5));
            var btnRow = ResourceCompressionUIHelper.CreateRow();
            var resetBtn = new Button(ResetAudioSettings);
            resetBtn.text = "重置设置";
            resetBtn.style.height = 25;
            resetBtn.style.flexGrow = 1;
            resetBtn.style.marginRight = 2;
            btnRow.Add(resetBtn);

            var loadBtn = new Button(LoadSettingsFromFirstAudio);
            loadBtn.text = "从第一个读取";
            loadBtn.style.height = 25;
            loadBtn.style.flexGrow = 1;
            loadBtn.style.marginLeft = 2;
            btnRow.Add(loadBtn);
            content.Add(btnRow);

            section.Add(content);
            return section;
        }

        private VisualElement CreateAudioCompressionSettingsSection()
        {
            var section = ResourceCompressionUIHelper.CreateSection();

            var foldout = new Foldout();
            foldout.text = "压缩设置";
            foldout.value = showAudioCompressionSettings;
            foldout.style.fontSize = 13;
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            section.Add(foldout);

            var content = new VisualElement();
            content.style.display = showAudioCompressionSettings ? DisplayStyle.Flex : DisplayStyle.None;

            foldout.RegisterValueChangedCallback(evt =>
            {
                showAudioCompressionSettings = evt.newValue;
                content.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            content.Add(ResourceCompressionUIHelper.CreateSectionLabel("平台设置", 10));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("覆盖平台设置", audioSettings.overridePlatformSettings, newValue =>
            {
                audioSettings.overridePlatformSettings = newValue;
                onStateChanged?.Invoke();
            }));
            if (audioSettings.overridePlatformSettings)
            {
                content.Add(ResourceCompressionUIHelper.CreateEnumRow("目标平台:", audioSettings.targetPlatform, newValue => audioSettings.targetPlatform = (AudioPlatform)newValue));
            }

            content.Add(ResourceCompressionUIHelper.CreateSpacer(3));
            content.Add(ResourceCompressionUIHelper.CreateSectionLabel("压缩选项", 10));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("强制单声道", audioSettings.forceToMono, newValue => audioSettings.forceToMono = newValue));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("环绕声", audioSettings.ambisonic, newValue => audioSettings.ambisonic = newValue));

            section.Add(content);
            return section;
        }

        public VisualElement BuildAudioListSection()
        {
            var section = ResourceCompressionUIHelper.CreateSection();

            var headerRow = ResourceCompressionUIHelper.CreateRow();
            var foldout = new Foldout();
            foldout.text = "已选择的音频";
            foldout.value = showAudioList;
            foldout.style.fontSize = 13;
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            foldout.style.flexGrow = 1;
            foldout.RegisterValueChangedCallback(evt => showAudioList = evt.newValue);
            headerRow.Add(foldout);

            if (audioCacheList.Count > 0)
            {
                var countLabel = new Label($"({audioCacheList.Count}个)");
                countLabel.style.color = Color.gray;
                countLabel.style.fontSize = 10;
                countLabel.style.marginRight = 4;
                headerRow.Add(countLabel);

                float totalMemoryMB = cachedAudioTotalMemory / (1024f * 1024f);
                var memoryLabel = new Label($"{totalMemoryMB:0.0}MB");
                memoryLabel.style.fontSize = 10;
                memoryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                memoryLabel.style.marginRight = 4;
                memoryLabel.style.color = totalMemoryMB > 100 ? Color.red : (totalMemoryMB > 50 ? Color.yellow : Color.green);
                headerRow.Add(memoryLabel);

                var clearBtn = new Button(() =>
                {
                    if (EditorUtility.DisplayDialog("确认清空", "确定要清空所有音频吗？", "确定", "取消"))
                    {
                        ClearAudioCache();
                        RebuildAudioList();
                        onStateChanged?.Invoke();
                    }
                });
                clearBtn.text = "清空";
                clearBtn.style.height = 20;
                clearBtn.style.width = 50;
                clearBtn.style.fontSize = 11;
                headerRow.Add(clearBtn);
            }
            section.Add(headerRow);

            var content = new VisualElement();
            content.style.display = showAudioList ? DisplayStyle.Flex : DisplayStyle.None;

            foldout.RegisterValueChangedCallback(evt =>
            {
                content.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            if (audioCacheList.Count == 0)
            {
                content.Add(new HelpBox("请添加音频文件", HelpBoxMessageType.Info));
            }
            else
            {
                content.Add(BuildAudioItems());
            }

            section.Add(content);
            return section;
        }

        private VisualElement BuildAudioItems()
        {
            var container = new VisualElement();
            Texture2D audioIcon = EditorGUIUtility.IconContent("AudioClip Icon").image as Texture2D;
            Texture2D closeIcon = EditorGUIUtility.IconContent("d_winbtn_mac_close_h@2x").image as Texture2D;

            for (int i = 0; i < audioCacheList.Count; i++)
            {
                var cache = audioCacheList[i];
                if (cache.audio == null) continue;

                int index = i;
                var item = new VisualElement();
                ResourceCompressionUIHelper.ApplyCacheItemStyle(item);

                // 按钮行：单击定位资源
                var mainRow = new Button(() => EditorGUIUtility.PingObject(cache.audio));
                mainRow.style.flexDirection = FlexDirection.Row;
                mainRow.style.alignItems = Align.Center;
                mainRow.style.minHeight = 28;
                mainRow.style.paddingLeft = 4;
                mainRow.style.paddingRight = 0;
                mainRow.style.justifyContent = Justify.FlexStart;
                mainRow.style.unityTextAlign = TextAnchor.MiddleLeft;
                mainRow.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
                mainRow.style.borderLeftWidth = 0;
                mainRow.style.borderRightWidth = 0;
                mainRow.style.borderTopWidth = 0;
                mainRow.style.borderBottomWidth = 0;
                mainRow.style.flexGrow = 1;

                float memoryMB = cache.memoryUsage / (1024f * 1024f);

                var iconImage = new Image();
                if (audioIcon != null)
                    iconImage.image = audioIcon;
                iconImage.style.width = 20;
                iconImage.style.height = 20;
                iconImage.style.marginLeft = 4;
                iconImage.style.marginRight = 4;
                iconImage.style.flexShrink = 0;
                mainRow.Add(iconImage);

                var nameLabel = new Label(cache.audio.name);
                nameLabel.style.fontSize = 12;
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                nameLabel.style.flexGrow = 1;
                nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                nameLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
                mainRow.Add(nameLabel);

                var memLabel = new Label($"{memoryMB:0.0}MB");
                memLabel.style.fontSize = 10;
                memLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                memLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                memLabel.style.color = memoryMB > 20 ? Color.red : (memoryMB > 10 ? Color.yellow : Color.green);
                memLabel.style.width = 50;
                memLabel.style.flexShrink = 0;
                mainRow.Add(memLabel);

                // 移除按钮（使用 d_winbtn_mac_close_h@2x 图标）
                var removeBtn = new Button(() =>
                {
                    cachedAudioTotalMemory -= cache.memoryUsage;
                    audioCacheList.RemoveAt(index);
                    RebuildAudioList();
                    onStateChanged?.Invoke();
                });
                if (closeIcon != null)
                    removeBtn.style.backgroundImage = closeIcon;
                removeBtn.text = "";
                removeBtn.style.width = 20;
                removeBtn.style.height = 20;
                removeBtn.style.marginLeft = 4;
                removeBtn.style.marginRight = 2;
                removeBtn.style.flexShrink = 0;
                removeBtn.style.backgroundSize = new BackgroundSize(15, 15);
                removeBtn.style.borderLeftWidth = 0;
                removeBtn.style.borderRightWidth = 0;
                removeBtn.style.borderTopWidth = 0;
                removeBtn.style.borderBottomWidth = 0;
                mainRow.Add(removeBtn);

                item.Add(mainRow);
                container.Add(item);
            }

            return container;
        }
    }
}

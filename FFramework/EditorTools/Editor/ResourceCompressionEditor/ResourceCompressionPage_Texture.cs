using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System.IO;
using UnityEngine.Profiling;

namespace FFramework.Editor
{
    /// <summary>
    /// 纹理压缩页面：状态、逻辑与 UI 构建。
    /// </summary>
    internal class ResourceCompressionTexturePage
    {
        // ================================================================
        // 状态
        // ================================================================

        private List<TextureCacheData> textureCacheList = new List<TextureCacheData>();
        private TextureCompressionSettings textureSettings = new TextureCompressionSettings();
        private TextureSettings texturePreSettings = new TextureSettings();
        private long cachedTextureTotalMemory = 0;

        // 折叠状态
        private bool showTextureList = true;
        private bool showCompressionSettings = true;
        private bool showTextureSettings = true;

        // UI 引用
        public VisualElement textureListContainer { get; set; }
        public VisualElement texturePageContent { get; set; }

        // 回调
        public System.Action<CompressionResult> onCompressionComplete { get; set; }
        public System.Action onStateChanged { get; set; }
        public System.Action onRefreshPage { get; set; }

        // 属性
        public int ItemCount => textureCacheList.Count;
        public long TotalMemory => cachedTextureTotalMemory;
        public string CompressButtonText => "开始图片压缩";

        // ================================================================
        // 页面构建
        // ================================================================

        public VisualElement CreateTextureCompressionPage()
        {
            var page = new VisualElement();
            page.Add(CreateDragAndDropArea());
            page.Add(CreateTextureSettingsSection());
            page.Add(CreateCompressionSettingsSection());
            var listSection = BuildTextureListSection();
            listSection.name = "textureListContainer";
            textureListContainer = listSection;
            page.Add(listSection);
            return page;
        }

        public void RebuildTextureList()
        {
            if (texturePageContent == null) return;
            var oldContainer = texturePageContent.Q<VisualElement>("textureListContainer");
            if (oldContainer != null && oldContainer.parent != null)
            {
                var parent = oldContainer.parent;
                int index = parent.IndexOf(oldContainer);
                var newContainer = BuildTextureListSection();
                newContainer.name = "textureListContainer";
                parent.Remove(oldContainer);
                parent.Insert(index, newContainer);
                textureListContainer = newContainer;
            }
        }

        public void ClearTextureCache()
        {
            textureCacheList.Clear();
            cachedTextureTotalMemory = 0;
        }

        public void RecalculateTextureCacheMemory()
        {
            long total = 0;
            foreach (var cache in textureCacheList)
            {
                if (cache.texture == null) continue;
                cache.memoryUsage = CalculateTextureMemoryUsage(cache.texture);
                total += cache.memoryUsage;
            }
            cachedTextureTotalMemory = total;
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
                ProcessDroppedObjects(DragAndDrop.objectReferences);
                evt.StopPropagation();
            });
        }

        private void ProcessDroppedObjects(UnityEngine.Object[] droppedObjects)
        {
            int addedCount = 0;
            foreach (var obj in droppedObjects)
            {
                if (obj == null) continue;

                if (obj is Texture2D texture)
                {
                    if (AddTextureToCache(texture)) addedCount++;
                }
                else if (AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)))
                {
                    addedCount += AddTexturesFromFolderPath(AssetDatabase.GetAssetPath(obj));
                }
            }
            if (addedCount > 0)
            {
                Debug.Log($"成功添加 {addedCount} 个图片");
                RebuildTextureList();
                onStateChanged?.Invoke();
            }
        }

        private bool AddTextureToCache(Texture2D texture)
        {
            if (texture == null) return false;
            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path)) return false;

            if (textureCacheList.Exists(t => t.path == path)) return false;

            var cache = new TextureCacheData
            {
                texture = texture,
                path = path,
                width = texture.width,
                height = texture.height,
                format = texture.format,
                mipmapCount = texture.mipmapCount,
                memoryUsage = CalculateTextureMemoryUsage(texture),
                foldout = false
            };

            textureCacheList.Add(cache);
            cachedTextureTotalMemory += cache.memoryUsage;
            return true;
        }

        private int AddTexturesFromFolderPath(string folderPath)
        {
            int addedCount = 0;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (AddTextureToCache(texture)) addedCount++;
            }
            return addedCount;
        }

        // ================================================================
        // 纹理逻辑
        // ================================================================

        public void ApplyTextureSettings()
        {
            if (textureCacheList.Count == 0)
            {
                EditorUtility.DisplayDialog("警告", "没有选择任何图片", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认应用设置",
                    $"确定要将设置应用到 {textureCacheList.Count} 张图片吗？", "确定", "取消"))
            {
                return;
            }

            int processedCount = 0;
            try
            {
                EditorUtility.DisplayProgressBar("应用设置", "正在处理图片设置...", 0f);
                for (int i = 0; i < textureCacheList.Count; i++)
                {
                    var cache = textureCacheList[i];
                    var texture = cache.texture;
                    if (texture == null) continue;

                    string path = cache.path;
                    if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                    {
                        importer.textureType = texturePreSettings.textureType;
                        importer.textureShape = texturePreSettings.textureShape;
                        importer.sRGBTexture = texturePreSettings.sRGBTexture;
                        importer.alphaSource = texturePreSettings.alphaSource;
                        importer.npotScale = texturePreSettings.nonPowerOf2;
                        importer.isReadable = texturePreSettings.readable;
                        importer.streamingMipmaps = texturePreSettings.streamingMipMaps;
                        importer.filterMode = texturePreSettings.filterMode;
                        importer.anisoLevel = texturePreSettings.anisoLevel;
                        importer.wrapMode = texturePreSettings.wrapMode;
                        importer.SaveAndReimport();
                        processedCount++;
                    }

                    EditorUtility.DisplayProgressBar(
                        "应用设置",
                        $"正在处理 {Path.GetFileName(path)}...",
                        (float)(i + 1) / textureCacheList.Count);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            RecalculateTextureCacheMemory();
            AssetDatabase.Refresh();
            RebuildTextureList();
            onStateChanged?.Invoke();
            EditorUtility.DisplayDialog("完成", $"成功应用设置到 {processedCount} 张图片", "确定");
        }

        public void ResetTextureSettings()
        {
            texturePreSettings = new TextureSettings();
            onRefreshPage?.Invoke();
        }

        public void LoadSettingsFromFirstTexture()
        {
            if (textureCacheList.Count == 0) return;
            var first = textureCacheList[0];
            if (first.texture == null) return;

            string path = first.path;
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                texturePreSettings.textureType = importer.textureType;
                texturePreSettings.textureShape = importer.textureShape;
                texturePreSettings.sRGBTexture = importer.sRGBTexture;
                texturePreSettings.alphaSource = importer.alphaSource;
                texturePreSettings.nonPowerOf2 = importer.npotScale;
                texturePreSettings.readable = importer.isReadable;
                texturePreSettings.streamingMipMaps = importer.streamingMipmaps;
                texturePreSettings.filterMode = importer.filterMode;
                texturePreSettings.anisoLevel = importer.anisoLevel;
                texturePreSettings.wrapMode = importer.wrapMode;
                onRefreshPage?.Invoke();
            }
        }

        private long CalculateTextureMemoryUsage(Texture2D texture)
        {
            if (texture == null) return 0;
            return Profiler.GetRuntimeMemorySizeLong(texture);
        }

        // ================================================================
        // 压缩
        // ================================================================

        public void CompressSelectedTextures()
        {
            if (textureCacheList.Count == 0) return;

            long originalTotal = 0;
            long compressedTotal = 0;
            int processed = 0;

            try
            {
                EditorUtility.DisplayProgressBar("纹理压缩", "处理中...", 0f);
                for (int i = 0; i < textureCacheList.Count; i++)
                {
                    var cache = textureCacheList[i];
                    var tex = cache.texture;
                    if (tex == null) continue;

                    string path = cache.path;
                    if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                    {
                        originalTotal += CalculateTextureMemoryUsage(tex);

                        importer.maxTextureSize = textureSettings.maxTextureSize;
                        importer.compressionQuality = Mathf.RoundToInt(textureSettings.compressionQuality);
                        importer.mipmapEnabled = textureSettings.generateMipMaps;
                        importer.textureCompression = textureSettings.compressionMode;

                        if (textureSettings.forceCompression &&
                            importer.textureCompression == TextureImporterCompression.Uncompressed)
                        {
                            importer.textureCompression = TextureImporterCompression.Compressed;
                        }

                        if (textureSettings.overridePlatformSettings)
                        {
                            ApplyPlatformTextureSetting(importer, "Standalone");
                            ApplyPlatformTextureSetting(importer, "Android");
                            ApplyPlatformTextureSetting(importer, "iPhone");
                        }
                        else
                        {
                            importer.ClearPlatformTextureSettings("Standalone");
                            importer.ClearPlatformTextureSettings("Android");
                            importer.ClearPlatformTextureSettings("iPhone");
                        }

                        importer.SaveAndReimport();

                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        var newTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        compressedTotal += CalculateTextureMemoryUsage(newTex);

                        cache.texture = newTex;
                        cache.memoryUsage = CalculateTextureMemoryUsage(newTex);

                        processed++;
                    }

                    EditorUtility.DisplayProgressBar(
                        "纹理压缩",
                        $"正在处理 {Path.GetFileName(path)}",
                        (float)(i + 1) / textureCacheList.Count);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            RecalculateTextureCacheMemory();

            var result = new CompressionResult
            {
                originalSize = originalTotal / (1024f * 1024f),
                compressedSize = compressedTotal / (1024f * 1024f),
                processedCount = processed
            };

            onCompressionComplete?.Invoke(result);
            RebuildTextureList();
            onStateChanged?.Invoke();
        }

        private void ApplyPlatformTextureSetting(TextureImporter importer, string platformName)
        {
            var ps = importer.GetPlatformTextureSettings(platformName);
            ps.overridden = true;
            ps.maxTextureSize = textureSettings.maxTextureSize;
            ps.compressionQuality = Mathf.RoundToInt(textureSettings.compressionQuality);
            ps.format = TextureImporterFormat.Automatic;
            importer.SetPlatformTextureSettings(ps);
        }

        // ================================================================
        // UI 构建
        // ================================================================

        private VisualElement CreateDragAndDropArea()
        {
            var section = ResourceCompressionUIHelper.CreateSection();

            var titleLabel = ResourceCompressionUIHelper.CreateSectionLabel("拖拽区域");
            section.Add(titleLabel);

            var helpBox = new HelpBox("将图片文件或文件夹拖拽到下方区域", HelpBoxMessageType.Info);
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

            var dropLabel = new Label("拖拽图片或文件夹到这里");
            dropLabel.style.fontSize = 11;
            dropLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            dropLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            dropArea.Add(dropLabel);

            SetupDragAndDrop(dropArea);

            section.Add(dropArea);
            return section;
        }

        private VisualElement CreateTextureSettingsSection()
        {
            var section = ResourceCompressionUIHelper.CreateSection();

            var headerRow = ResourceCompressionUIHelper.CreateRow();
            var foldout = new Foldout();
            foldout.text = "图片设置";
            foldout.value = showTextureSettings;
            foldout.style.fontSize = 13;
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            foldout.style.flexGrow = 1;
            foldout.RegisterValueChangedCallback(evt =>
            {
                showTextureSettings = evt.newValue;
            });
            headerRow.Add(foldout);

            var applyBtn = new Button(ApplyTextureSettings);
            applyBtn.text = "应用设置";
            applyBtn.style.height = 20;
            applyBtn.style.width = 80;
            applyBtn.style.fontSize = 11;
            applyBtn.style.marginLeft = 4;
            applyBtn.style.flexShrink = 0;
            headerRow.Add(applyBtn);
            section.Add(headerRow);

            var content = new VisualElement();
            content.style.display = showTextureSettings ? DisplayStyle.Flex : DisplayStyle.None;

            foldout.RegisterValueChangedCallback(evt =>
            {
                content.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            content.Add(ResourceCompressionUIHelper.CreateSectionLabel("基础设置", 10));
            content.Add(ResourceCompressionUIHelper.CreateEnumRow("纹理类型:", texturePreSettings.textureType, newValue => texturePreSettings.textureType = (TextureImporterType)newValue));
            content.Add(ResourceCompressionUIHelper.CreateEnumRow("纹理形状:", texturePreSettings.textureShape, newValue => texturePreSettings.textureShape = (TextureImporterShape)newValue));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("sRGB (颜色纹理)", texturePreSettings.sRGBTexture, newValue => texturePreSettings.sRGBTexture = newValue));
            content.Add(ResourceCompressionUIHelper.CreateEnumRow("Alpha 来源", texturePreSettings.alphaSource, newValue => texturePreSettings.alphaSource = (TextureImporterAlphaSource)newValue));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("Alpha是否透明", texturePreSettings.alphaIsTransparency, newValue => texturePreSettings.alphaIsTransparency = newValue));
#if UNITY_2020_1_OR_NEWER
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("Alpha预乘", texturePreSettings.alphaPremultiply, newValue => texturePreSettings.alphaPremultiply = newValue));
#endif

            content.Add(ResourceCompressionUIHelper.CreateSpacer(3));
            content.Add(ResourceCompressionUIHelper.CreateSectionLabel("高级设置", 10));
            content.Add(ResourceCompressionUIHelper.CreateEnumRow("非2次幂", texturePreSettings.nonPowerOf2, newValue => texturePreSettings.nonPowerOf2 = (TextureImporterNPOTScale)newValue));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("可读写", texturePreSettings.readable, newValue => texturePreSettings.readable = newValue));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("流式MipMaps", texturePreSettings.streamingMipMaps, newValue => texturePreSettings.streamingMipMaps = newValue));
            content.Add(ResourceCompressionUIHelper.CreateEnumRow("过滤模式:", texturePreSettings.filterMode, newValue => texturePreSettings.filterMode = (FilterMode)newValue));
            content.Add(ResourceCompressionUIHelper.CreateSliderRow("各向异性:", texturePreSettings.anisoLevel, 0, 16, newValue => texturePreSettings.anisoLevel = Mathf.RoundToInt(newValue)));

            content.Add(ResourceCompressionUIHelper.CreateSpacer(3));
            content.Add(ResourceCompressionUIHelper.CreateSectionLabel("包装设置", 10));
            content.Add(ResourceCompressionUIHelper.CreateEnumRow("包装模式:", texturePreSettings.wrapMode, newValue => texturePreSettings.wrapMode = (TextureWrapMode)newValue));

            content.Add(ResourceCompressionUIHelper.CreateSpacer(5));
            var btnRow = ResourceCompressionUIHelper.CreateRow();
            var resetBtn = new Button(ResetTextureSettings);
            resetBtn.text = "重置设置";
            resetBtn.style.height = 25;
            resetBtn.style.flexGrow = 1;
            resetBtn.style.marginRight = 2;
            btnRow.Add(resetBtn);

            var loadBtn = new Button(LoadSettingsFromFirstTexture);
            loadBtn.text = "从第一张读取";
            loadBtn.style.height = 25;
            loadBtn.style.flexGrow = 1;
            loadBtn.style.marginLeft = 2;
            btnRow.Add(loadBtn);
            content.Add(btnRow);

            section.Add(content);
            return section;
        }

        private VisualElement CreateCompressionSettingsSection()
        {
            var section = ResourceCompressionUIHelper.CreateSection();

            var foldout = new Foldout();
            foldout.text = "压缩设置";
            foldout.value = showCompressionSettings;
            foldout.style.fontSize = 13;
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            section.Add(foldout);

            var content = new VisualElement();
            content.style.display = showCompressionSettings ? DisplayStyle.Flex : DisplayStyle.None;

            foldout.RegisterValueChangedCallback(evt =>
            {
                showCompressionSettings = evt.newValue;
                content.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            content.Add(ResourceCompressionUIHelper.CreateSectionLabel("基础设置", 10));
            content.Add(ResourceCompressionUIHelper.CreateIntFieldRow("最大尺寸:", textureSettings.maxTextureSize, newValue => textureSettings.maxTextureSize = newValue));
            content.Add(ResourceCompressionUIHelper.CreateSliderRow("压缩质量:", textureSettings.compressionQuality, 0f, 100f, newValue => textureSettings.compressionQuality = newValue));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("生成MipMaps", textureSettings.generateMipMaps, newValue => textureSettings.generateMipMaps = newValue));

            content.Add(ResourceCompressionUIHelper.CreateSpacer(3));
            content.Add(ResourceCompressionUIHelper.CreateSectionLabel("高级设置", 10));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("强制压缩", textureSettings.forceCompression, newValue => textureSettings.forceCompression = newValue));
            content.Add(ResourceCompressionUIHelper.CreateEnumRow("压缩模式:", textureSettings.compressionMode, newValue => textureSettings.compressionMode = (TextureImporterCompression)newValue));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("覆盖平台设置", textureSettings.overridePlatformSettings, newValue => textureSettings.overridePlatformSettings = newValue));

            section.Add(content);
            return section;
        }

        public VisualElement BuildTextureListSection()
        {
            var section = ResourceCompressionUIHelper.CreateSection();

            var headerRow = ResourceCompressionUIHelper.CreateRow();
            var foldout = new Foldout();
            foldout.text = "已选择的图片";
            foldout.value = showTextureList;
            foldout.style.fontSize = 13;
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            foldout.style.flexGrow = 1;
            foldout.RegisterValueChangedCallback(evt => showTextureList = evt.newValue);
            headerRow.Add(foldout);

            if (textureCacheList.Count > 0)
            {
                var countLabel = new Label($"({textureCacheList.Count}个)");
                countLabel.style.color = Color.gray;
                countLabel.style.fontSize = 10;
                countLabel.style.marginRight = 4;
                headerRow.Add(countLabel);

                float totalMemoryMB = cachedTextureTotalMemory / (1024f * 1024f);
                var memoryLabel = new Label($"{totalMemoryMB:0.0}MB");
                memoryLabel.style.fontSize = 10;
                memoryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                memoryLabel.style.marginRight = 4;
                memoryLabel.style.color = totalMemoryMB > 50 ? Color.red : (totalMemoryMB > 20 ? Color.yellow : Color.green);
                headerRow.Add(memoryLabel);

                var clearBtn = new Button(() =>
                {
                    if (EditorUtility.DisplayDialog("确认清空", "确定要清空所有图片吗?", "确定", "取消"))
                    {
                        ClearTextureCache();
                        RebuildTextureList();
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
            content.style.display = showTextureList ? DisplayStyle.Flex : DisplayStyle.None;

            foldout.RegisterValueChangedCallback(evt =>
            {
                content.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            if (textureCacheList.Count == 0)
            {
                content.Add(new HelpBox("请添加图片文件", HelpBoxMessageType.Info));
            }
            else
            {
                content.Add(BuildTextureItems());
            }

            section.Add(content);
            return section;
        }

        private VisualElement BuildTextureItems()
        {
            var container = new VisualElement();
            Texture2D closeIcon = EditorGUIUtility.IconContent("d_winbtn_mac_close_h@2x").image as Texture2D;

            for (int i = 0; i < textureCacheList.Count; i++)
            {
                var cache = textureCacheList[i];
                if (cache.texture == null) continue;

                int index = i;
                var item = new VisualElement();
                ResourceCompressionUIHelper.ApplyCacheItemStyle(item);

                // 按钮行：单击定位资源
                var mainRow = new Button(() => EditorGUIUtility.PingObject(cache.texture));
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

                var previewImage = new Image();
                Texture2D previewTex = AssetPreview.GetAssetPreview(cache.texture) ??
                                       (EditorGUIUtility.IconContent("Texture Icon").image as Texture2D);
                if (previewTex != null)
                    previewImage.image = previewTex;
                previewImage.style.width = 20;
                previewImage.style.height = 20;
                previewImage.style.marginLeft = 4;
                previewImage.style.marginRight = 4;
                previewImage.style.flexShrink = 0;
                mainRow.Add(previewImage);

                var nameLabel = new Label(cache.texture.name);
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
                memLabel.style.color = memoryMB > 10 ? Color.red : (memoryMB > 5 ? Color.yellow : Color.green);
                memLabel.style.width = 50;
                memLabel.style.flexShrink = 0;
                mainRow.Add(memLabel);

                // 移除按钮（使用 d_winbtn_mac_close_h@2x 图标）
                var removeBtn = new Button(() =>
                {
                    cachedTextureTotalMemory -= cache.memoryUsage;
                    textureCacheList.RemoveAt(index);
                    RebuildTextureList();
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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System.IO;
using UnityEngine.Profiling;

namespace FFramework.Editor
{
    /// <summary>
    /// 模型压缩页面：状态、逻辑与 UI 构建。
    /// </summary>
    internal class ResourceCompressionModelPage
    {
        // ================================================================
        // 状态
        // ================================================================

        private List<ModelCacheData> modelCacheList = new List<ModelCacheData>();
        private ModelCompressionSettings modelSettings = new ModelCompressionSettings();
        private long cachedModelTotalMemory = 0;

        // 折叠状态
        private bool showModelList = true;
        private bool showModelSettings = true;
        private bool showModelCompressionSettings = true;

        // UI 引用
        public VisualElement modelListContainer { get; set; }
        public VisualElement modelPageContent { get; set; }

        // 回调
        public System.Action<CompressionResult> onCompressionComplete { get; set; }
        public System.Action onStateChanged { get; set; }
        public System.Action onRefreshPage { get; set; }

        // 属性
        public int ItemCount => modelCacheList.Count;
        public long TotalMemory => cachedModelTotalMemory;
        public string CompressButtonText => "开始模型压缩";

        // ================================================================
        // 页面构建
        // ================================================================

        public VisualElement CreateModelCompressionPage()
        {
            var page = new VisualElement();
            page.Add(CreateModelDragAndDropArea());
            page.Add(CreateModelSettingsSection());
            page.Add(CreateModelCompressionSettingsSection());
            var listSection = BuildModelListSection();
            listSection.name = "modelListContainer";
            modelListContainer = listSection;
            page.Add(listSection);
            return page;
        }

        public void RebuildModelList()
        {
            if (modelPageContent == null) return;
            var oldContainer = modelPageContent.Q<VisualElement>("modelListContainer");
            if (oldContainer != null && oldContainer.parent != null)
            {
                var parent = oldContainer.parent;
                int index = parent.IndexOf(oldContainer);
                var newContainer = BuildModelListSection();
                newContainer.name = "modelListContainer";
                parent.Remove(oldContainer);
                parent.Insert(index, newContainer);
                modelListContainer = newContainer;
            }
        }

        public void ClearModelCache()
        {
            modelCacheList.Clear();
            cachedModelTotalMemory = 0;
        }

        public void RecalculateModelCacheMemory()
        {
            long total = 0;
            foreach (var cache in modelCacheList)
            {
                if (cache.model == null) continue;
                cache.memoryUsage = CalculateModelMemoryUsage(cache.model);
                total += cache.memoryUsage;
            }
            cachedModelTotalMemory = total;
        }

        // ================================================================
        // 拖放
        // ================================================================

        private void SetupModelDragAndDrop(VisualElement dropArea)
        {
            dropArea.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            });

            dropArea.RegisterCallback<DragPerformEvent>(evt =>
            {
                DragAndDrop.AcceptDrag();
                ProcessDroppedModels(DragAndDrop.objectReferences);
                evt.StopPropagation();
            });
        }

        private void ProcessDroppedModels(UnityEngine.Object[] droppedObjects)
        {
            int addedCount = 0;
            foreach (var obj in droppedObjects)
            {
                if (obj == null) continue;
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    addedCount += AddModelsFromFolderPath(path);
                }
                else
                {
                    string ext = Path.GetExtension(path).ToLower();
                    if (ext == ".fbx" || ext == ".obj" || ext == ".blend" ||
                        ext == ".dae" || ext == ".3ds" || ext == ".dxf" ||
                        ext == ".max" || ext == ".ma" || ext == ".mb")
                    {
                        if (AddModelToCache(path)) addedCount++;
                    }
                }
            }
            if (addedCount > 0)
            {
                Debug.Log($"成功添加 {addedCount} 个模型");
                RebuildModelList();
                onStateChanged?.Invoke();
            }
        }

        private bool AddModelToCache(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (modelCacheList.Exists(m => m.path == path)) return false;

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) return false;

            int totalVerts = 0;
            int totalTris = 0;
            int meshCount = 0;
            bool hasAnim = false;
            bool hasAvatar = false;

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                hasAnim = importer.importAnimation;
                hasAvatar = importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar;
            }

            foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (subAsset is Mesh mesh)
                {
                    totalVerts += mesh.vertexCount;
                    totalTris += mesh.triangles.Length / 3;
                    meshCount++;
                }
            }

            long memoryUsage = CalculateModelMemoryUsage(model);

            var cache = new ModelCacheData
            {
                model = model,
                path = path,
                memoryUsage = memoryUsage,
                foldout = false,
                vertexCount = totalVerts,
                triangleCount = totalTris,
                meshCount = meshCount,
                hasAnimation = hasAnim,
                hasAvatar = hasAvatar
            };

            modelCacheList.Add(cache);
            cachedModelTotalMemory += memoryUsage;
            return true;
        }

        private int AddModelsFromFolderPath(string folderPath)
        {
            int addedCount = 0;
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AddModelToCache(assetPath)) addedCount++;
            }
            return addedCount;
        }

        private long CalculateModelMemoryUsage(GameObject model)
        {
            if (model == null) return 0;
            long total = 0;
            foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(model)))
            {
                if (subAsset is Mesh mesh)
                {
                    total += Profiler.GetRuntimeMemorySizeLong(mesh);
                }
            }
            return total;
        }

        // ================================================================
        // 模型逻辑
        // ================================================================

        public void ApplyModelSettings()
        {
            if (modelCacheList.Count == 0)
            {
                EditorUtility.DisplayDialog("警告", "没有选择任何模型", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认应用设置",
                    $"确定要将设置应用到 {modelCacheList.Count} 个模型吗？", "确定", "取消"))
            {
                return;
            }

            int processedCount = 0;
            try
            {
                EditorUtility.DisplayProgressBar("应用设置", "正在处理模型设置...", 0f);
                for (int i = 0; i < modelCacheList.Count; i++)
                {
                    var cache = modelCacheList[i];
                    string path = cache.path;
                    if (AssetImporter.GetAtPath(path) is ModelImporter importer)
                    {
                        importer.meshCompression = modelSettings.meshCompression;
                        importer.isReadable = modelSettings.isReadable;
                        importer.optimizeMeshPolygons = modelSettings.optimizeMeshPolygons;
                        importer.optimizeMeshVertices = modelSettings.optimizeMeshVertices;
                        importer.importBlendShapes = modelSettings.importBlendShapes;
                        importer.addCollider = modelSettings.addCollider;

                        importer.importAnimation = modelSettings.importAnimation;
                        importer.animationCompression = modelSettings.animationCompression;
                        importer.animationRotationError = modelSettings.animationRotationError;
                        importer.animationPositionError = modelSettings.animationPositionError;
                        importer.animationScaleError = modelSettings.animationScaleError;

                        importer.avatarSetup = modelSettings.avatarSetup;
                        importer.materialImportMode = modelSettings.materialImportMode;

                        importer.SaveAndReimport();
                        processedCount++;
                    }

                    EditorUtility.DisplayProgressBar(
                        "应用设置",
                        $"正在处理 {Path.GetFileName(path)}...",
                        (float)(i + 1) / modelCacheList.Count);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            RecalculateModelCacheMemory();
            AssetDatabase.Refresh();
            RebuildModelList();
            onStateChanged?.Invoke();
            EditorUtility.DisplayDialog("完成", $"成功应用设置到 {processedCount} 个模型", "确定");
        }

        public void ResetModelSettings()
        {
            modelSettings = new ModelCompressionSettings();
            onRefreshPage?.Invoke();
        }

        public void LoadSettingsFromFirstModel()
        {
            if (modelCacheList.Count == 0) return;
            var first = modelCacheList[0];
            string path = first.path;
            if (AssetImporter.GetAtPath(path) is ModelImporter importer)
            {
                modelSettings.meshCompression = importer.meshCompression;
                modelSettings.isReadable = importer.isReadable;
                modelSettings.optimizeMeshPolygons = importer.optimizeMeshPolygons;
                modelSettings.optimizeMeshVertices = importer.optimizeMeshVertices;
                modelSettings.importBlendShapes = importer.importBlendShapes;
                modelSettings.addCollider = importer.addCollider;
                modelSettings.importAnimation = importer.importAnimation;
                modelSettings.animationCompression = importer.animationCompression;
                modelSettings.animationRotationError = importer.animationRotationError;
                modelSettings.animationPositionError = importer.animationPositionError;
                modelSettings.animationScaleError = importer.animationScaleError;
                modelSettings.avatarSetup = importer.avatarSetup;
                modelSettings.materialImportMode = importer.materialImportMode;
                onRefreshPage?.Invoke();
            }
        }

        // ================================================================
        // 压缩
        // ================================================================

        public void CompressSelectedModels()
        {
            if (modelCacheList.Count == 0) return;

            long originalTotal = 0;
            long compressedTotal = 0;
            int processed = 0;

            try
            {
                EditorUtility.DisplayProgressBar("模型压缩", "处理中...", 0f);
                for (int i = 0; i < modelCacheList.Count; i++)
                {
                    var cache = modelCacheList[i];
                    string path = cache.path;
                    if (AssetImporter.GetAtPath(path) is ModelImporter importer)
                    {
                        originalTotal += cache.memoryUsage;

                        importer.meshCompression = modelSettings.meshCompression;
                        importer.optimizeMeshPolygons = modelSettings.optimizeMeshPolygons;
                        importer.optimizeMeshVertices = modelSettings.optimizeMeshVertices;
                        importer.isReadable = modelSettings.isReadable;
                        importer.importBlendShapes = modelSettings.importBlendShapes;
                        importer.addCollider = modelSettings.addCollider;

                        importer.importAnimation = modelSettings.importAnimation;
                        importer.animationCompression = modelSettings.animationCompression;
                        importer.animationRotationError = modelSettings.animationRotationError;
                        importer.animationPositionError = modelSettings.animationPositionError;
                        importer.animationScaleError = modelSettings.animationScaleError;

                        importer.SaveAndReimport();

                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        cache.model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        long newMemory = CalculateModelMemoryUsage(cache.model);
                        compressedTotal += newMemory;
                        cache.memoryUsage = newMemory;

                        processed++;
                    }

                    EditorUtility.DisplayProgressBar(
                        "模型压缩",
                        $"正在处理 {Path.GetFileName(path)}",
                        (float)(i + 1) / modelCacheList.Count);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            RecalculateModelCacheMemory();

            var result = new CompressionResult
            {
                originalSize = originalTotal / (1024f * 1024f),
                compressedSize = compressedTotal / (1024f * 1024f),
                processedCount = processed
            };

            onCompressionComplete?.Invoke(result);
            RebuildModelList();
            onStateChanged?.Invoke();
        }

        // ================================================================
        // UI 构建
        // ================================================================

        private VisualElement CreateModelDragAndDropArea()
        {
            var section = ResourceCompressionUIHelper.CreateSection();

            var titleLabel = ResourceCompressionUIHelper.CreateSectionLabel("拖拽区域");
            section.Add(titleLabel);

            var helpBox = new HelpBox("将模型文件(.fbx/.obj等)或文件夹拖拽到下方区域", HelpBoxMessageType.Info);
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

            var dropLabel = new Label("拖拽模型或文件夹到这里");
            dropLabel.style.fontSize = 11;
            dropLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            dropLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            dropArea.Add(dropLabel);

            SetupModelDragAndDrop(dropArea);

            section.Add(dropArea);
            return section;
        }

        private VisualElement CreateModelSettingsSection()
        {
            var section = ResourceCompressionUIHelper.CreateSection();

            var headerRow = ResourceCompressionUIHelper.CreateRow();
            var foldout = new Foldout();
            foldout.text = "模型设置";
            foldout.value = showModelSettings;
            foldout.style.fontSize = 13;
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            foldout.style.flexGrow = 1;
            foldout.RegisterValueChangedCallback(evt => showModelSettings = evt.newValue);
            headerRow.Add(foldout);

            var applyBtn = new Button(ApplyModelSettings);
            applyBtn.text = "应用设置";
            applyBtn.style.height = 20;
            applyBtn.style.width = 80;
            applyBtn.style.fontSize = 11;
            applyBtn.style.marginLeft = 4;
            applyBtn.style.flexShrink = 0;
            headerRow.Add(applyBtn);
            section.Add(headerRow);

            var content = new VisualElement();
            content.style.display = showModelSettings ? DisplayStyle.Flex : DisplayStyle.None;

            foldout.RegisterValueChangedCallback(evt =>
            {
                content.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            content.Add(ResourceCompressionUIHelper.CreateSectionLabel("基础设置", 10));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("可读写", modelSettings.isReadable, newValue => modelSettings.isReadable = newValue));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("优化网格多边形", modelSettings.optimizeMeshPolygons, newValue => modelSettings.optimizeMeshPolygons = newValue));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("优化网格顶点", modelSettings.optimizeMeshVertices, newValue => modelSettings.optimizeMeshVertices = newValue));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("导入混合形状", modelSettings.importBlendShapes, newValue => modelSettings.importBlendShapes = newValue));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("生成碰撞体", modelSettings.addCollider, newValue => modelSettings.addCollider = newValue));

            content.Add(ResourceCompressionUIHelper.CreateSpacer(3));
            content.Add(ResourceCompressionUIHelper.CreateSectionLabel("骨骼设置", 10));
            content.Add(ResourceCompressionUIHelper.CreateEnumRow("骨骼设置:", modelSettings.avatarSetup, newValue => modelSettings.avatarSetup = (ModelImporterAvatarSetup)newValue));

            content.Add(ResourceCompressionUIHelper.CreateSpacer(3));
            content.Add(ResourceCompressionUIHelper.CreateSectionLabel("材质设置", 10));
            content.Add(ResourceCompressionUIHelper.CreateEnumRow("材质导入模式:", modelSettings.materialImportMode, newValue => modelSettings.materialImportMode = (ModelImporterMaterialImportMode)newValue));

            content.Add(ResourceCompressionUIHelper.CreateSpacer(5));
            var btnRow = ResourceCompressionUIHelper.CreateRow();
            var resetBtn = new Button(ResetModelSettings);
            resetBtn.text = "重置设置";
            resetBtn.style.height = 25;
            resetBtn.style.flexGrow = 1;
            resetBtn.style.marginRight = 2;
            btnRow.Add(resetBtn);

            var loadBtn = new Button(LoadSettingsFromFirstModel);
            loadBtn.text = "从第一个读取";
            loadBtn.style.height = 25;
            loadBtn.style.flexGrow = 1;
            loadBtn.style.marginLeft = 2;
            btnRow.Add(loadBtn);
            content.Add(btnRow);

            section.Add(content);
            return section;
        }

        private VisualElement CreateModelCompressionSettingsSection()
        {
            var section = ResourceCompressionUIHelper.CreateSection();

            var foldout = new Foldout();
            foldout.text = "压缩设置";
            foldout.value = showModelCompressionSettings;
            foldout.style.fontSize = 13;
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            section.Add(foldout);

            var content = new VisualElement();
            content.style.display = showModelCompressionSettings ? DisplayStyle.Flex : DisplayStyle.None;

            foldout.RegisterValueChangedCallback(evt =>
            {
                showModelCompressionSettings = evt.newValue;
                content.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            content.Add(ResourceCompressionUIHelper.CreateSectionLabel("网格压缩", 10));
            content.Add(ResourceCompressionUIHelper.CreateEnumRow("网格压缩级别:", modelSettings.meshCompression, newValue => modelSettings.meshCompression = (ModelImporterMeshCompression)newValue));

            content.Add(ResourceCompressionUIHelper.CreateSpacer(3));
            content.Add(ResourceCompressionUIHelper.CreateSectionLabel("动画压缩", 10));
            content.Add(ResourceCompressionUIHelper.CreateToggleRow("导入动画", modelSettings.importAnimation, newValue => modelSettings.importAnimation = newValue));
            content.Add(ResourceCompressionUIHelper.CreateEnumRow("动画压缩模式:", modelSettings.animationCompression, newValue => modelSettings.animationCompression = (ModelImporterAnimationCompression)newValue));
            content.Add(ResourceCompressionUIHelper.CreateSliderRow("旋转误差:", modelSettings.animationRotationError, 0f, 1f, newValue => modelSettings.animationRotationError = newValue));
            content.Add(ResourceCompressionUIHelper.CreateSliderRow("位置误差:", modelSettings.animationPositionError, 0f, 1f, newValue => modelSettings.animationPositionError = newValue));
            content.Add(ResourceCompressionUIHelper.CreateSliderRow("缩放误差:", modelSettings.animationScaleError, 0f, 1f, newValue => modelSettings.animationScaleError = newValue));

            section.Add(content);
            return section;
        }

        public VisualElement BuildModelListSection()
        {
            var section = ResourceCompressionUIHelper.CreateSection();

            var headerRow = ResourceCompressionUIHelper.CreateRow();
            var foldout = new Foldout();
            foldout.text = "已选择的模型";
            foldout.value = showModelList;
            foldout.style.fontSize = 13;
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            foldout.style.flexGrow = 1;
            foldout.RegisterValueChangedCallback(evt => showModelList = evt.newValue);
            headerRow.Add(foldout);

            if (modelCacheList.Count > 0)
            {
                var countLabel = new Label($"({modelCacheList.Count}个)");
                countLabel.style.color = Color.gray;
                countLabel.style.fontSize = 10;
                countLabel.style.marginRight = 4;
                headerRow.Add(countLabel);

                float totalMemoryMB = cachedModelTotalMemory / (1024f * 1024f);
                var memoryLabel = new Label($"{totalMemoryMB:0.0}MB");
                memoryLabel.style.fontSize = 10;
                memoryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                memoryLabel.style.marginRight = 4;
                memoryLabel.style.color = totalMemoryMB > 100 ? Color.red : (totalMemoryMB > 50 ? Color.yellow : Color.green);
                headerRow.Add(memoryLabel);

                var clearBtn = new Button(() =>
                {
                    if (EditorUtility.DisplayDialog("确认清空", "确定要清空所有模型吗？", "确定", "取消"))
                    {
                        ClearModelCache();
                        RebuildModelList();
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
            content.style.display = showModelList ? DisplayStyle.Flex : DisplayStyle.None;

            foldout.RegisterValueChangedCallback(evt =>
            {
                content.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            if (modelCacheList.Count == 0)
            {
                content.Add(new HelpBox("请添加模型文件", HelpBoxMessageType.Info));
            }
            else
            {
                content.Add(BuildModelItems());
            }

            section.Add(content);
            return section;
        }

        private VisualElement BuildModelItems()
        {
            var container = new VisualElement();
            Texture2D modelIcon = EditorGUIUtility.IconContent("ModelImporter Icon").image as Texture2D;
            Texture2D closeIcon = EditorGUIUtility.IconContent("d_winbtn_mac_close_h@2x").image as Texture2D;

            for (int i = 0; i < modelCacheList.Count; i++)
            {
                var cache = modelCacheList[i];
                if (cache.model == null) continue;

                int index = i;
                var item = new VisualElement();
                ResourceCompressionUIHelper.ApplyCacheItemStyle(item);

                // 按钮行：单击定位资源
                var mainRow = new Button(() => EditorGUIUtility.PingObject(cache.model));
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
                Texture2D previewTex = AssetPreview.GetAssetPreview(cache.model) ?? modelIcon;
                if (previewTex != null)
                    iconImage.image = previewTex;
                iconImage.style.width = 20;
                iconImage.style.height = 20;
                iconImage.style.marginLeft = 4;
                iconImage.style.marginRight = 4;
                iconImage.style.flexShrink = 0;
                mainRow.Add(iconImage);

                var nameLabel = new Label(cache.model.name);
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
                    cachedModelTotalMemory -= cache.memoryUsage;
                    modelCacheList.RemoveAt(index);
                    RebuildModelList();
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
                removeBtn.style.backgroundSize = new BackgroundSize(15,15);
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

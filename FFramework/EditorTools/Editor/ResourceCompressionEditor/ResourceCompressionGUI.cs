using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System.Collections.Generic;

namespace FFramework.Editor
{
    /// <summary>
    /// 资源压缩工具的主入口。协调纹理/音频/模型三个独立页面的生命周期，
    /// 提供导航、页脚和页面切换功能。
    /// </summary>
    public static class ResourceCompressionGUI
    {
        /// <summary>
        /// 为 FFrameworkEditor 等外部容器提供内嵌的资源压缩视图。
        /// </summary>
        public static VisualElement CreateCompressionView()
        {
            // ================================================================
            // 1. 共享状态
            // ================================================================

            CompressionPage currentPage = CompressionPage.Texture;
            ScrollView contentScrollView = null;
            CompressionResult lastResult = null;
            Button compressButton = null;
            VisualElement footerResultSection = null;

            // ================================================================
            // 2. 创建三个独立页面实例
            // ================================================================

            var texturePage = new ResourceCompressionTexturePage();
            var audioPage = new ResourceCompressionAudioPage();
            var modelPage = new ResourceCompressionModelPage();

            // ================================================================
            // 3. 公共回调
            // ================================================================

            System.Action updateFooterState = () =>
            {
                if (compressButton == null) return;

                bool isEnabled = false;
                switch (currentPage)
                {
                    case CompressionPage.Texture:
                        compressButton.text = texturePage.CompressButtonText;
                        isEnabled = texturePage.ItemCount > 0;
                        break;
                    case CompressionPage.Audio:
                        compressButton.text = audioPage.CompressButtonText;
                        isEnabled = audioPage.ItemCount > 0;
                        break;
                    case CompressionPage.Model:
                        compressButton.text = modelPage.CompressButtonText;
                        isEnabled = modelPage.ItemCount > 0;
                        break;
                }
                compressButton.SetEnabled(isEnabled);

                if (footerResultSection != null)
                {
                    footerResultSection.style.display = lastResult != null ? DisplayStyle.Flex : DisplayStyle.None;
                }
            };

            System.Action updateFooterResult = () =>
            {
                if (footerResultSection == null) return;

                footerResultSection.Clear();

                if (lastResult != null)
                {
                    footerResultSection.style.display = DisplayStyle.Flex;

                    var resultHeader = ResourceCompressionUIHelper.CreateRow();
                    var resultTitle = new Label("最近压缩结果");
                    resultTitle.style.fontSize = 13;
                    resultTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                    resultTitle.style.color = new Color(0.2f, 0.5f, 0.9f);
                    resultTitle.style.width = 110;
                    resultHeader.Add(resultTitle);

                    var resultText = new Label($"压缩前: {lastResult.originalSize:0.0}MB → 压缩后: {lastResult.compressedSize:0.0}MB");
                    resultText.style.fontSize = 11;
                    resultText.style.color = new Color(0.85f, 0.85f, 0.85f);
                    resultHeader.Add(resultText);
                    footerResultSection.Add(resultHeader);

                    var savedRow = ResourceCompressionUIHelper.CreateRow();
                    var spacer = new VisualElement();
                    spacer.style.width = 110;
                    savedRow.Add(spacer);

                    float savedMemory = lastResult.originalSize - lastResult.compressedSize;
                    float compressionRatio = lastResult.originalSize > 0 ? (savedMemory / lastResult.originalSize) * 100 : 0;
                    var savedText = new Label($"节省: {savedMemory:0.0}MB ({compressionRatio:0.0}%)    处理: {lastResult.processedCount} 个");
                    savedText.style.fontSize = 10;
                    savedText.style.unityFontStyleAndWeight = FontStyle.Bold;
                    savedText.style.color = savedMemory > 0 ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.5f, 0.5f, 0.5f);
                    savedRow.Add(savedText);
                    footerResultSection.Add(savedRow);

                    footerResultSection.Add(ResourceCompressionUIHelper.CreateSpacer(2));
                }
                else
                {
                    footerResultSection.style.display = DisplayStyle.None;
                }
            };

            System.Action refreshCurrentPage = () =>
            {
                if (contentScrollView == null) return;
                contentScrollView.Clear();

                switch (currentPage)
                {
                    case CompressionPage.Texture:
                        texturePage.texturePageContent = texturePage.CreateTextureCompressionPage();
                        contentScrollView.Add(texturePage.texturePageContent);
                        break;
                    case CompressionPage.Audio:
                        audioPage.audioPageContent = audioPage.CreateAudioCompressionPage();
                        contentScrollView.Add(audioPage.audioPageContent);
                        break;
                    case CompressionPage.Model:
                        modelPage.modelPageContent = modelPage.CreateModelCompressionPage();
                        contentScrollView.Add(modelPage.modelPageContent);
                        break;
                }
            };

            // 压缩完成回调
            System.Action<CompressionResult> onCompressionComplete = (result) =>
            {
                lastResult = result;
                ShowResultDialog(result);
                updateFooterResult();
                updateFooterState();
            };

            // 状态变更回调（传递给各页面）
            System.Action onStateChanged = () =>
            {
                updateFooterState();
            };

            // 注册页面回调
            texturePage.onCompressionComplete = onCompressionComplete;
            texturePage.onStateChanged = onStateChanged;
            texturePage.onRefreshPage = refreshCurrentPage;

            audioPage.onStateChanged = onStateChanged;
            audioPage.onRefreshPage = refreshCurrentPage;

            modelPage.onCompressionComplete = onCompressionComplete;
            modelPage.onStateChanged = onStateChanged;
            modelPage.onRefreshPage = refreshCurrentPage;

            // ================================================================
            // 4. 核心方法
            // ================================================================

            void StartCompression()
            {
                switch (currentPage)
                {
                    case CompressionPage.Texture:
                        texturePage.CompressSelectedTextures();
                        break;
                    case CompressionPage.Audio:
                        audioPage.ApplyAudioSettings();
                        audioPage.RecalculateAudioCacheMemory();
                        break;
                    case CompressionPage.Model:
                        modelPage.CompressSelectedModels();
                        break;
                }
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("提示", "所有资源已保存！", "确定");
            }

            void ShowResultDialog(CompressionResult result)
            {
                if (result == null) return;
                string message = $"压缩完成！\n\n" +
                                 $"处理资源: {result.processedCount} 个\n" +
                                 $"压缩前内存: {result.originalSize:0.00} MB\n" +
                                 $"压缩后内存: {result.compressedSize:0.00} MB\n" +
                                 $"节省内存: {result.originalSize - result.compressedSize:0.00} MB";
                EditorUtility.DisplayDialog("压缩结果", message, "确定");
            }

            // ================================================================
            // 5. UI 构建方法
            // ================================================================

            VisualElement CreateHeader()
            {
                var header = new VisualElement();
                header.style.justifyContent = Justify.Center;
                header.style.alignItems = Align.Center;
                header.style.paddingTop = 6;
                header.style.paddingBottom = 6;
                header.style.marginBottom = 4;

                var title = new Label("资源压缩工具");
                title.style.fontSize = 16;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.unityTextAlign = TextAnchor.MiddleCenter;
                title.style.color = new Color(0.9f, 0.9f, 0.9f);
                title.style.height = 32;
                header.Add(title);

                return header;
            }

            VisualElement CreateNavigation()
            {
                var nav = ResourceCompressionUIHelper.CreateSection();
                nav.style.marginBottom = 4;

                var row = ResourceCompressionUIHelper.CreateRow();
                var label = new Label("功能模块:");
                label.style.fontSize = 12;
                label.style.width = 60;
                label.style.color = new Color(0.85f, 0.85f, 0.85f);
                row.Add(label);

                string[] pageNames = { "图片压缩", "音频压缩", "模型压缩" };

                var popup = new PopupField<string>(new List<string>(pageNames), pageNames[(int)currentPage]);
                popup.style.flexGrow = 1;
                popup.style.height = 20;
                popup.RegisterValueChangedCallback(evt =>
                {
                    int newIndex = System.Array.IndexOf(pageNames, evt.newValue);
                    if (newIndex >= 0)
                    {
                        currentPage = (CompressionPage)newIndex;
                        refreshCurrentPage();
                        updateFooterState();
                    }
                });
                row.Add(popup);

                nav.Add(row);
                return nav;
            }

            VisualElement CreateFooter()
            {
                var section = ResourceCompressionUIHelper.CreateSection();

                footerResultSection = new VisualElement();
                footerResultSection.style.display = DisplayStyle.None;

                section.Add(footerResultSection);

                compressButton = new Button(StartCompression);
                compressButton.text = "开始压缩";
                compressButton.style.height = 26;
                compressButton.style.fontSize = 13;
                compressButton.style.unityFontStyleAndWeight = FontStyle.Bold;
                compressButton.style.unityTextAlign = TextAnchor.MiddleCenter;
                compressButton.style.marginTop = 2;
                compressButton.style.marginBottom = 2;
                compressButton.style.marginLeft = 2;
                compressButton.style.marginRight = 2;
                section.Add(compressButton);

                return section;
            }

            // ================================================================
            // 6. 构建 UI 树
            // ================================================================

            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.paddingLeft = 4;
            root.style.paddingRight = 4;
            root.style.paddingTop = 2;

            root.Add(CreateHeader());
            root.Add(CreateNavigation());

            contentScrollView = new ScrollView();
            contentScrollView.style.flexGrow = 1;
            root.Add(contentScrollView);

            root.Add(CreateFooter());

            refreshCurrentPage();
            updateFooterState();

            return root;
        }
    }
}

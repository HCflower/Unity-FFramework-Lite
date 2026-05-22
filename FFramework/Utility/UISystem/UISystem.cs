// =============================================================
// 描述：UI系统管理器（集成UIRoot功能）
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：1.0.3
// 修改记录：修复缓存面板层级不一致、数据清理不彻底、错误处理等问题
// =============================================================
using System.Collections.Generic;
using UnityEngine.EventSystems;
using FFramework.Core;
using UnityEngine.UI;
using UnityEngine;
using System.Linq;

namespace FFramework.Utility
{
    [AddComponentMenu("FFramework/UI/UISystem")]
    public class UISystem : SingletonMono<UISystem>
    {
        #region UI层级引用

        [Header("UI Layers")]
        public Transform BackgroundLayer;       // 背景层
        public Transform PostProcessingLayer;   // 后期处理层
        public Transform ContentLayer;          // 内容层
        public Transform PopupLayer;            // 弹窗层
        public Transform GuideLayer;            // 引导层    
        public Transform DebugLayer;            // 调试层   

        #endregion

        #region 私有字段

        // 资源加载器接口
        private IUIResLoader uiResLoader = new ResourcesUILoader();

        // 缓存的UI面板字典（所有创建过的面板）
        private Dictionary<string, UIPanel> cachedPanels = new Dictionary<string, UIPanel>();

        // 当前活跃的面板栈（按打开顺序）
        private Stack<UIPanel> activeStack = new Stack<UIPanel>();

        // 每个层级的活跃面板列表
        private Dictionary<UILayer, List<UIPanel>> layerPanels = new Dictionary<UILayer, List<UIPanel>>();

        #endregion

        #region 属性

        /// <summary>当前打开的UI面板数量</summary>
        public int OpenPanelCount => activeStack.Count;

        /// <summary>缓存的UI面板数量</summary>
        public int CachedPanelCount => cachedPanels.Count;

        /// <summary>是否有打开的面板</summary>
        public bool HasOpenPanels => activeStack.Count > 0;

        /// <summary>获取当前栈顶面板</summary>
        public UIPanel CurrentPanel => activeStack.Count > 0 ? activeStack.Peek() : null;

        /// <summary>获取当前面板名称</summary>
        public string CurrentPanelName => CurrentPanel?.name ?? "无";

        /// <summary>获取当前面板类型名</summary>
        public string CurrentPanelTypeName => CurrentPanel?.GetType().Name ?? "无";

        #endregion

        #region Unity生命周期

        protected override void InitializeSingleton()
        {
            // 1. 确保UI根节点环境（Canvas, EventSystem, Layers）存在
            SetupUIRoot();

            // 2. 初始化层级面板列表
            foreach (UILayer layer in System.Enum.GetValues(typeof(UILayer)))
            {
                if (!layerPanels.ContainsKey(layer))
                {
                    layerPanels[layer] = new List<UIPanel>();
                }
            }

            Debug.Log("[UISystem] UI系统初始化完成");
        }

        protected override void OnDestroy()
        {
            ClearAllPanels(false);
            base.OnDestroy();
        }

        #endregion

        #region UI根节点设置 (原UIRoot逻辑)

        [ContextMenu("初始化UI根节点")]
        public void SetupUIRoot()
        {
            // 确保自身名称
            if (gameObject.name != "UISystem") gameObject.name = "UISystem";

            // 1. 添加必要组件
            if (!TryGetComponent<Canvas>(out _))
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            if (!TryGetComponent<CanvasScaler>(out _))
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); // 默认参考分辨率
                scaler.matchWidthOrHeight = 0.5f;
            }
            if (!TryGetComponent<GraphicRaycaster>(out _)) gameObject.AddComponent<GraphicRaycaster>();

            // 2. 创建UI层级
            BackgroundLayer = CreateAndAddUIlayerInGameObject("BackgroundLayer", this.transform);
            PostProcessingLayer = CreateAndAddUIlayerInGameObject("PostProcessingLayer", this.transform);
            ContentLayer = CreateAndAddUIlayerInGameObject("ContentLayer", this.transform);
            PopupLayer = CreateAndAddUIlayerInGameObject("PopupLayer", this.transform);
            GuideLayer = CreateAndAddUIlayerInGameObject("GuideLayer", this.transform);
            DebugLayer = CreateAndAddUIlayerInGameObject("DebugLayer", this.transform);

            // 3. 检查并创建 EventSystem
            if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.transform.SetParent(this.transform);
                esObj.transform.localPosition = Vector3.zero;
                esObj.transform.localRotation = Quaternion.identity;
                esObj.transform.localScale = Vector3.one;
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }
        }

        /// <summary>
        /// 创建并添加UI层
        /// </summary>
        private Transform CreateAndAddUIlayerInGameObject(string uiLayerName, Transform parent)
        {
            // 优先查找已存在的层级
            Transform exist = parent.Find(uiLayerName);
            if (exist != null)
            {
                // 确保它是全屏拉伸的
                var rect = exist as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
                return exist;
            }

            // 创建新层级
            GameObject uiLayer = new GameObject(uiLayerName, typeof(RectTransform));
            var rectTransform = uiLayer.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);

            // 设置全屏拉伸
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            // 重置变换
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;

            // 这里的层级需要不阻挡射线，除非挂载了Graphic
            // 通常层级节点本身不需要Image组件，所以默认是透明且穿透的

            return rectTransform;
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 设置自定义UI资源加载器
        /// </summary>
        /// <param name="loader">实现IUIResLoader接口的加载器</param>
        public void SetUIResLoader(IUIResLoader loader)
        {
            uiResLoader = loader ?? new ResourcesUILoader();
        }

        private Transform GetUILayer(UILayer layer)
        {
            // 直接返回本地引用
            switch (layer)
            {
                case UILayer.BackgroundLayer: return BackgroundLayer;
                case UILayer.PostProcessingLayer: return PostProcessingLayer;
                case UILayer.ContentLayer: return ContentLayer;
                case UILayer.GuideLayer: return GuideLayer;
                case UILayer.PopupLayer: return PopupLayer;
                case UILayer.DebugLayer: return DebugLayer;
                default: return ContentLayer;
            }
        }

        private bool ShouldLockPreviousPanel(UILayer layer)
        {
            // 通常Content和Popup都需要锁定前一个，只有特效层不需要
            return layer != UILayer.PostProcessingLayer && layer != UILayer.BackgroundLayer;
        }

        private T CreatePanel<T>(string panelName, UILayer layer, GameObject prefab) where T : UIPanel
        {
            Transform layerTransform = GetUILayer(layer);
            if (layerTransform == null)
            {
                // 如果层级为空，尝试重新初始化
                Debug.LogWarning($"[UISystem] 层级 {layer} 未初始化，尝试重新设置UI根节点");
                SetupUIRoot();
                layerTransform = GetUILayer(layer);
                if (layerTransform == null)
                {
                    Debug.LogError($"[UISystem] 无法获取或创建UI层级: {layer}");
                    return null;
                }
            }

            GameObject panelObject;
            if (prefab != null)
            {
                panelObject = Instantiate(prefab, layerTransform);
            }
            else
            {
                // 修改点：使用接口加载资源，并添加更详细的错误信息
                GameObject prefabRes = null;
                try
                {
                    prefabRes = uiResLoader.LoadPanelPrefab(panelName);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[UISystem] 加载UI预制体时发生异常: {panelName} - {ex.Message}");
                    return null;
                }

                if (prefabRes == null)
                {
                    Debug.LogError($"[UISystem] 无法加载UI预制体: {panelName}，请检查资源路径或资源是否存在");
                    return null;
                }
                panelObject = Instantiate(prefabRes, layerTransform);
            }

            panelObject.name = panelName;

            T panel = panelObject.GetComponent<T>();
            if (panel == null)
            {
                Debug.LogError($"[UISystem] 预制体 {panelName} 缺少 {typeof(T).Name} 组件");
                Destroy(panelObject);
                return null;
            }

            return panel;
        }

        private void RemovePanelFromStack(UIPanel targetPanel)
        {
            if (targetPanel == null) return;

            var tempStack = new Stack<UIPanel>();
            bool found = false;

            while (activeStack.Count > 0)
            {
                var panel = activeStack.Pop();
                if (panel == targetPanel)
                {
                    found = true;
                    break;
                }
                tempStack.Push(panel);
            }

            while (tempStack.Count > 0)
            {
                activeStack.Push(tempStack.Pop());
            }

            if (!found)
            {
                // Debug.LogWarning($"[UISystem] 面板 {targetPanel.GetType().Name} 不在活跃栈中");
            }
        }

        private GameObject FindChildRecursive(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child.gameObject;

                var result = FindChildRecursive(child, childName);
                if (result != null) return result;
            }
            return null;
        }

        #endregion

        #region 核心面板管理

        /// <summary>
        /// 从Resources加载UI面板
        /// </summary>
        public T OpenPanel<T>(UILayer layer = UILayer.ContentLayer, bool useCache = true) where T : UIPanel
        {
            string panelName = typeof(T).Name;
            return OpenPanelInternal<T>(panelName, layer, useCache, null);
        }

        /// <summary>
        /// 从预制体加载UI面板
        /// </summary>
        public T OpenPanel<T>(GameObject prefab, UILayer layer = UILayer.ContentLayer, bool useCache = true) where T : UIPanel
        {
            if (prefab == null)
            {
                Debug.LogError("[UISystem] 预制体不能为空");
                return null;
            }
            return OpenPanelInternal<T>(prefab.name, layer, useCache, prefab);
        }

        /// <summary>
        /// 关闭当前顶层面板
        /// </summary>
        public void CloseCurrentPanel()
        {
            if (activeStack.Count == 0)
            {
                Debug.LogWarning("[UISystem] 没有打开的面板可以关闭");
                return;
            }

            var currentPanel = activeStack.Pop();
            ClosePanelInternal(currentPanel);
        }

        /// <summary>
        /// 关闭指定类型的面板
        /// </summary>
        public void ClosePanel<T>() where T : UIPanel
        {
            string panelName = typeof(T).Name;

            if (cachedPanels.TryGetValue(panelName, out UIPanel panel) &&
                panel.gameObject.activeInHierarchy)
            {
                RemovePanelFromStack(panel);
                ClosePanelInternal(panel);
            }
            else
            {
                Debug.LogWarning($"[UISystem] 面板 {panelName} 未打开或不存在");
            }
        }

        /// <summary>
        /// 获取指定类型的面板（如果存在且活跃）
        /// </summary>
        public T GetPanel<T>() where T : UIPanel
        {
            string panelName = typeof(T).Name;
            if (cachedPanels.TryGetValue(panelName, out UIPanel panel) &&
                panel.gameObject.activeInHierarchy)
            {
                return panel as T;
            }
            return null;
        }

        /// <summary>
        /// 获取当前栈顶面板（指定类型）
        /// </summary>
        public T GetTopPanel<T>() where T : UIPanel
        {
            return CurrentPanel as T;
        }

        /// <summary>
        /// 检查当前面板是否为指定类型
        /// </summary>
        public bool IsCurrentPanel<T>() where T : UIPanel
        {
            return CurrentPanel is T;
        }

        #endregion

        #region 面板批量管理

        /// <summary>
        /// 清理所有UI面板
        /// </summary>
        public void ClearAllPanels(bool destroyGameObjects = true)
        {
            // 收集并去重所有面板
            var allPanels = new HashSet<UIPanel>();

            // 从栈中取出
            while (activeStack.Count > 0)
            {
                var p = activeStack.Pop();
                if (p != null) allPanels.Add(p);
            }

            // 从缓存中取出
            foreach (var kvp in cachedPanels)
            {
                if (kvp.Value != null) allPanels.Add(kvp.Value);
            }

            // 从各层级中取出
            foreach (var list in layerPanels.Values)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    if (p != null) allPanels.Add(p);
                }
            }

            // 统一处理面板
            foreach (var panel in allPanels)
            {
                try
                {
                    // 修复：添加空值检查
                    if (panel != null)
                    {
                        panel.Close();
                    }

                    if (panel != null && panel.gameObject != null)
                    {
                        if (destroyGameObjects)
                        {
                            // 避免重复销毁
                            if (panel.gameObject)
                                Destroy(panel.gameObject);
                        }
                        else
                        {
                            // 非销毁情况下禁用对象以避免残留交互
                            panel.gameObject.SetActive(false);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[UISystem] 清理面板异常: {panel?.GetType().Name} - {ex.Message}");
                }
            }

            // 彻底清空所有容器
            cachedPanels.Clear();
            foreach (var layer in layerPanels.Keys)
            {
                layerPanels[layer].Clear();
            }

            // 修复：添加栈的清理确认
            activeStack.Clear();

            Debug.Log("[UISystem] 清理所有UI面板完成");
        }

        /// <summary>
        /// 清理指定层级的所有面板
        /// </summary>
        public void ClearPanelsInLayer(UILayer layer, bool destroyGameObjects = true)
        {
            Debug.Log($"<color=orange>[UISystem] 清理层级面板</color>: {layer}");

            if (!layerPanels.ContainsKey(layer)) return;

            var list = layerPanels[layer];
            var panelsToRemove = new List<UIPanel>(list);

            // 去重避免与缓存/栈重复处理
            var processed = new HashSet<UIPanel>();

            foreach (var panel in panelsToRemove)
            {
                if (panel == null || processed.Contains(panel)) continue;
                processed.Add(panel);

                // 从栈移除
                RemovePanelFromStack(panel);

                // 从缓存移除对应条目
                var keysToRemove = new List<string>();
                foreach (var kvp in cachedPanels)
                {
                    // 比较引用
                    if (kvp.Value == panel) keysToRemove.Add(kvp.Key);
                }
                foreach (var key in keysToRemove)
                {
                    cachedPanels.Remove(key);
                }

                // 关闭并销毁/禁用
                try
                {
                    panel.Close();

                    if (panel.gameObject != null)
                    {
                        if (destroyGameObjects)
                        {
                            if (panel.gameObject)
                                Destroy(panel.gameObject);
                        }
                        else
                        {
                            panel.gameObject.SetActive(false);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[UISystem] 清理层级面板异常: {panel?.GetType().Name} - {ex.Message}");
                }
            }

            // 清空该层列表
            list.Clear();
            Debug.Log($"[UISystem] 层级 {layer} 清理完成，共清理 {processed.Count} 个面板");
        }

        /// <summary>
        /// 获取指定层级的活跃面板数量
        /// </summary>
        public int GetActivePanelCountInLayer(UILayer layer)
        {
            if (!layerPanels.ContainsKey(layer)) return 0;

            int count = 0;
            foreach (var panel in layerPanels[layer])
            {
                if (panel != null && panel.gameObject.activeInHierarchy)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 检查指定层级是否有活跃面板
        /// </summary>
        public bool HasActivePanelsInLayer(UILayer layer)
        {
            return GetActivePanelCountInLayer(layer) > 0;
        }

        #endregion

        #region 调试和数据一致性

        /// <summary>
        /// 检查并修复数据一致性问题（调试用）
        /// </summary>
        [ContextMenu("检查数据一致性")]
        public void ValidateDataConsistency()
        {
            Debug.Log("[UISystem] 开始数据一致性检查...");

            var issues = new List<string>();

            // 检查缓存中的面板是否都在对应层级中
            foreach (var kvp in cachedPanels)
            {
                var panel = kvp.Value;
                if (panel == null)
                {
                    issues.Add($"缓存中存在空面板引用: {kvp.Key}");
                    continue;
                }

                if (!layerPanels.ContainsKey(panel.Layer) || !layerPanels[panel.Layer].Contains(panel))
                {
                    issues.Add($"面板 {kvp.Key} 不在其声明的层级 {panel.Layer} 中");
                }
            }

            // 检查层级中的面板是否都在缓存中（活跃面板）
            foreach (var kvp in layerPanels)
            {
                var layer = kvp.Key;
                var panels = kvp.Value;

                for (int i = panels.Count - 1; i >= 0; i--)
                {
                    var panel = panels[i];
                    if (panel == null)
                    {
                        panels.RemoveAt(i);
                        issues.Add($"层级 {layer} 中存在空面板引用，已清理");
                    }
                    else if (panel.gameObject.activeInHierarchy && !cachedPanels.ContainsValue(panel))
                    {
                        issues.Add($"活跃面板 {panel.GetType().Name} 不在缓存中");
                    }
                }
            }

            // 检查栈中的面板
            var stackPanels = activeStack.ToArray();
            for (int i = 0; i < stackPanels.Length; i++)
            {
                var panel = stackPanels[i];
                if (panel == null)
                {
                    issues.Add("活跃栈中存在空面板引用");
                }
                else if (!panel.gameObject.activeInHierarchy)
                {
                    issues.Add($"栈中面板 {panel.GetType().Name} 未激活");
                }
            }

            if (issues.Count == 0)
            {
                Debug.Log("<color=green>[UISystem] 数据一致性检查通过</color>");
            }
            else
            {
                Debug.LogWarning($"[UISystem] 发现 {issues.Count} 个数据一致性问题:");
                foreach (var issue in issues)
                {
                    Debug.LogWarning($"  - {issue}");
                }
            }

            Debug.Log($"[UISystem] 当前状态 - 缓存: {cachedPanels.Count}, 栈: {activeStack.Count}, 层级面板总数: {layerPanels.Values.Sum(list => list.Count)}");
        }

        /// <summary>
        /// 清理空引用和无效数据
        /// </summary>
        [ContextMenu("清理无效数据")]
        public void CleanupInvalidData()
        {
            Debug.Log("[UISystem] 开始清理无效数据...");

            int cleanedCount = 0;

            // 清理缓存中的空引用
            var cacheKeysToRemove = new List<string>();
            foreach (var kvp in cachedPanels)
            {
                if (kvp.Value == null)
                {
                    cacheKeysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in cacheKeysToRemove)
            {
                cachedPanels.Remove(key);
                cleanedCount++;
            }

            // 清理层级中的空引用
            foreach (var kvp in layerPanels)
            {
                var list = kvp.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i] == null)
                    {
                        list.RemoveAt(i);
                        cleanedCount++;
                    }
                }
            }

            // 清理栈中的空引用
            var validPanels = new Stack<UIPanel>();
            while (activeStack.Count > 0)
            {
                var panel = activeStack.Pop();
                if (panel != null)
                {
                    validPanels.Push(panel);
                }
                else
                {
                    cleanedCount++;
                }
            }

            // 重建栈
            while (validPanels.Count > 0)
            {
                activeStack.Push(validPanels.Pop());
            }

            Debug.Log($"[UISystem] 清理无效数据完成，共清理 {cleanedCount} 个无效引用");
        }

        #endregion

        #region 组件查找

        // 查找子物体（支持路径查找和递归查找）
        private GameObject FindChildGameObject(GameObject panel, string childPath, bool recursive = true)
        {
            if (string.IsNullOrEmpty(childPath) || panel == null)
            {
                Debug.LogError("[UISystem] 参数不能为空");
                return null;
            }

            if (childPath.Contains("/"))
            {
                // 路径查找
                Transform current = panel.transform;
                string[] pathSegments = childPath.Split('/');

                foreach (string segment in pathSegments)
                {
                    if (string.IsNullOrEmpty(segment)) continue;

                    Transform found = current.Find(segment);
                    if (found == null)
                    {
                        Debug.LogError($"[UISystem] 路径 {childPath} 中找不到 {segment}");
                        return null;
                    }
                    current = found;
                }
                return current.gameObject;
            }
            else
            {
                // 直接查找
                Transform found = panel.transform.Find(childPath);
                if (found != null) return found.gameObject;

                // 递归查找（如果启用）
                if (recursive)
                {
                    return FindChildRecursive(panel.transform, childPath);
                }

                Debug.LogError($"[UISystem] 在 {panel.name} 中找不到子物体 {childPath}");
                return null;
            }
        }

        /// <summary>
        /// 获取指定名称的子物体组件
        /// </summary>
        public T GetChildComponent<T>(GameObject panel, string objectName, bool recursive = true) where T : Component
        {
            if (panel == null || string.IsNullOrEmpty(objectName))
            {
                Debug.LogError("[UISystem] 参数不能为空");
                return null;
            }

            GameObject targetObj = FindChildGameObject(panel, objectName, recursive);
            if (targetObj == null) return null;

            return targetObj.GetComponent<T>();
        }

        #endregion

        #region 内部实现

        private T OpenPanelInternal<T>(string panelName, UILayer layer, bool useCache, GameObject prefab) where T : UIPanel
        {
            Debug.Log($"<color=green>[UISystem] 打开UI面板</color>: {panelName}");

            UIPanel panel = null;

            // 检查缓存
            if (useCache && cachedPanels.TryGetValue(panelName, out panel))
            {
                if (panel.gameObject.activeInHierarchy)
                {
                    Debug.LogWarning($"[UISystem] 面板 {panelName} 已经打开");
                    return panel as T;
                }

                // 修复：确保缓存面板的层级正确
                if (panel.Layer != layer)
                {
                    // 从原层级移除
                    if (layerPanels.ContainsKey(panel.Layer))
                    {
                        layerPanels[panel.Layer].Remove(panel);
                    }

                    // 更新层级并移动到新层级
                    panel.Layer = layer;
                    Transform targetLayer = GetUILayer(layer);
                    if (targetLayer != null)
                    {
                        panel.transform.SetParent(targetLayer, false);
                    }
                }
            }
            else
            {
                // 创建新面板
                panel = CreatePanel<T>(panelName, layer, prefab);
                if (panel == null) return null;

                if (useCache)
                {
                    cachedPanels[panelName] = panel;
                }
            }

            // 记录面板所属层级
            panel.Layer = layer;

            // 处理同层级内的锁定逻辑
            if (!layerPanels.ContainsKey(layer))
            {
                layerPanels[layer] = new List<UIPanel>();
            }

            // 仅获取当前申请层级的列表，如果该层级已经有面板，锁定该层级当前最顶层的面板
            if (layerPanels[layer].Count > 0 && ShouldLockPreviousPanel(layer))
            {
                // 仅锁定该层级原本的最顶层面板
                var previousPanelInLayer = layerPanels[layer][layerPanels[layer].Count - 1];
                previousPanelInLayer.OnLock();
            }

            if (!layerPanels[layer].Contains(panel))
            {
                layerPanels[layer].Add(panel);
            }

            // 显示面板
            panel.Show();
            activeStack.Push(panel);
            panel.transform.SetAsLastSibling();

            return panel as T;
        }

        private void ClosePanelInternal(UIPanel panel)
        {
            if (panel == null) return;

            Debug.Log($"<color=yellow>[UISystem] 关闭UI面板</color>: {panel.GetType().Name}");

            // 从其所属层级列表中移除，并处理同层级解锁
            if (layerPanels.ContainsKey(panel.Layer) && layerPanels[panel.Layer].Contains(panel))
            {
                var list = layerPanels[panel.Layer];
                list.Remove(panel);

                // 如果该层级还有其他面板，解锁该层级最新的面板，仅解锁同层级的下一个
                if (list.Count > 0)
                {
                    list[list.Count - 1].OnUnLock();
                }
            }
            else
            {
                // 兜底：如果Layer属性不准确，遍历所有层级移除
                foreach (var list in layerPanels.Values)
                {
                    if (list.Remove(panel))
                    {
                        // 同样处理解锁
                        if (list.Count > 0) list[list.Count - 1].OnUnLock();
                        break;
                    }
                }
            }

            // 必须添加：从全局栈中移除，保持数据一致性
            RemovePanelFromStack(panel);

            panel.Close();
        }

        #endregion
    }

    /// <summary>
    /// UI资源加载器接口
    /// </summary>
    public interface IUIResLoader
    {
        /// <summary>
        /// 加载面板预制体
        /// </summary>
        /// <param name="panelName">面板名称（通常是类名）</param>
        /// <returns>加载的GameObject预制体</returns>
        GameObject LoadPanelPrefab(string panelName);
    }

    /// <summary>
    /// 默认的Resources加载器实现
    /// </summary>
    public class ResourcesUILoader : IUIResLoader
    {
        public GameObject LoadPanelPrefab(string panelName)
        {
            // 保持原有的路径约定
            return Resources.Load<GameObject>($"UI/{panelName}");
        }
    }

    //TODO: 后续可以添加Addressables的实现类

    /// <summary>
    /// UI层级枚举
    /// </summary>
    public enum UILayer
    {
        /// <summary> 
        /// 背景层 - 静态背景
        /// </summary>
        BackgroundLayer,

        /// <summary> 
        /// 后期处理层 - UI后期处理效果
        /// </summary>
        PostProcessingLayer,

        /// <summary>
        /// 内容层 - 主要UI功能
        /// </summary>
        ContentLayer,

        /// <summary> 
        /// 弹窗层 - 消息弹窗
        /// </summary>
        PopupLayer,

        /// <summary>
        /// 引导层 - 引导玩家操作
        /// </summary>
        GuideLayer,

        /// <summary> 
        /// 调试层 - 创建和调试UI
        /// </summary>
        DebugLayer
    }
}
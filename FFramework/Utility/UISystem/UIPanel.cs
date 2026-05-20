// =============================================================
// 描述：UI面板基类
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：1.0.1
// 更新记录：继承BaseViewController，初始化方法名称更改为OnInitialize
// =============================================================
using System.Collections.Generic;
using FFramework.Core;
using UnityEngine;

namespace FFramework.Utility
{
    public abstract class UIPanel : ArchitectureViewController
    {
        #region 私有字段

        private CanvasGroup canvasGroup;
        private bool isInitialized = false;
        private bool isShowing = false;
        private bool isLocked = false;

        // 事件追踪列表，用于自动注销
        private List<System.Action> eventCleanupActions = new List<System.Action>();

        #endregion

        #region 属性

        /// <summary>面板是否已初始化</summary>
        public bool IsInitialized => isInitialized;

        /// <summary>面板是否正在显示</summary>
        public bool IsShowing => isShowing;

        /// <summary>面板是否被锁定</summary>
        public bool IsLocked => isLocked;

        /// <summary>面板层级</summary>
        public UILayer Layer { get; internal set; }

        /// <summary>事件数量</summary>
        public int EventCount => eventCleanupActions.Count;

        #endregion

        #region Unity生命周期

        protected override void Awake()
        {
            // 调用基类 ArchitectureViewController 的 Awake，触发自动注册 + 依赖注入
            base.Awake();
            OnAwake();
        }

        protected virtual void Start()
        {
            OnStart();
        }

        protected virtual void OnEnable()
        {
            if (!isInitialized)
            {
                // 调用基类的Initialize，实际会调用OnInitialize
                base.Initialize();
                isInitialized = true;
            }
            OnPanelEnable();
        }

        protected virtual void OnDisable()
        {
            OnPanelDisable();
        }

        protected override void OnDestroy()
        {
            OnPanelDestroy();
            CleanupAll();
        }

        #endregion

        #region 抽象和虚方法 - 子类重写

        /// <summary>面板初始化 - 只调用一次（子类必须实现）</summary>
        protected override abstract void OnInitialize();

        /// <summary>Awake时调用</summary>
        protected virtual void OnAwake() { }

        /// <summary>Start时调用</summary>
        protected virtual void OnStart() { }

        /// <summary>面板启用时调用</summary>
        protected virtual void OnPanelEnable() { }

        /// <summary>面板禁用时调用</summary>
        protected virtual void OnPanelDisable() { }

        /// <summary>面板销毁时调用</summary>
        protected virtual void OnPanelDestroy() { }

        /// <summary>面板显示时调用</summary>
        protected virtual void OnShow() { }

        /// <summary>面板隐藏时调用</summary>
        protected virtual void OnHide() { }

        /// <summary>面板锁定时调用</summary>
        protected virtual void OnLockPanel() { }

        /// <summary>面板解锁时调用</summary>
        protected virtual void OnUnlockPanel() { }

        #endregion

        #region 面板控制

        /// <summary>显示面板</summary>
        public void Show()
        {
            if (isShowing) return;

            EnsureCanvasGroup();
            gameObject.SetActive(true);
            canvasGroup.blocksRaycasts = !isLocked;
            isShowing = true;

            OnShow();
            Debug.Log($"<color=green>[UIPanel] 显示面板</color>: {GetType().Name}");
        }

        /// <summary>隐藏面板</summary>
        public void Hide()
        {
            if (!isShowing) return;

            EnsureCanvasGroup();
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
            isShowing = false;

            OnHide();
            Debug.Log($"<color=yellow>[UIPanel] 隐藏面板</color>: {GetType().Name}");
        }

        /// <summary>关闭面板（隐藏的别名）</summary>
        public void Close()
        {
            Hide();
        }

        /// <summary>锁定面板（禁用交互）</summary>
        public void OnLock()
        {
            if (isLocked) return;

            EnsureCanvasGroup();
            canvasGroup.blocksRaycasts = false;
            isLocked = true;

            OnLockPanel();
            Debug.Log($"<color=orange>[UIPanel] 锁定面板</color>: {GetType().Name}");
        }

        /// <summary>解锁面板（启用交互）</summary>
        public void OnUnLock()
        {
            if (!isLocked) return;

            EnsureCanvasGroup();
            if (isShowing)
            {
                canvasGroup.blocksRaycasts = true;
            }
            isLocked = false;

            OnUnlockPanel();
            Debug.Log($"<color=cyan>[UIPanel] 解锁面板</color>: {GetType().Name}");
        }

        #endregion

        #region 面板属性设置

        /// <summary>设置面板透明度</summary>
        public void SetAlpha(float alpha)
        {
            EnsureCanvasGroup();
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        /// <summary>设置面板可交互性</summary>
        public void SetInteractable(bool interactable)
        {
            EnsureCanvasGroup();
            canvasGroup.interactable = interactable;
        }

        /// <summary>设置面板是否阻挡射线</summary>
        public void SetBlocksRaycasts(bool blocksRaycasts)
        {
            EnsureCanvasGroup();
            canvasGroup.blocksRaycasts = blocksRaycasts;
        }

        #endregion

        #region 事件管理

        /// <summary>添加事件清理动作</summary>
        public void AddEventCleanup(System.Action cleanupAction, string componentName = "Unknown")
        {
            if (cleanupAction != null)
            {
                eventCleanupActions.Add(cleanupAction);
            }
        }

        /// <summary>移除指定的事件清理动作</summary>
        public void RemoveEventCleanup(System.Action cleanupAction)
        {
            eventCleanupActions.Remove(cleanupAction);
        }

        /// <summary>清理所有追踪的事件</summary>
        public void ClearTrackedEvents()
        {
            foreach (var cleanupAction in eventCleanupActions)
            {
                try
                {
                    cleanupAction?.Invoke();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[UIPanel] 清理事件时发生错误: {e.Message}");
                }
            }
            eventCleanupActions.Clear();
        }

        #endregion

        #region 内部方法

        /// <summary>确保CanvasGroup组件存在</summary>
        private void EnsureCanvasGroup()
        {
            if (canvasGroup == null && !TryGetComponent<CanvasGroup>(out canvasGroup))
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        /// <summary>清理所有资源</summary>
        private void CleanupAll()
        {
            // 清理追踪的事件
            ClearTrackedEvents();

            // 使用扩展方法清理所有UI事件（双重保险）
            try
            {
                if (this != null && gameObject != null)
                {
                    this.UnbindAllEvents();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[UIPanel] 清理UI事件扩展方法时出错: {e.Message}");
            }

            Debug.Log($"[UIPanel] 面板 {name} 已清理完成");
        }

        #endregion

        #region 调试工具

        /// <summary>打印面板状态信息</summary>
        [ContextMenu("打印面板状态")]
        public void PrintPanelStatus()
        {
            Debug.Log($"<color=cyan>[UIPanel] 面板状态</color> - {GetType().Name}:\n" +
                     $"  已初始化: {isInitialized}\n" +
                     $"  正在显示: {isShowing}\n" +
                     $"  已锁定: {isLocked}\n" +
                     $"  事件数量: {eventCleanupActions.Count}\n" +
                     $"  层级: {Layer}");
        }

        /// <summary>强制清理事件（调试用）</summary>
        [ContextMenu("强制清理事件")]
        public void ForceCleanupEvents()
        {
            ClearTrackedEvents();
            this.UnbindAllEvents();
            Debug.Log($"[UIPanel] 强制清理面板 {name} 的所有事件");
        }

        #endregion
    }
}
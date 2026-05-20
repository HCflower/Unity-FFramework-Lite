// =============================================================
// 描述：场景加载管理器
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：1.0.0
// =============================================================
using UnityEngine.SceneManagement;
using System.Collections;
using FFramework.Core;
using UnityEngine;
using System;

namespace FFramework.Utility
{
    public class SceneLoad : SingletonMono<SceneLoad>
    {
        /// <summary>
        /// 加载进度回调
        /// </summary>
        public Action<float> OnLoadProgress;

        protected override void InitializeSingleton() { }
        /// <summary>
        /// 同步加载场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="mode">加载模式（默认Single会自动卸载旧场景）</param>
        /// <param name="complete">完成回调</param>
        public void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single, Action complete = null)
        {
            try
            {
                SceneManager.LoadScene(sceneName, mode);
                complete?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"场景加载失败: {sceneName}, 错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步加载场景（不使用协程版本）
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="mode">加载模式（默认Single会自动卸载旧场景）</param>
        /// <param name="progress">进度回调</param>
        /// <param name="complete">完成回调</param>
        public void LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single,
            Action<float> progress = null, Action complete = null)
        {
            var asyncOperation = SceneManager.LoadSceneAsync(sceneName, mode);

            if (asyncOperation == null)
            {
                Debug.LogError($"无法加载场景: {sceneName}");
                return;
            }

            // 监听进度
            asyncOperation.completed += (op) => complete?.Invoke();

            // 如果需要进度回调，启动协程监听
            if (progress != null || OnLoadProgress != null)
            {
                CoroutineRunner.Instance.StartCoroutine(MonitorLoadProgress(asyncOperation, progress));
            }
        }

        /// <summary>
        /// 异步加载场景（协程版本）- 更好的控制
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="mode">加载模式</param>
        /// <param name="progress">进度回调</param>
        /// <param name="complete">完成回调</param>
        /// <param name="allowSceneActivation">是否允许场景激活（可用于预加载）</param>
        public void LoadSceneAsyncWithCoroutine(string sceneName, LoadSceneMode mode = LoadSceneMode.Single,
            Action<float> progress = null, Action complete = null, bool allowSceneActivation = true)
        {
            CoroutineRunner.Instance.StartCoroutine(LoadSceneCoroutine(sceneName, mode, progress, complete, allowSceneActivation));
        }

        /// <summary>
        /// 预加载场景（加载但不激活）
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="progress">进度回调</param>
        /// <param name="complete">完成回调</param>
        /// <returns>返回AsyncOperation用于后续激活</returns>
        public AsyncOperation PreloadScene(string sceneName, Action<float> progress = null, Action complete = null)
        {
            var asyncOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            if (asyncOperation != null)
            {
                // 阻止自动激活场景
                asyncOperation.allowSceneActivation = false;

                asyncOperation.completed += (op) => complete?.Invoke();

                if (progress != null || OnLoadProgress != null)
                {
                    CoroutineRunner.Instance.StartCoroutine(MonitorLoadProgress(asyncOperation, progress));
                }
            }

            return asyncOperation;
        }

        /// <summary>
        /// 激活预加载的场景
        /// </summary>
        /// <param name="asyncOperation">预加载操作</param>
        public void ActivatePreloadedScene(AsyncOperation asyncOperation)
        {
            if (asyncOperation != null)
            {
                asyncOperation.allowSceneActivation = true;
            }
        }

        /// <summary>
        /// 卸载场景
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <param name="complete">完成回调</param>
        public void UnloadScene(string sceneName, Action complete = null)
        {
            var asyncOperation = SceneManager.UnloadSceneAsync(sceneName);
            if (asyncOperation != null)
            {
                asyncOperation.completed += (op) => complete?.Invoke();
            }
        }

        /// <summary>
        /// 加载场景协程
        /// </summary>
        private IEnumerator LoadSceneCoroutine(string sceneName, LoadSceneMode mode,
            Action<float> progress, Action complete, bool allowSceneActivation)
        {
            var asyncOperation = SceneManager.LoadSceneAsync(sceneName, mode);

            if (asyncOperation == null)
            {
                Debug.LogError($"无法加载场景: {sceneName}");
                yield break;
            }

            asyncOperation.allowSceneActivation = allowSceneActivation;

            // 监听进度
            while (!asyncOperation.isDone)
            {
                float progressValue = Mathf.Clamp01(asyncOperation.progress / 0.9f);
                progress?.Invoke(progressValue);
                OnLoadProgress?.Invoke(progressValue);
                yield return null;
            }

            complete?.Invoke();
        }

        /// <summary>
        /// 监听加载进度协程
        /// </summary>
        private IEnumerator MonitorLoadProgress(AsyncOperation asyncOperation, Action<float> progress)
        {
            while (!asyncOperation.isDone)
            {
                float progressValue = Mathf.Clamp01(asyncOperation.progress / 0.9f);
                progress?.Invoke(progressValue);
                OnLoadProgress?.Invoke(progressValue);
                yield return null;
            }
        }

        /// <summary>
        /// 获取当前活动场景名称
        /// </summary>
        public string GetActiveSceneName()
        {
            return SceneManager.GetActiveScene().name;
        }

        /// <summary>
        /// 检查场景是否已加载
        /// </summary>
        /// <param name="sceneName">场景名称</param>
        /// <returns>是否已加载</returns>
        public bool IsSceneLoaded(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == sceneName)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 协程运行器（用于在非MonoBehaviour中运行协程）
    /// </summary>
    public class CoroutineRunner : SingletonMono<CoroutineRunner>
    {
        // 空的MonoBehaviour，仅用于运行协程
        protected override void InitializeSingleton()
        {

        }
    }
}
// =============================================================
// 描述：对象池静态拓展
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：1.0.2
// 修改记录：修复内存泄漏，优化协程管理，修正命名
// =============================================================
using System.Collections.Generic;
using UnityEngine;

namespace FFramework.Utility
{
    /// <summary>
    /// ObjectPool 链式调用扩展类
    /// </summary>
    public static class ObjectPoolExtensions
    {
        #region 非泛型扩展方法(GameObject为主)

        public static GameObject SetPosition(this GameObject obj, Vector3 position)
        {
            if (obj != null)
                obj.transform.position = position;
            return obj;
        }

        public static GameObject SetLocalPosition(this GameObject obj, Vector3 localPosition)
        {
            if (obj != null)
                obj.transform.localPosition = localPosition;
            return obj;
        }

        public static GameObject SetRotation(this GameObject obj, Quaternion rotation)
        {
            if (obj != null)
                obj.transform.rotation = rotation;
            return obj;
        }

        public static GameObject SetEulerAngles(this GameObject obj, Vector3 eulerAngles)
        {
            if (obj != null)
                obj.transform.eulerAngles = eulerAngles;
            return obj;
        }

        public static GameObject SetLocalRotation(this GameObject obj, Quaternion localRotation)
        {
            if (obj != null)
                obj.transform.localRotation = localRotation;
            return obj;
        }

        public static GameObject SetLocalEulerAngles(this GameObject obj, Vector3 localEulerAngles)
        {
            if (obj != null)
                obj.transform.localEulerAngles = localEulerAngles;
            return obj;
        }

        public static GameObject SetLocalScale(this GameObject obj, Vector3 scale)
        {
            if (obj != null)
                obj.transform.localScale = scale;
            return obj;
        }

        public static GameObject SetLocalScale(this GameObject obj, float scale)
        {
            if (obj != null)
                obj.transform.localScale = Vector3.one * scale;
            return obj;
        }

        public static GameObject SetParent(this GameObject obj, Transform parent, bool worldPositionStays = true)
        {
            if (obj != null)
                obj.transform.SetParent(parent, worldPositionStays);
            return obj;
        }

        public static GameObject SetName(this GameObject obj, string name)
        {
            if (obj != null)
                obj.name = name;
            return obj;
        }

        public static GameObject Do<T>(this GameObject obj, System.Action<T> action) where T : Component
        {
            if (obj != null && obj.TryGetComponent<T>(out var component))
            {
                action?.Invoke(component);
            }
            return obj;
        }

        public static GameObject TryDo<T>(this GameObject obj, System.Action<T> action) where T : Component
        {
            if (obj == null) return obj;

            if (obj.TryGetComponent<T>(out var component))
            {
                action?.Invoke(component);
            }
            else
            {
                Debug.LogWarning($"对象 {obj.name} 不包含组件 {typeof(T).Name}");
            }
            return obj;
        }

        public static GameObject SetActive(this GameObject obj, bool active)
        {
            if (obj != null)
                obj.SetActive(active);
            return obj;
        }

        [System.Obsolete("请使用 ReturnToPoolDelay 替代此方法")]
        public static GameObject ReturnToPoolDalay(this GameObject obj, float delay)
        {
            return obj.ReturnToPoolDelay(delay);
        }

        public static GameObject ReturnToPoolDelay(this GameObject obj, float delay)
        {
            if (obj != null)
            {
                var mono = obj.GetComponent<MonoBehaviour>();
                if (mono != null)
                {
                    // 先停止同一对象上已有的延迟返回协程
                    StopDelayReturnCoroutine(obj);
                    var coroutine = mono.StartCoroutine(ReturnToPoolCoroutine(obj, delay));
                    TrackDelayReturnCoroutine(obj, coroutine);
                }
                else
                {
                    Debug.LogWarning($"对象 {obj.name} 没有MonoBehaviour组件,无法启动协程延迟回收");
                }
            }
            return obj;
        }

        /// <summary>
        /// 精确停止指定对象上的延迟返回协程，不干扰其他业务协程
        /// </summary>
        public static void StopDelayReturnCoroutine(GameObject obj)
        {
            if (obj == null) return;

            if (delayReturnCoroutines.TryGetValue(obj, out CoroutineInfo info))
            {
                if (info.mono != null && info.coroutine != null)
                {
                    info.mono.StopCoroutine(info.coroutine);
                }
                delayReturnCoroutines.Remove(obj);
            }
        }

        #endregion

        #region 泛型扩展方法(Component为主)

        public static T SetPosition<T>(this T component, Vector3 position) where T : Component
        {
            if (component != null)
                component.transform.position = position;
            return component;
        }

        public static T SetLocalPosition<T>(this T component, Vector3 localPosition) where T : Component
        {
            if (component != null)
                component.transform.localPosition = localPosition;
            return component;
        }

        public static T SetRotation<T>(this T component, Quaternion rotation) where T : Component
        {
            if (component != null)
                component.transform.rotation = rotation;
            return component;
        }

        public static T SetLocalScale<T>(this T component, Vector3 scale) where T : Component
        {
            if (component != null)
                component.transform.localScale = scale;
            return component;
        }

        public static T SetParent<T>(this T component, Transform parent, bool worldPositionStays = true) where T : Component
        {
            if (component != null)
                component.transform.SetParent(parent, worldPositionStays);
            return component;
        }

        public static GameObject GetGameObject<T>(this T component) where T : Component
        {
            return component?.gameObject;
        }

        [System.Obsolete("请使用 ReturnToPoolDelay 替代此方法")]
        public static T ReturnToPoolDalay<T>(this T component, float delay) where T : Component
        {
            return component.ReturnToPoolDelay(delay);
        }

        public static T ReturnToPoolDelay<T>(this T component, float delay) where T : Component
        {
            if (component != null)
            {
                var mono = component as MonoBehaviour;
                if (mono != null)
                {
                    StopDelayReturnCoroutine(component.gameObject);
                    var coroutine = mono.StartCoroutine(ReturnToPoolCoroutine(component.gameObject, delay));
                    TrackDelayReturnCoroutine(component.gameObject, coroutine);
                }
                else
                {
                    // 尝试从同一GameObject获取MonoBehaviour组件
                    var monoOnObj = component.GetComponent<MonoBehaviour>();
                    if (monoOnObj != null)
                    {
                        StopDelayReturnCoroutine(component.gameObject);
                        var coroutine = monoOnObj.StartCoroutine(ReturnToPoolCoroutine(component.gameObject, delay));
                        TrackDelayReturnCoroutine(component.gameObject, coroutine);
                    }
                    else
                    {
                        Debug.LogWarning($"组件 {typeof(T).Name} 不是 MonoBehaviour，无法延迟回收");
                    }
                }
            }
            return component;
        }

        #endregion

        #region 对象池快速操作扩展

        /// <summary>
        /// 从对象池获取对象 - GameObject版本
        /// </summary>
        /// <param name="objectName">对象名称（Resources路径）</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        /// <returns>从对象池获取的GameObject</returns>
        public static GameObject GetPoolObject(this string objectName, bool resetTransform = true)
        {
            return ObjectPool.Instance.GetResourcesObjectFromPool(objectName, resetTransform);
        }

        /// <summary>
        /// 从对象池获取对象并返回指定组件 - string版本
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="objectName">对象名称（Resources路径）</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        /// <returns>从对象池获取的组件</returns>
        public static T GetPoolObject<T>(this string objectName, bool resetTransform = true) where T : Component
        {
            return ObjectPool.Instance.GetResourcesObjectFromPool<T>(objectName, resetTransform);
        }

        /// <summary>
        /// 从对象池获取对象 - 预制体版本
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        /// <returns>从对象池获取的GameObject</returns>
        public static GameObject GetPoolObject(this GameObject prefab, bool resetTransform = true)
        {
            return ObjectPool.Instance.GetAssetsObjectFromPool(prefab, resetTransform);
        }

        /// <summary>
        /// 从对象池获取对象并返回指定组件 - 预制体版本
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="prefab">预制体</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        /// <returns>从对象池获取的组件</returns>
        public static T GetPoolObject<T>(this GameObject prefab, bool resetTransform = true) where T : Component
        {
            return ObjectPool.Instance.GetAssetsObjectFromPool<T>(prefab, resetTransform);
        }

        /// <summary>
        /// 将GameObject返回到对象池
        /// </summary>
        /// <param name="obj">要返回的GameObject</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        /// <returns>返回自身以支持链式调用</returns>
        public static GameObject ReturnPool(this GameObject obj, bool resetTransform = true)
        {
            if (obj != null)
            {
                ObjectPool.Instance.ReturnObjectToPool(obj, resetTransform);
            }
            return obj;
        }

        /// <summary>
        /// 将Component对应的GameObject返回到对象池
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="component">要返回的组件</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        /// <returns>返回自身以支持链式调用</returns>
        public static T ReturnPool<T>(this T component, bool resetTransform = true) where T : Component
        {
            if (component != null)
            {
                ObjectPool.Instance.ReturnObjectToPool(component.gameObject, resetTransform);
            }
            return component;
        }

        /// <summary>
        /// 延迟返回GameObject到对象池
        /// </summary>
        /// <param name="obj">要返回的GameObject</param>
        /// <param name="delay">延迟时间（秒）</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        /// <returns>返回自身以支持链式调用</returns>
        public static GameObject ReturnPool(this GameObject obj, float delay, bool resetTransform = true)
        {
            if (obj != null)
            {
                StopDelayReturnCoroutine(obj);

                var mono = obj.GetComponent<MonoBehaviour>();
                if (mono != null)
                {
                    var coroutine = mono.StartCoroutine(ReturnToPoolCoroutine(obj, delay, resetTransform));
                    TrackDelayReturnCoroutine(obj, coroutine);
                }
                else
                {
                    Debug.LogWarning($"对象 {obj.name} 没有MonoBehaviour组件，无法延迟返回对象池");
                }
            }
            return obj;
        }

        /// <summary>
        /// 延迟返回Component对应的GameObject到对象池
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="component">要返回的组件</param>
        /// <param name="delay">延迟时间（秒）</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        /// <returns>返回自身以支持链式调用</returns>
        public static T ReturnPool<T>(this T component, float delay, bool resetTransform = true) where T : Component
        {
            if (component != null)
            {
                StopDelayReturnCoroutine(component.gameObject);

                var mono = component as MonoBehaviour;
                if (mono != null)
                {
                    var coroutine = mono.StartCoroutine(ReturnToPoolCoroutine(component.gameObject, delay, resetTransform));
                    TrackDelayReturnCoroutine(component.gameObject, coroutine);
                }
                else
                {
                    // 尝试从同一GameObject获取MonoBehaviour组件
                    var monoOnObj = component.GetComponent<MonoBehaviour>();
                    if (monoOnObj != null)
                    {
                        var coroutine = monoOnObj.StartCoroutine(ReturnToPoolCoroutine(component.gameObject, delay, resetTransform));
                        TrackDelayReturnCoroutine(component.gameObject, coroutine);
                    }
                    else
                    {
                        Debug.LogWarning($"对象 {component.gameObject.name} 没有MonoBehaviour组件,无法延迟返回对象池");
                    }
                }
            }
            return component;
        }

        #endregion

        #region 对象池预热扩展

        /// <summary>
        /// 预热对象池 - string版本（Resources加载）
        /// </summary>
        /// <param name="objectName">对象名称（Resources路径）</param>
        /// <param name="count">预热数量</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        /// <returns>返回对象名称以支持链式调用</returns>
        public static string PrewarmPool(this string objectName, int count, bool resetTransform = true)
        {
            if (!string.IsNullOrEmpty(objectName) && count > 0)
            {
                ObjectPool.Instance.PrewarmResourcesPool(objectName, count, resetTransform);
            }
            return objectName;
        }

        /// <summary>
        /// 预热对象池 - GameObject版本（预制体）
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <param name="count">预热数量</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        /// <returns>返回预制体以支持链式调用</returns>
        public static GameObject PrewarmPool(this GameObject prefab, int count, bool resetTransform = true)
        {
            if (prefab != null && count > 0)
            {
                ObjectPool.Instance.PrewarmAssetsPool(prefab, count, resetTransform);
            }
            return prefab;
        }

        /// <summary>
        /// 批量预热对象池 - string版本
        /// </summary>
        /// <param name="objectNames">对象名称数组</param>
        /// <param name="counts">对应的预热数量数组</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        public static void PrewarmPools(this string[] objectNames, int[] counts, bool resetTransform = true)
        {
            if (objectNames == null)
            {
                Debug.LogError("对象名称数组不能为null");
                return;
            }

            if (counts == null)
            {
                Debug.LogError("预热数量数组不能为null");
                return;
            }

            if (objectNames.Length != counts.Length)
            {
                Debug.LogError($"数组长度不匹配: objectNames({objectNames.Length}) vs counts({counts.Length})");
                return;
            }

            for (int i = 0; i < objectNames.Length; i++)
            {
                if (counts[i] < 0)
                {
                    Debug.LogWarning($"预热数量不能为负数: {objectNames[i]} = {counts[i]}");
                    continue;
                }
                objectNames[i].PrewarmPool(counts[i], resetTransform);
            }
        }

        /// <summary>
        /// 批量预热对象池 - GameObject版本
        /// </summary>
        /// <param name="prefabs">预制体数组</param>
        /// <param name="counts">对应的预热数量数组</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        public static void PrewarmPools(this GameObject[] prefabs, int[] counts, bool resetTransform = true)
        {
            if (prefabs == null || counts == null || prefabs.Length != counts.Length)
            {
                Debug.LogError("预制体数组和预热数量数组不匹配");
                return;
            }

            for (int i = 0; i < prefabs.Length; i++)
            {
                prefabs[i].PrewarmPool(counts[i], resetTransform);
            }
        }

        /// <summary>
        /// 批量预热对象池 - 统一数量版本
        /// </summary>
        /// <param name="objectNames">对象名称数组</param>
        /// <param name="count">统一的预热数量</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        public static void PrewarmPools(this string[] objectNames, int count, bool resetTransform = true)
        {
            if (objectNames == null) return;

            foreach (string objectName in objectNames)
            {
                objectName.PrewarmPool(count, resetTransform);
            }
        }

        /// <summary>
        /// 批量预热对象池 - 预制体统一数量版本
        /// </summary>
        /// <param name="prefabs">预制体数组</param>
        /// <param name="count">统一的预热数量</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        public static void PrewarmPools(this GameObject[] prefabs, int count, bool resetTransform = true)
        {
            if (prefabs == null) return;

            foreach (GameObject prefab in prefabs)
            {
                prefab.PrewarmPool(count, resetTransform);
            }
        }

        #endregion

        #region 对象池状态查询扩展

        /// <summary>
        /// 检查对象是否在对象池中
        /// </summary>
        /// <param name="obj">要检查的GameObject</param>
        /// <returns>如果对象在池中返回true，否则返回false</returns>
        public static bool IsInPool(this GameObject obj)
        {
            return ObjectPool.Instance.IsObjectInPool(obj);
        }

        /// <summary>
        /// 检查组件对应的GameObject是否在对象池中
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="component">要检查的组件</param>
        /// <returns>如果对象在池中返回true，否则返回false</returns>
        public static bool IsInPool<T>(this T component) where T : Component
        {
            return component != null && ObjectPool.Instance.IsObjectInPool(component.gameObject);
        }

        /// <summary>
        /// 获取对象池中的对象数量
        /// </summary>
        /// <param name="objectName">对象名称</param>
        /// <returns>池中对象数量</returns>
        public static int GetPoolCount(this string objectName)
        {
            return ObjectPool.Instance.GetPoolCount(objectName);
        }

        /// <summary>
        /// 获取对象池中的对象数量 - 预制体版本
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <returns>池中对象数量</returns>
        public static int GetPoolCount(this GameObject prefab)
        {
            return prefab != null ? ObjectPool.Instance.GetPoolCount(prefab.name) : 0;
        }

        /// <summary>
        /// 检查对象池是否存在
        /// </summary>
        /// <param name="objectName">对象名称</param>
        /// <returns>如果池存在返回true，否则返回false</returns>
        public static bool HasPool(this string objectName)
        {
            return ObjectPool.Instance.HasPool(objectName);
        }

        /// <summary>
        /// 检查对象池是否存在 - 预制体版本
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <returns>如果池存在返回true，否则返回false</returns>
        public static bool HasPool(this GameObject prefab)
        {
            return prefab != null && ObjectPool.Instance.HasPool(prefab.name);
        }

        #endregion

        // 协程跟踪信息结构
        private class CoroutineInfo
        {
            public MonoBehaviour mono;
            public Coroutine coroutine;

            public CoroutineInfo(MonoBehaviour mono, Coroutine coroutine)
            {
                this.mono = mono;
                this.coroutine = coroutine;
            }
        }

        // 协程跟踪字典 - 使用 CoroutineInfo 记录Mono引用，确保能精确停止
        private static Dictionary<GameObject, CoroutineInfo> delayReturnCoroutines = new Dictionary<GameObject, CoroutineInfo>();

        // 缓存的 WaitForSeconds 实例，减少GC分配
        private static Dictionary<float, WaitForSeconds> waitForSecondsCache = new Dictionary<float, WaitForSeconds>();

        private static void TrackDelayReturnCoroutine(GameObject obj, Coroutine coroutine)
        {
            if (obj == null) return;
            var mono = obj.GetComponent<MonoBehaviour>();
            if (mono != null)
            {
                delayReturnCoroutines[obj] = new CoroutineInfo(mono, coroutine);
            }
        }

        private static WaitForSeconds GetCachedWaitForSeconds(float delay)
        {
            if (!waitForSecondsCache.TryGetValue(delay, out WaitForSeconds wfs))
            {
                wfs = new WaitForSeconds(delay);
                waitForSecondsCache[delay] = wfs;
            }
            return wfs;
        }

        private static System.Collections.IEnumerator ReturnToPoolCoroutine(GameObject obj, float delay, bool resetTransform = true)
        {
            yield return GetCachedWaitForSeconds(delay);
            if (obj != null)
            {
                // 清理协程跟踪
                delayReturnCoroutines.Remove(obj);
                ObjectPool.Instance.ReturnObjectToPool(obj, resetTransform);
            }
        }
    }
}
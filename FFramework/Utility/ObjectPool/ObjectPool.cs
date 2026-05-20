// =============================================================
// 描述：对象池
// 作者：HCFlower
// 创建时间：2025-11-15 18:49:00
// 版本：1.0.2
// 修改记录：优化惰性清理，修复内存泄漏，增加容量限制，添加 OnDestroy 自动清理
// =============================================================
using System.Collections.Generic;
using FFramework.Core;
using UnityEngine;

namespace FFramework.Utility
{
    public class ObjectPool : SingletonMono<ObjectPool>
    {
        // 对象池字典
        private Dictionary<string, PoolData> poolDic = new Dictionary<string, PoolData>();
        // 根节点
        private GameObject poolRoot;
        // 缓存对象到池名的映射
        private Dictionary<GameObject, string> objectToPoolName = new Dictionary<GameObject, string>();

        protected override void InitializeSingleton() { }

        protected override void OnDestroy()
        {
            ClearPool();
            base.OnDestroy();
        }

        /// <summary>
        /// 对象池数据结构
        /// </summary>
        public class PoolData
        {
            // 池父节点
            public GameObject parent;
            // 对象列表
            public List<GameObject> objectList = new List<GameObject>();
            // 原始预制体引用（用于创建新对象）
            public GameObject prefab;
            // 跟踪在池中的对象
            private HashSet<GameObject> objectsInPool = new HashSet<GameObject>();
            // 最大容量，-1表示无限制
            public int maxCapacity = -1;

            // 构造函数（通过预制体）
            public PoolData(GameObject poolRoot, GameObject prefab)
            {
                this.prefab = prefab;
                this.parent = new GameObject(prefab.name + "-Pool");
                this.parent.transform.SetParent(poolRoot.transform);
            }

            // 构造函数（通过Resources路径）
            public PoolData(GameObject poolRoot, string objectName)
            {
                this.prefab = Resources.Load<GameObject>(objectName);
                if (this.prefab == null)
                {
                    Debug.LogError($"无法从Resources路径加载对象: {objectName}");
                }
                this.parent = new GameObject(objectName + "-Pool");
                this.parent.transform.SetParent(poolRoot.transform);
            }

            // 清理无效对象（惰性清理，只在必要时执行）
            private void RemoveNulls()
            {
                for (int i = objectList.Count - 1; i >= 0; i--)
                {
                    if (objectList[i] == null)
                    {
                        objectList.RemoveAt(i);
                    }
                }
            }

            // 获取对象
            public GameObject GetObjectFromPool()
            {
                // 惰性清理：只在列表非空时检查最后一个元素
                while (objectList.Count > 0)
                {
                    int lastIndex = objectList.Count - 1;
                    GameObject obj = objectList[lastIndex];
                    if (obj == null)
                    {
                        objectList.RemoveAt(lastIndex);
                        continue;
                    }

                    objectList.RemoveAt(lastIndex);
                    objectsInPool.Remove(obj);
                    return obj;
                }
                return null;
            }

            // 将对象返回到池中
            public bool ReturnObjectToPool(GameObject obj, bool resetTransform = true)
            {
                // 避免重复添加
                if (obj == null || objectsInPool.Contains(obj)) return false;

                // 检查容量限制
                if (maxCapacity > 0 && objectList.Count >= maxCapacity)
                {
                    GameObject.Destroy(obj);
                    return false;
                }

                obj.SetActive(false);
                obj.transform.SetParent(parent.transform);

                // 根据参数决定是否重置Transform
                if (resetTransform)
                {
                    obj.transform.localPosition = Vector3.zero;
                    obj.transform.localRotation = Quaternion.identity;
                }

                objectList.Add(obj);
                objectsInPool.Add(obj);
                return true;
            }

            // 预热对象
            public void PrewarmObjects(int count, bool resetTransform = true)
            {
                if (prefab == null) return;

                // 受容量限制
                int actualCount = maxCapacity > 0
                    ? Mathf.Min(count, Mathf.Max(0, maxCapacity - objectList.Count))
                    : count;

                for (int i = 0; i < actualCount; i++)
                {
                    GameObject obj = GameObject.Instantiate(prefab);
                    obj.name = prefab.name;
                    obj.SetActive(false);
                    obj.transform.SetParent(parent.transform);

                    if (resetTransform)
                    {
                        obj.transform.localPosition = Vector3.zero;
                        obj.transform.localRotation = Quaternion.identity;
                    }

                    objectList.Add(obj);
                    objectsInPool.Add(obj);
                }
            }

            // 检查对象是否在池中
            public bool IsObjectInPool(GameObject obj)
            {
                return obj != null && objectsInPool.Contains(obj);
            }

            // 获取池中对象数量
            public int Count => objectList.Count;

            // 设置最大容量
            public void SetMaxCapacity(int capacity)
            {
                maxCapacity = capacity;
            }

            // 清理过期缓存引用
            public void CleanupInvalidRefs()
            {
                RemoveNulls();
            }

            // 清理池
            public void Clear()
            {
                for (int i = objectList.Count - 1; i >= 0; i--)
                {
                    if (objectList[i] != null)
                    {
                        GameObject.Destroy(objectList[i]);
                    }
                }
                objectList.Clear();
                objectsInPool.Clear();

                if (parent != null)
                {
                    GameObject.Destroy(parent);
                    parent = null;
                }
            }
        }

        #region 对象获取方法

        /// <summary>
        /// 从对象池中获取对象（通过Resources加载）
        /// </summary>
        /// <param name="objectName">对象名称</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        /// <returns></returns>
        public GameObject GetResourcesObjectFromPool(string objectName, bool resetTransform = true)
        {
            if (string.IsNullOrEmpty(objectName)) return null;

            EnsurePoolRoot();

            // 确保池存在
            if (!poolDic.ContainsKey(objectName))
            {
                poolDic[objectName] = new PoolData(poolRoot, objectName);
            }

            // 从池中获取对象
            GameObject obj = poolDic[objectName].GetObjectFromPool();
            if (obj == null)
            {
                if (poolDic[objectName].prefab == null)
                {
                    Debug.LogError($"无法从Resources加载对象: {objectName}");
                    return null;
                }
                obj = GameObject.Instantiate(poolDic[objectName].prefab);
                obj.name = objectName;
            }

            SetupObjectFromPool(obj, resetTransform);
            return obj;
        }

        /// <summary>
        /// 从对象池中获取对象并返回指定组件（通过Resources加载）
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="objectName">对象名称</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        /// <returns>指定类型的组件，如果不存在则返回null</returns>
        public T GetResourcesObjectFromPool<T>(string objectName, bool resetTransform = true) where T : Component
        {
            GameObject obj = GetResourcesObjectFromPool(objectName, resetTransform);
            if (obj == null) return null;

            T component = obj.GetComponent<T>();
            if (component == null)
            {
                Debug.LogWarning($"对象 {objectName} 不包含组件 {typeof(T).Name}");
            }
            return component;
        }

        /// <summary>
        /// 从对象池中获取对象（通过传入预制体）
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        /// <returns></returns>
        public GameObject GetAssetsObjectFromPool(GameObject prefab, bool resetTransform = true)
        {
            if (prefab == null) return null;

            EnsurePoolRoot();

            string poolName = prefab.name;

            // 确保池存在
            if (!poolDic.ContainsKey(poolName))
            {
                poolDic[poolName] = new PoolData(poolRoot, prefab);
            }

            // 从池中获取对象
            GameObject obj = poolDic[poolName].GetObjectFromPool();
            if (obj == null)
            {
                obj = GameObject.Instantiate(prefab);
                obj.name = prefab.name;
            }

            SetupObjectFromPool(obj, resetTransform);
            return obj;
        }

        /// <summary>
        /// 从对象池中获取对象并返回指定组件（通过传入预制体）
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="prefab">预制体</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        /// <returns>指定类型的组件，如果不存在则返回null</returns>
        public T GetAssetsObjectFromPool<T>(GameObject prefab, bool resetTransform = true) where T : Component
        {
            GameObject obj = GetAssetsObjectFromPool(prefab, resetTransform);
            if (obj == null) return null;

            T component = obj.GetComponent<T>();
            if (component == null)
            {
                Debug.LogWarning($"对象 {prefab.name} 不包含组件 {typeof(T).Name}");
            }
            return component;
        }

        #endregion

        #region 对象归还和清理

        /// <summary>
        /// 返回对象到对象池
        /// </summary>
        /// <param name="obj">对象</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        public void ReturnObjectToPool(GameObject obj, bool resetTransform = true)
        {
            if (obj == null) return;

            EnsurePoolRoot();

            // 清理对象到池名的缓存映射
            objectToPoolName.Remove(obj);

            // 只停止该对象上的延迟返回协程，不干扰其他业务协程
            ObjectPoolExtensions.StopDelayReturnCoroutine(obj);

            // 获取或创建池
            string poolName = obj.name;
            if (!poolDic.TryGetValue(poolName, out PoolData poolData))
            {
                Debug.LogWarning($"对象 {poolName} 没有对应的对象池，创建新池");
                GameObject prefab = Resources.Load<GameObject>(poolName);
                if (prefab != null)
                {
                    poolData = new PoolData(poolRoot, poolName);
                }
                else
                {
                    poolData = new PoolData(poolRoot, obj);
                }
                poolDic[poolName] = poolData;
            }

            // 先返回到池中（会隐藏对象并重置Transform）
            bool addedToPool = poolData.ReturnObjectToPool(obj, resetTransform);

            if (addedToPool)
            {
                // 再调用接口回调（此时对象已隐藏，不会影响场景表现）
                IPoolObject poolObj = obj.GetComponent<IPoolObject>();
                poolObj?.OnAfterReturnToPool();
            }
        }

        /// <summary>
        /// 清理对象池 - 场景切换时调用
        /// </summary>
        public void ClearPool()
        {
            foreach (var pool in poolDic.Values)
            {
                pool.Clear();
            }
            poolDic.Clear();
            objectToPoolName.Clear();

            if (poolRoot != null)
            {
                GameObject.Destroy(poolRoot);
                poolRoot = null;
            }
        }

        /// <summary>
        /// 清理指定对象池
        /// </summary>
        /// <param name="poolName">池名称</param>
        public void ClearPool(string poolName)
        {
            if (poolDic.ContainsKey(poolName))
            {
                poolDic[poolName].Clear();
                poolDic.Remove(poolName);
            }
            // 清理对象映射缓存中属于该池的条目
            List<GameObject> keysToRemove = null;
            foreach (var kvp in objectToPoolName)
            {
                if (kvp.Value == poolName)
                {
                    if (keysToRemove == null)
                        keysToRemove = new List<GameObject>();
                    keysToRemove.Add(kvp.Key);
                }
            }
            if (keysToRemove != null)
            {
                foreach (var key in keysToRemove)
                {
                    objectToPoolName.Remove(key);
                }
            }
        }

        #endregion

        #region 预热功能

        /// <summary>
        /// 预热对象池（通过Resources加载）
        /// </summary>
        /// <param name="objectName">对象名称</param>
        /// <param name="count">预热数量</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        public void PrewarmResourcesPool(string objectName, int count, bool resetTransform = true)
        {
            if (string.IsNullOrEmpty(objectName) || count <= 0) return;

            EnsurePoolRoot();

            // 确保池存在
            if (!poolDic.ContainsKey(objectName))
            {
                poolDic[objectName] = new PoolData(poolRoot, objectName);
                // 检查预制体是否成功加载
                if (poolDic[objectName].prefab == null)
                {
                    Debug.LogError($"预热失败:无法加载Resources对象 {objectName}");
                    poolDic.Remove(objectName);
                    return;
                }
            }

            // 预热对象
            poolDic[objectName].PrewarmObjects(count, resetTransform);
        }

        /// <summary>
        /// 预热对象池（通过预制体）
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <param name="count">预热数量</param>
        /// <param name="resetTransform">是否重置Transform状态</param>
        public void PrewarmAssetsPool(GameObject prefab, int count, bool resetTransform = true)
        {
            if (prefab == null || count <= 0) return;

            EnsurePoolRoot();

            string poolName = prefab.name;
            // 确保池存在
            if (!poolDic.ContainsKey(poolName))
            {
                poolDic[poolName] = new PoolData(poolRoot, prefab);
            }

            // 预热对象
            poolDic[poolName].PrewarmObjects(count, resetTransform);
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 设置从池中获取的对象状态
        /// </summary>
        private void SetupObjectFromPool(GameObject obj, bool resetTransform = true)
        {
            // 设置父级为null和激活状态
            obj.transform.SetParent(null);
            obj.SetActive(true);

            // 根据参数决定是否重置Transform
            if (resetTransform)
            {
                obj.transform.position = Vector3.zero;
                obj.transform.rotation = Quaternion.identity;
                obj.transform.localScale = Vector3.one;
            }

            // 调用接口回调
            IPoolObject poolObj = obj.GetComponent<IPoolObject>();
            poolObj?.OnBeforeGetFromPool();

            // 缓存对象到池名的映射
            objectToPoolName[obj] = obj.name;
        }

        /// <summary>
        /// 确保池根节点存在
        /// </summary>
        private void EnsurePoolRoot()
        {
            if (poolRoot == null)
            {
                poolRoot = new GameObject("ObjectPoolRoot");
                // DontDestroyOnLoad(poolRoot);
            }
        }

        /// <summary>
        /// 清理对象池中的无效引用（惰性清理的补充）
        /// </summary>
        public void CleanupInvalidRefs()
        {
            List<string> emptyPools = null;
            foreach (var kvp in poolDic)
            {
                kvp.Value.CleanupInvalidRefs();
                if (kvp.Value.Count == 0)
                {
                    if (emptyPools == null)
                        emptyPools = new List<string>();
                    emptyPools.Add(kvp.Key);
                }
            }
            // 清理空池
            if (emptyPools != null)
            {
                foreach (var poolName in emptyPools)
                {
                    poolDic[poolName].Clear();
                    poolDic.Remove(poolName);
                }
            }
        }

        #endregion

        #region 池状态查询

        /// <summary>
        /// 获取指定池的对象数量
        /// </summary>
        public int GetPoolCount(string poolName)
        {
            return poolDic.ContainsKey(poolName) ? poolDic[poolName].Count : 0;
        }

        /// <summary>
        /// 尝试获取池数据（新增方法） - 允许外部查询池的父节点和对象数量，增强调试和监控能力
        /// </summary>
        /// <param name="poolName">池的名称</param>
        /// <param name="parent">池的父节点</param>
        /// <param name="count">池中的对象数量</param>
        /// <returns>如果成功获取池数据返回true，否则返回false</returns>
        public bool TryGetPoolData(string poolName, out GameObject parent, out int count)
        {
            parent = null;
            count = 0;

            if (string.IsNullOrEmpty(poolName))
                return false;

            if (!poolDic.TryGetValue(poolName, out var poolData) || poolData == null)
                return false;

            parent = poolData.parent;
            count = poolData.Count;
            return true;
        }

        /// <summary>
        /// 检查池是否存在
        /// </summary>
        public bool HasPool(string poolName)
        {
            return poolDic.ContainsKey(poolName);
        }

        /// <summary>
        /// 获取所有池的名称
        /// </summary>
        public string[] GetAllPoolNames()
        {
            string[] names = new string[poolDic.Count];
            int index = 0;
            foreach (string key in poolDic.Keys)
            {
                names[index++] = key;
            }
            return names;
        }

        /// <summary>
        /// 检查对象是否在对象池中
        /// </summary>
        /// <param name="obj">要检查的对象</param>
        /// <returns>如果对象在池中返回true，否则返回false</returns>
        public bool IsObjectInPool(GameObject obj)
        {
            if (obj == null) return false;

            if (objectToPoolName.TryGetValue(obj, out string poolName))
            {
                if (poolDic.ContainsKey(poolName))
                {
                    return poolDic[poolName].IsObjectInPool(obj);
                }
            }
            return false;
        }

        /// <summary>
        /// 检查指定池名的对象是否在对象池中
        /// </summary>
        /// <param name="obj">要检查的对象</param>
        /// <param name="poolName">指定的池名</param>
        /// <returns>如果对象在指定池中返回true，否则返回false</returns>
        public bool IsObjectInPool(GameObject obj, string poolName)
        {
            if (obj == null || string.IsNullOrEmpty(poolName)) return false;

            if (poolDic.ContainsKey(poolName))
            {
                return poolDic[poolName].IsObjectInPool(obj);
            }
            return false;
        }

        #endregion
    }

    /// <summary>
    /// 对象池对象接口 - 实现该接口的对象可以在获取和归还时执行特定逻辑
    /// </summary>
    public interface IPoolObject
    {
        /// <summary>
        /// 当从对象池中获取出前时调用
        /// </summary>
        void OnBeforeGetFromPool();

        /// <summary>
        /// 当归还对象到对象池时调用
        /// </summary>
        void OnAfterReturnToPool();
    }
}
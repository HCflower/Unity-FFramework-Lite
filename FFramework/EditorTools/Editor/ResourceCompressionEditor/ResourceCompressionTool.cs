using UnityEngine;

namespace FFramework.Editor
{
    internal enum CompressionPage
    {
        Texture = 0,
        Audio = 1,
        Model = 2,
    }

    // 资源缓存数据类
    [System.Serializable]
    public class TextureCacheData
    {
        public Texture2D texture;
        public string path;
        public long memoryUsage;
        public bool foldout;
        public int width;
        public int height;
        public TextureFormat format;
        public int mipmapCount;
    }

    [System.Serializable]
    public class AudioCacheData
    {
        public AudioClip audio;
        public string path;
        public long memoryUsage;
        public bool foldout;
        public float length;
        public int frequency;
        public int channels;
    }

    [System.Serializable]
    public class ModelCacheData
    {
        public GameObject model;
        public string path;
        public long memoryUsage;
        public bool foldout;
        public int vertexCount;
        public int triangleCount;
        public int meshCount;
        public bool hasAnimation;
        public bool hasAvatar;
    }
}

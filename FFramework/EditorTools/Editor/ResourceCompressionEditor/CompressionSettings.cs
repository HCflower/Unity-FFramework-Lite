using UnityEngine;
using UnityEditor;

namespace FFramework.Editor
{
    [System.Serializable]
    public class TextureCompressionSettings
    {
        public int maxTextureSize = 1024;
        public float compressionQuality = 50f;
        public bool generateMipMaps = false;
        public bool forceCompression = true;
        public TextureImporterCompression compressionMode = TextureImporterCompression.Compressed;
        public bool overridePlatformSettings = true;
    }

    [System.Serializable]
    public class TextureSettings
    {
        public TextureImporterType textureType = TextureImporterType.Default;
        public TextureImporterShape textureShape = TextureImporterShape.Texture2D;
        public bool sRGBTexture = true;
        public TextureImporterAlphaSource alphaSource = TextureImporterAlphaSource.FromInput;
        public bool alphaIsTransparency;
#if UNITY_2020_1_OR_NEWER
        public bool alphaPremultiply;
#endif
        public TextureImporterNPOTScale nonPowerOf2 = TextureImporterNPOTScale.ToNearest;
        public bool readable = false;
        public bool streamingMipMaps = false;
        public FilterMode filterMode = FilterMode.Bilinear;
        public int anisoLevel = 1;
        public TextureWrapMode wrapMode = TextureWrapMode.Repeat;
    }

    [System.Serializable]
    public class AudioCompressionSettings
    {
        public AudioClipLoadType loadType = AudioClipLoadType.DecompressOnLoad;
        public bool preloadAudioData = true;
        public AudioCompressionFormat compressionFormat = AudioCompressionFormat.Vorbis;
        public float quality = 0.5f;
        public AudioSampleRateSetting sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        public int sampleRateOverride = 44100;
        public bool overridePlatformSettings = false;
        public AudioPlatform targetPlatform = AudioPlatform.Standalone;
        public bool forceToMono = false;
        public bool ambisonic = false;
    }

    public enum AudioPlatform
    {
        Standalone,
        Android,
        iPhone,
        WebGL
    }

    [System.Serializable]
    public class ModelCompressionSettings
    {
        // Mesh 压缩
        public ModelImporterMeshCompression meshCompression = ModelImporterMeshCompression.Off;
        public bool isReadable = false;
        public bool optimizeMeshPolygons = true;
        public bool optimizeMeshVertices = true;
        public bool importBlendShapes = true;
        public bool addCollider = false;

        // 动画压缩
        public bool importAnimation = true;
        public ModelImporterAnimationCompression animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
        public float animationRotationError = 0.5f;
        public float animationPositionError = 0.5f;
        public float animationScaleError = 0.5f;

        // 骨骼
        public ModelImporterAvatarSetup avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

        // 材质
        public ModelImporterMaterialImportMode materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

        // 平台
        public bool overridePlatformSettings = false;
    }

    [System.Serializable]
    public class CompressionResult
    {
        public float originalSize;
        public float compressedSize;
        public int processedCount;
    }
}

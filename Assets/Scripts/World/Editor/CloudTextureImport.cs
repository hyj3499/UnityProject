using UnityEditor;

namespace FarmMVP
{
    /// <summary>Sprites/Cloud 의 구름 그림을 픽셀 스프라이트로 임포트한다 (그림자로 쓰므로 압축·뭉갬 없이).</summary>
    public class CloudTextureImport : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/Sprites/Cloud/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = UnityEngine.FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}

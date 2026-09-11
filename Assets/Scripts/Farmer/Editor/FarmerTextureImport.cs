using UnityEditor;
using UnityEngine;

namespace FarmMVP
{
    /// <summary>
    /// Farmer 그림을 넣을 때마다 임포트 설정을 자동으로 맞춰 준다.
    ///
    /// 이 그림들은 Unity가 만들어 주는 스프라이트를 그대로 쓰지 않는다. 실행 중에 픽셀을 직접
    /// 읽어(FarmerArt) 색을 갈아 칠하고 칸별로 잘라 쓰기 때문에, 설정이 기본값이면 그림마다
    /// 다른 방식으로 어긋난다:
    ///   · 읽기가 막혀 있으면 → 픽셀을 읽는 순간 예외가 난다
    ///   · 뭉개는 확대를 쓰면 → 픽셀 그림이 흐리게 번진다
    ///   · 압축이 켜져 있으면 → 색이 미세하게 달라져 소매 색 갈아 끼우기가 어긋난다
    ///
    /// 그림을 새로 넣을 때 손으로 맞출 것이 없도록 여기서 한 번에 정한다.
    /// </summary>
    public class FarmerTextureImport : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/Sprites/Farmer/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = FarmerSlots.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FarmMVP
{
    public static class MPLiquidBarTools
    {
        [MenuItem("Farm MVP/UI/MP 바 적용 및 검증")]
        public static void ApplyAndCheck()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before applying the MP bar.");
            Validate();
            int count = 0;
            foreach (var hud in UnityEngine.Object.FindObjectsByType<HPMPBarsHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!hud.gameObject.scene.IsValid() || !hud.gameObject.scene.isLoaded) continue;
                var bar = hud.transform.Find("MPBar");
                if (bar == null) continue;
                Undo.RegisterFullObjectHierarchyUndo(bar.gameObject, "Apply liquid MP bar");
                var liquid = bar.GetComponent<MPLiquidBar>();
                if (liquid == null) liquid = Undo.AddComponent<MPLiquidBar>(bar.gameObject);
                liquid.Configure();
                liquid.SetMp(100, 100, true);
                EditorUtility.SetDirty(liquid);
                EditorSceneManager.MarkSceneDirty(bar.gameObject.scene);
                EditorSceneManager.SaveScene(bar.gameObject.scene);
                Selection.activeGameObject = bar.gameObject;
                count++;
            }
            if (count == 0) throw new InvalidOperationException("No loaded HPMPBarsHUD/MPBar found.");
            File.AppendAllText("Temp/MPBarChecks/result.txt", $"\nApplied and saved {count} MP bar(s) in the loaded scene.\n");
            Debug.Log("MP liquid bar: validation passed; applied and saved the loaded scene.");
        }

        [MenuItem("Farm MVP/UI/MP 바 검증만 실행")]
        public static void Validate()
        {
            Directory.CreateDirectory("Temp/MPBarChecks");
            File.WriteAllText("Temp/MPBarChecks/result.txt", "Running MP bar checks...\n");
            var root = new GameObject("MP bar validation", typeof(RectTransform));
            root.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var liquid = root.AddComponent<MPLiquidBar>();
                liquid.Configure();
                var fill = root.transform.Find("Fill").GetComponent<Image>();
                var wave = root.transform.Find("LiquidAnimation").GetComponent<Image>();
                var frame = root.transform.Find("Frame").GetComponent<Image>();
                var bg = root.transform.Find("Background").GetComponent<Image>();
                Assert(fill != null && frame.sprite != null && bg.sprite != null, "Required layers and supplied sprites resolve");
                Assert(frame.sprite.name == "MPFrame" && bg.sprite.name == "MPBackground", "Frame and background use supplied sheets");
                Assert(frame.sprite.rect.size == new Vector2(26, 9), "Frame slice has exact native bounds");
                foreach (var path in new[] { "MP bar", "MP bar frame", "MP Bar_animation" })
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/Sprites/UI/Bars/" + path + ".png");
                    Assert(importer != null && importer.filterMode == FilterMode.Point && !importer.mipmapEnabled
                        && importer.textureCompression == TextureImporterCompression.Uncompressed, "Pixel import: " + path);
                }
                var frames = Resources.LoadAll<Sprite>("Sprites/UI/Bars/MP Bar_animation");
                Assert(frames.Length == 8 && frames.All(s => s.rect.size == new Vector2(22, 4)), "Eight separate 22x4 animation frames");
                liquid.SetMp(100, 100, true);
                Assert(fill.fillAmount == 1f && !wave.enabled && !liquid.IsDraining, "Initial load snaps to full without animation");
                liquid.SetMp(63, 100);
                Assert(liquid.IsDraining && fill.fillAmount == 1f && wave.enabled, "Consumption begins a liquid drain");
                Assert(fill.sprite.name == "MPFill_1", "Color reflects actual MP during drain");
                Advance(liquid, 0.225f);
                Assert(fill.fillAmount > 0.63f && fill.fillAmount < 1f, "Drain interpolates visible amount");
                float visible = fill.fillAmount;
                liquid.SetMp(63, 100);
                Assert(fill.fillAmount == visible, "Unrelated stat refresh does not reset drain");
                liquid.SetMp(40, 100);
                Assert(fill.fillAmount == visible, "Repeated spend continues from the visible amount");
                Advance(liquid, 1f);
                Assert(Mathf.Approximately(fill.fillAmount, 0.4f) && !wave.enabled && !liquid.IsDraining, "Drain settles at exact percentage");
                liquid.SetMp(10, 100);
                Advance(liquid, 0.1f);
                liquid.SetMp(80, 100);
                Assert(fill.fillAmount == 0.8f && !liquid.IsDraining && !wave.enabled, "Recovery cancels an active drain");
                liquid.SetMp(0, 100);
                Advance(liquid, 1f);
                Assert(fill.fillAmount == 0f && !wave.enabled, "Empty MP hides liquid and animation");
                liquid.SetMp(100, 0, true);
                Assert(fill.fillAmount == 0f, "Zero maximum avoids division by zero");
                liquid.SetMp(-10, 100, true);
                Assert(fill.fillAmount == 0f, "Negative MP clamps to empty");
                liquid.SetMp(150, 100, true);
                Assert(fill.fillAmount == 1f, "Overfull MP clamps to full");
                foreach (var pair in new[] { (1f, 0), (0.71f, 0), (0.7f, 1), (0.5f, 2), (0.3f, 3), (0.2f, 4), (0f, 4) })
                    Assert(liquid.ColorIndex(pair.Item1) == pair.Item2, "Color boundary " + pair.Item1);
                Assert(frame.type == Image.Type.Simple && frame.color == Color.white, "Frame does not drain or change color");
                Assert(root.GetComponentsInChildren<Image>().All(i => !i.raycastTarget), "HUD layers do not intercept clicks");
                CheckWobble(fill);
                File.AppendAllText("Temp/MPBarChecks/result.txt", "PASS: all MP liquid bar checks completed.\n");
            }
            catch (Exception e)
            {
                File.AppendAllText("Temp/MPBarChecks/result.txt", "FAIL: " + e + "\n");
                throw;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CheckWobble(Image fill)
        {
            fill.rectTransform.sizeDelta = new Vector2(88, 16);
            var bounds = fill.rectTransform.rect;
            using (var vh = new VertexHelper())
            {
                var quad = new UIVertex[4];
                for (int i = 0; i < 4; i++) quad[i] = UIVertex.simpleVert;
                quad[0].position = new Vector2(bounds.xMin, bounds.yMin);
                quad[1].position = new Vector2(bounds.xMin, bounds.yMax);
                quad[2].position = new Vector2(0, bounds.yMax);
                quad[3].position = new Vector2(0, bounds.yMin);
                vh.AddUIVertexQuad(quad);
                fill.GetComponent<MPLiquidWobble>().SetWave(1, 1);
                fill.GetComponent<MPLiquidWobble>().ModifyMesh(vh);
                Assert(vh.currentVertCount == 16, "Liquid edge splits into four pixel rows");
                UIVertex vertex = default;
                for (int i = 0; i < vh.currentVertCount; i++)
                {
                    vh.PopulateUIVertex(ref vertex, i);
                    Assert(vertex.position.x >= bounds.xMin && vertex.position.x <= bounds.xMax, "Wobble stays inside frame");
                }
            }
        }

        private static void Advance(MPLiquidBar liquid, float delta)
            => typeof(MPLiquidBar).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(liquid, new object[] { delta });

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            File.AppendAllText("Temp/MPBarChecks/result.txt", "PASS: " + message + "\n");
        }
    }
}

// Copied to Temp/FarmerEyesChecks/Assets/Editor for isolated Unity validation.
using System;
using System.IO;
using System.Reflection;
using FarmMVP;
using UnityEditor;
using UnityEngine;

public static class FarmerEyesChecks
{
    private static int checks;
    private static void Check(bool ok, string message)
    {
        checks++;
        if (!ok) throw new Exception(message);
    }

    public static void Run()
    {
        try
        {
            var original = Resources.Load<Texture2D>(FarmerPoses.SheetPath);
            var before = original.GetPixels32();
            var after = original.GetPixels32();
            var preview = new Texture2D(192 * 3, 480, TextureFormat.RGBA32, false);
            preview.SetPixels32(new Color32[preview.width * preview.height]);
            for (int y = 0; y < 480; y++)
            for (int x = 0; x < 192; x++)
            {
                preview.SetPixel(x, y, before[y * 192 + x]);
                preview.SetPixel(x + 192, y, after[y * 192 + x]);
                preview.SetPixel(x + 384, y, after[y * 192 + x]);
            }
            int anchors = 0;
            int[] counts = { 1, 4, 6, 4, 5 };
            for (int row = 0; row < 15; row++)
            for (int frame = 0; frame < 6; frame++)
            {
                bool found = FarmerEyes.TryAnchor(row, frame, out var anchor);
                if (frame >= counts[row / 3]) continue;
                Check(found, $"Guide coverage {row}:{frame}");
                anchors++;
                for (int style = 0; style < 2; style++)
                {
                    string key = style == 0 ? FarmerEyes.FirstStyle : FarmerEyes.SecondStyle;
                    var sprite = FarmerEyes.SpriteFor(key, row, frame, false);
                    Check((sprite == null) == (row % 3 == 2), "Rear view hides eyes");
                    if (sprite == null) continue;
                    Check(sprite.name.StartsWith($"eyes_{style}_"), "Selected style");
                    if (row == 7) Check(sprite.name.EndsWith("_2"), "Side run uses ordinary side eyes");
                    for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        Color p = sprite.texture.GetPixel((int)sprite.rect.x + x, (int)sprite.rect.y + y);
                        if (p.a == 0) continue;
                        int px = (style + 1) * 192 + frame * 32 + x + Mathf.RoundToInt(anchor.x - sprite.pivot.x);
                        int py = 480 - (row + 1) * 32 + y + Mathf.RoundToInt(anchor.y - sprite.pivot.y);
                        preview.SetPixel(px, py, p);
                    }
                }
            }
            Check(anchors == 60, "All 60 active frames have anchors");
            Check(FarmerEyes.SpriteFor("", 0, 0, false) == FarmerEyes.SpriteFor(FarmerEyes.FirstStyle, 0, 0, false),
                  "Old saves fall back to first style");
            Check(FarmerEyes.SpriteFor("", 0, 0, true).name.EndsWith("_1"), "Front blink");
            Check(FarmerEyes.SpriteFor("", 1, 0, true).name.EndsWith("_3"), "Side blink");
            Check(FarmerCatalog.Of(FarmerCategory.Eyes).Count == 2, "Two UI choices");
            var appearance = new PlayerAppearance { eyes = FarmerEyes.SecondStyle };
            var restored = JsonUtility.FromJson<PlayerAppearance>(JsonUtility.ToJson(appearance.Clone()));
            Check(restored.eyes == FarmerEyes.SecondStyle, "Selection survives clone and save roundtrip");

            var go = new GameObject("ValidationCharacter");
            var animator = go.AddComponent<FarmerAnimator>();
            animator.Init(restored);
            var advance = typeof(FarmerAnimator).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic);
            var eyeRenderer = go.transform.Find("Body/HeadAnchor/Eyes").GetComponent<SpriteRenderer>();
            animator.SetLocomotion(true, true, Direction.Right);
            advance.Invoke(animator, new object[] { 0f });
            Vector3 right = eyeRenderer.transform.parent.localPosition;
            animator.SetLocomotion(true, true, Direction.Left);
            advance.Invoke(animator, new object[] { 0f });
            Check(eyeRenderer.flipX && eyeRenderer.transform.parent.localPosition.x == -right.x, "Left anchor and sprite mirror together");
            animator.SetSortingOrder(100);
            Check(eyeRenderer.sortingOrder == 103, "Eyes follow world sorting");
            animator.SetLocomotion(false, false, Direction.Up);
            advance.Invoke(animator, new object[] { 0f });
            Check(!eyeRenderer.enabled, "Rear renderer disabled");
            animator.SetLocomotion(false, false, Direction.Down);
            advance.Invoke(animator, new object[] { 0f });
            animator.SetAppearance(new PlayerAppearance { eyes = FarmerEyes.FirstStyle });
            Check(eyeRenderer.enabled && eyeRenderer.sprite.name == "eyes_0_0", "Appearance refreshes immediately");
            var body = go.transform.Find("Body").GetComponent<SpriteRenderer>();
            Check(body.sprite.texture == original, "Body uses unmodified source texture");
            foreach (Direction direction in Enum.GetValues(typeof(Direction)))
            {
                Check(FarmerPoses.Get(FarmerAnim.Idle, direction).count == 1, "One Idle frame per direction");
                animator.SetLocomotion(false, false, direction);
                typeof(FarmerAnimator).GetField("_blinkTimer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(animator, 0f);
                advance.Invoke(animator, new object[] { 0f });
                var idleBody = body.sprite;
                var idleAnchor = eyeRenderer.transform.parent.localPosition;
                advance.Invoke(animator, new object[] { 3.4f });
                Check(body.sprite == idleBody && eyeRenderer.transform.parent.localPosition == idleAnchor, "Idle body and anchor stay still while blinking");
                if (direction != Direction.Up)
                    Check(eyeRenderer.sprite.name.EndsWith(direction == Direction.Down ? "_1" : "_3"), "Idle eyes close");
                advance.Invoke(animator, new object[] { 0.2f });
                Check(body.sprite == idleBody, "Idle stays on its first frame after blink");
                if (direction != Direction.Up)
                    Check(eyeRenderer.sprite.name.EndsWith(direction == Direction.Down ? "_0" : "_2"), "Idle eyes reopen");
            }
            UnityEngine.Object.DestroyImmediate(go);
            preview.Apply();
            File.WriteAllBytes("preview.png", preview.EncodeToPNG());
            File.WriteAllText("result.txt", $"PASS: {checks} checks, 60 frames, two eye styles, mirrored renderer and save roundtrip.");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText("result.txt", "FAIL: " + e);
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}

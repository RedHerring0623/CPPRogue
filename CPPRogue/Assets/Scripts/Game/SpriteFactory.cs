using UnityEngine;

namespace CPPRogue.Game
{
    /// <summary>运行时生成圆形贴图——演示阶段没有美术资源，用代码画圆。</summary>
    public static class SpriteFactory
    {
        /// <summary>生成直径 1 个世界单位的圆形 Sprite（pixelsPerUnit = size）。</summary>
        public static Sprite CreateCircle(int size, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
            };
            float r = size * 0.5f - 1f;
            float c = size * 0.5f - 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(r - d + 0.5f); // 约 1px 软边
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}

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

        /// <summary>生成默认朝上的实心三角 Sprite（撤离指引箭头）。</summary>
        public static Sprite CreateTriangle(int size, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
            };
            float c = size * 0.5f - 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                // 底边最宽，往上收拢到顶点
                float t = Mathf.InverseLerp(1f, size - 2f, y);
                float half = Mathf.Lerp((size - 3f) * 0.5f, 1f, t);
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(half - Mathf.Abs(x - c) + 0.5f)
                            * Mathf.Clamp01(y - 0.5f)
                            * Mathf.Clamp01(size - 1.5f - y);
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}

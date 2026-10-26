using UnityEngine;

namespace BlockBlast
{
    /// <summary>Generates every sprite and sound at runtime, so the project needs zero imported assets.</summary>
    public static class Art
    {
        static Sprite cell, square;

        public static Sprite Cell
        {
            get { if (cell == null) cell = MakeCell(); return cell; }
        }

        public static Sprite Square
        {
            get { if (square == null) square = MakeSquare(); return square; }
        }

        static Sprite MakeCell()
        {
            const int N = 64;
            const float radius = 14f;
            const float inset = 2f;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var px = new Color[N * N];
            float half = N * 0.5f;
            float box = half - inset - radius;

            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - box, 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - box, 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;      // < 0 inside
                    float alpha = Mathf.Clamp01(0.5f - d);                  // anti-aliased edge

                    float vertical = Mathf.Lerp(0.80f, 1.08f, y / (float)(N - 1));  // darker bottom, lighter top
                    float bevel = Mathf.Lerp(0.82f, 1f, Mathf.Clamp01(-d / 7f));    // soft inner bevel
                    float v = Mathf.Clamp01(vertical * bevel);
                    px[y * N + x] = new Color(v, v, v, alpha);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);   // 1 world unit
        }

        static Sprite MakeSquare()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);  // 1 world unit
        }

        /// <summary>A short sine blip that glides from freq to slideTo.</summary>
        public static AudioClip Tone(float freq, float seconds, float volume, float slideTo)
        {
            const int rate = 44100;
            int n = Mathf.Max(1, (int)(rate * seconds));
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(freq, slideTo, t);
                phase += 2f * Mathf.PI * f / rate;
                float env = Mathf.Min(1f, i / 200f) * (1f - t) * (1f - t);
                data[i] = Mathf.Sin(phase) * env * volume;
            }
            var clip = AudioClip.Create("tone", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

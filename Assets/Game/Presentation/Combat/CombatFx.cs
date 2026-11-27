using System.Collections.Generic;
using UnityEngine;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// World-space combat visuals with generated textures and pooled objects: bullet tracers, impact
    /// marks with sparks or dust, blood puffs, remote muzzle flashes. Uses the always-included
    /// Sprites/Default shader so it works in every build without extra assets.
    /// </summary>
    public sealed class CombatFx : MonoBehaviour
    {
        static Sprite _flash, _dot, _decal;
        static Material _lineMat;

        public static Sprite FlashSprite => _flash != null ? _flash : (_flash = MakeFlash());
        static Sprite Dot => _dot != null ? _dot : (_dot = MakeDot(32, 0.5f));
        static Sprite Decal => _decal != null ? _decal : (_decal = MakeDecal());
        static Material LineMat => _lineMat != null ? _lineMat : (_lineMat = new Material(Shader.Find("Sprites/Default")));

        struct Particle
        {
            public Transform T;
            public SpriteRenderer R;
            public Vector3 Velocity;
            public float Life, MaxLife;
            public Color Color;
            public float Size;
            public bool Gravity;
        }

        struct TracerLine
        {
            public LineRenderer L;
            public float Life;
        }

        readonly List<Particle> _particles = new List<Particle>(256);
        readonly Stack<SpriteRenderer> _spritePool = new Stack<SpriteRenderer>();
        readonly List<TracerLine> _tracers = new List<TracerLine>(32);
        readonly Stack<LineRenderer> _linePool = new Stack<LineRenderer>();
        readonly Queue<GameObject> _decals = new Queue<GameObject>();
        Camera _camera;
        const int MaxDecals = 80;

        public static CombatFx Create(Camera camera)
        {
            var go = new GameObject("CombatFx");
            var fx = go.AddComponent<CombatFx>();
            fx._camera = camera;
            return fx;
        }

        public void Tracer(Vector3 from, Vector3 to, bool heavy)
        {
            var l = _linePool.Count > 0 ? _linePool.Pop() : NewLine();
            l.gameObject.SetActive(true);
            // Draw only a streak travelling along the path for a realistic look.
            Vector3 dir = to - from;
            float len = dir.magnitude;
            Vector3 start = from + dir * Mathf.Min(0.15f, 2f / Mathf.Max(len, 0.01f));
            l.SetPosition(0, start);
            l.SetPosition(1, to);
            l.widthMultiplier = heavy ? 0.025f : 0.014f;
            _tracers.Add(new TracerLine { L = l, Life = 0.06f });
        }

        LineRenderer NewLine()
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(transform, false);
            var l = go.AddComponent<LineRenderer>();
            l.sharedMaterial = LineMat;
            l.positionCount = 2;
            l.numCapVertices = 2;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            l.receiveShadows = false;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.95f, 0.75f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 1f) });
            l.colorGradient = g;
            return l;
        }

        public void Impact(Vector3 point, Vector3 normal)
        {
            if (normal == Vector3.zero) normal = Vector3.up;
            var d = new GameObject("BulletHole");
            d.transform.SetParent(transform, false);
            d.transform.position = point + normal * 0.004f;
            d.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            d.transform.localScale = Vector3.one * Random.Range(0.05f, 0.075f);
            var sr = d.AddComponent<SpriteRenderer>();
            sr.sprite = Decal;
            _decals.Enqueue(d);
            if (_decals.Count > MaxDecals) Destroy(_decals.Dequeue());

            for (int i = 0; i < 6; i++)
                Spawn(point, (normal + Random.insideUnitSphere * 0.7f).normalized * Random.Range(2f, 5f), new Color(1f, 0.75f, 0.35f), 0.025f, Random.Range(0.12f, 0.25f), true);
            for (int i = 0; i < 3; i++)
                Spawn(point, (normal + Random.insideUnitSphere * 0.4f) * Random.Range(0.3f, 0.8f), new Color(0.55f, 0.52f, 0.48f, 0.6f), 0.12f, Random.Range(0.4f, 0.7f), false);
        }

        public void Blood(Vector3 point, Vector3 shotDir)
        {
            for (int i = 0; i < 8; i++)
                Spawn(point, (shotDir.normalized + Random.insideUnitSphere * 0.6f) * Random.Range(0.8f, 2.5f), new Color(0.5f, 0.02f, 0.02f, 0.9f), Random.Range(0.04f, 0.09f), Random.Range(0.25f, 0.5f), true);
        }

        public void MuzzleFlash(Vector3 at)
        {
            Spawn(at, Vector3.zero, new Color(1f, 0.8f, 0.4f), 0.35f, 0.05f, false, FlashSprite);
        }

        void Spawn(Vector3 pos, Vector3 vel, Color color, float size, float life, bool gravity, Sprite sprite = null)
        {
            var r = _spritePool.Count > 0 ? _spritePool.Pop() : NewSprite();
            r.gameObject.SetActive(true);
            r.sprite = sprite != null ? sprite : Dot;
            r.color = color;
            r.transform.position = pos;
            r.transform.localScale = Vector3.one * size;
            _particles.Add(new Particle { T = r.transform, R = r, Velocity = vel, Life = life, MaxLife = life, Color = color, Size = size, Gravity = gravity });
        }

        SpriteRenderer NewSprite()
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(transform, false);
            return go.AddComponent<SpriteRenderer>();
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Quaternion face = _camera != null ? _camera.transform.rotation : Quaternion.identity;
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    p.R.gameObject.SetActive(false);
                    _spritePool.Push(p.R);
                    _particles.RemoveAt(i);
                    continue;
                }
                if (p.Gravity) p.Velocity += Physics.gravity * dt;
                p.T.position += p.Velocity * dt;
                p.T.rotation = face;
                float k = p.Life / p.MaxLife;
                var c = p.Color;
                c.a *= k;
                p.R.color = c;
                _particles[i] = p;
            }
            for (int i = _tracers.Count - 1; i >= 0; i--)
            {
                var t = _tracers[i];
                t.Life -= dt;
                if (t.Life <= 0f)
                {
                    t.L.gameObject.SetActive(false);
                    _linePool.Push(t.L);
                    _tracers.RemoveAt(i);
                    continue;
                }
                _tracers[i] = t;
            }
        }

        // ------------------------------------------------------------------ textures

        static Sprite MakeDot(int size, float hardness)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01((1f - d) / (1f - hardness * 0.9f));
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeFlash()
        {
            const int size = 128;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var rng = new System.Random(7);
            float[] petals = new float[7];
            for (int i = 0; i < petals.Length; i++) petals[i] = 0.55f + (float)rng.NextDouble() * 0.45f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f) / (size * 0.5f);
                    float ang = (Mathf.Atan2(p.y, p.x) / (Mathf.PI * 2f) + 1f) % 1f;
                    float fi = ang * petals.Length;
                    int i0 = (int)fi % petals.Length, i1 = (i0 + 1) % petals.Length;
                    float reach = Mathf.Lerp(petals[i0], petals[i1], Mathf.SmoothStep(0f, 1f, fi - Mathf.Floor(fi)));
                    float star = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * Mathf.PI * petals.Length)), 3f) * reach;
                    float d = p.magnitude;
                    float core = Mathf.Clamp01(1f - d / 0.35f);
                    float a = Mathf.Clamp01(Mathf.Max(core, (star - d) * 2.2f));
                    Color c = Color.Lerp(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.97f, 0.85f), core);
                    c.a = a;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeDecal()
        {
            const int size = 64;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var rng = new System.Random(11);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float noise = (float)rng.NextDouble() * 0.25f;
                    float hole = d < 0.22f ? 1f : 0f;
                    float scorch = Mathf.Clamp01(1f - (d + noise) * 1.1f) * 0.75f;
                    float a = Mathf.Max(hole, scorch);
                    Color c = hole > 0f ? new Color(0.02f, 0.02f, 0.02f) : new Color(0.12f, 0.1f, 0.09f);
                    c.a = a;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}

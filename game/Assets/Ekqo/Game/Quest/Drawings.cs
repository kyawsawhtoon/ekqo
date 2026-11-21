using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ekqo.Game
{
    /// <summary>
    /// Placeholder art drawn in code with UI Toolkit's Painter2D, so Quest 1 is playable before
    /// the real art exists. Each piece is one element; swap it for sprites later without
    /// touching the quest flow.
    /// </summary>
    internal static class Draw
    {
        public static void Circle(Painter2D p, Vector2 c, float r, Color fill)
        {
            p.fillColor = fill;
            p.BeginPath();
            p.Arc(c, r, Angle.Degrees(0), Angle.Degrees(360));
            p.ClosePath();
            p.Fill();
        }

        public static void Rect(Painter2D p, Rect r, Color fill)
        {
            p.fillColor = fill;
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMax));
            p.LineTo(new Vector2(r.xMin, r.yMax));
            p.ClosePath();
            p.Fill();
        }

        public static void RoundRect(Painter2D p, Rect r, float radius, Color fill)
        {
            radius = Mathf.Min(radius, r.width / 2, r.height / 2);
            p.fillColor = fill;
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin + radius, r.yMin));
            p.ArcTo(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), radius);
            p.ArcTo(new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), radius);
            p.ArcTo(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), radius);
            p.ArcTo(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), radius);
            p.ClosePath();
            p.Fill();
        }

        public static void Star(Painter2D p, Vector2 c, float r, Color fill, float rotation = 0)
        {
            p.fillColor = fill;
            p.BeginPath();
            for (var i = 0; i < 10; i++)
            {
                var a = (rotation - 90 + i * 36) * Mathf.Deg2Rad;
                var rr = i % 2 == 0 ? r : r * 0.45f;
                var pt = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
            }
            p.ClosePath();
            p.Fill();
        }

        public static void Stroke(Painter2D p, Color color, float width, params Vector2[] points)
        {
            p.strokeColor = color;
            p.lineWidth = width;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(points[0]);
            for (var i = 1; i < points.Length; i++) p.LineTo(points[i]);
            p.Stroke();
        }

        public static void ArcStroke(Painter2D p, Vector2 c, float r, float from, float to, Color color, float width)
        {
            p.strokeColor = color;
            p.lineWidth = width;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.Arc(c, r, Angle.Degrees(from), Angle.Degrees(to));
            p.Stroke();
        }

        /// <summary>A soft glow: stacked translucent circles.</summary>
        public static void Glow(Painter2D p, Vector2 c, float r, Color color, float strength)
        {
            for (var i = 6; i >= 1; i--)
            {
                var k = i / 6f;
                Circle(p, c, r * (1 + k * 1.4f), new Color(color.r, color.g, color.b, color.a * strength * 0.12f * (1.2f - k)));
            }
        }

        public static void Moon(Painter2D p, Vector2 c, float r, Color moon, Color sky)
        {
            Circle(p, c, r, moon);
            Circle(p, c + new Vector2(r * 0.45f, -r * 0.25f), r * 0.85f, sky);
        }
    }

    /// <summary>The night garden: sky, twinkling stars, moon, grass and the bush Ekqo hides in.</summary>
    internal sealed class GardenView : VisualElement
    {
        public static readonly Color Sky = new Color(0.106f, 0.114f, 0.227f);
        private static readonly Color SkyLow = new Color(0.20f, 0.18f, 0.38f);
        private readonly List<Vector3> _stars = new List<Vector3>();
        private float _time;

        public GardenView()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            var rng = new System.Random(7);
            for (var i = 0; i < 60; i++) _stars.Add(new Vector3((float)rng.NextDouble(), (float)rng.NextDouble() * 0.6f, (float)rng.NextDouble() * 6.28f));
            generateVisualContent += Paint;
        }

        public void Tick(float time) { _time = time; MarkDirtyRepaint(); }

        private void Paint(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var r = contentRect;
            if (r.width <= 0) return;
            const int bands = 24;
            for (var i = 0; i < bands; i++)
            {
                var t = i / (float)(bands - 1);
                Draw.Rect(p, new Rect(0, r.height * 0.64f * i / bands, r.width, r.height * 0.64f / bands + 1), Color.Lerp(Sky, SkyLow, t));
            }
            foreach (var s in _stars)
            {
                var a = 0.5f + 0.5f * Mathf.Sin(_time * 1.5f + s.z);
                Draw.Star(p, new Vector2(s.x * r.width, s.y * r.height), 3 + 3 * a, new Color(1f, 0.97f, 0.8f, 0.35f + 0.6f * a), s.z * 20);
            }
            Draw.Moon(p, new Vector2(r.width * 0.86f, r.height * 0.14f), r.height * 0.06f, new Color(1f, 0.95f, 0.75f), Color.Lerp(Sky, SkyLow, 0.2f));

            // Grandma's house wall and a lit window on the left.
            Draw.Rect(p, new Rect(0, r.height * 0.24f, r.width * 0.16f, r.height * 0.40f), new Color(0.16f, 0.13f, 0.24f));
            Draw.RoundRect(p, new Rect(r.width * 0.04f, r.height * 0.32f, r.width * 0.08f, r.height * 0.11f), 6, new Color(1f, 0.82f, 0.45f, 0.9f));
            // Grass.
            Draw.Rect(p, new Rect(0, r.height * 0.62f, r.width, r.height * 0.38f), new Color(0.09f, 0.20f, 0.17f));
            Draw.Rect(p, new Rect(0, r.height * 0.62f, r.width, r.height * 0.02f), new Color(0.13f, 0.28f, 0.22f));
            // A few flowers.
            for (var i = 0; i < 7; i++)
            {
                var x = r.width * (0.2f + 0.11f * i);
                var y = r.height * (0.68f + 0.04f * Mathf.Sin(i * 2.3f));
                Draw.Circle(p, new Vector2(x, y), 5, new Color(0.95f, 0.6f + 0.05f * i, 0.75f, 0.8f));
            }
        }
    }

    /// <summary>The bush in front of Ekqo. Rustles when shake &gt; 0.</summary>
    internal sealed class BushView : VisualElement
    {
        private float _time;
        public float Shake;

        public BushView()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        public void Tick(float time) { _time = time; Shake = Mathf.MoveTowards(Shake, 0, 0.02f); MarkDirtyRepaint(); }

        private void Paint(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var r = contentRect;
            var w = r.width; var h = r.height;
            var dx = Mathf.Sin(_time * 30) * 6 * Shake;
            var dark = new Color(0.10f, 0.30f, 0.20f);
            var mid = new Color(0.14f, 0.40f, 0.26f);
            var light = new Color(0.20f, 0.50f, 0.32f);
            Draw.Circle(p, new Vector2(w * 0.20f + dx, h * 0.62f), h * 0.36f, dark);
            Draw.Circle(p, new Vector2(w * 0.80f - dx, h * 0.62f), h * 0.36f, dark);
            Draw.Circle(p, new Vector2(w * 0.35f - dx, h * 0.45f), h * 0.40f, mid);
            Draw.Circle(p, new Vector2(w * 0.65f + dx, h * 0.48f), h * 0.38f, mid);
            Draw.Circle(p, new Vector2(w * 0.50f + dx, h * 0.62f), h * 0.36f, light);
            Draw.Rect(p, new Rect(w * 0.08f, h * 0.62f, w * 0.84f, h * 0.38f), mid);
            for (var i = 0; i < 4; i++)
                Draw.Circle(p, new Vector2(w * (0.22f + 0.18f * i) + dx, h * (0.45f + 0.1f * (i % 2))), 4, new Color(1f, 0.85f, 0.9f, 0.8f));
        }
    }

    /// <summary>
    /// Ekqo at Stage 1 (Spark): a tiny, muddy light spirit. Glow runs 0 (dim) to 1 (bright).
    /// </summary>
    internal sealed class EkqoView : VisualElement
    {
        public float Glow = 0.15f;       // eased toward TargetGlow
        public float TargetGlow = 0.15f;
        public bool Sleepy;
        public bool EyesOpen = true;
        public float Talk;               // mouth movement while a line plays
        public float Happy;              // bounce after a right answer
        public float Mud = 1;            // mud spots until the bath in Q3
        private float _time;
        private float _blinkAt = 2;

        public EkqoView()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        public void Tick(float time, float dt)
        {
            _time = time;
            Glow = Mathf.MoveTowards(Glow, TargetGlow, dt * 0.6f);
            Happy = Mathf.MoveTowards(Happy, 0, dt * 0.8f);
            if (time > _blinkAt + 0.15f) _blinkAt = time + 2 + Random.value * 3;
            var bob = Mathf.Sin(time * (Sleepy ? 1.2f : 2.4f)) * (Sleepy ? 3 : 6) - Mathf.Abs(Mathf.Sin(time * 10)) * 26 * Happy;
            style.translate = new Translate(0, bob);
            MarkDirtyRepaint();
        }

        private void Paint(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var r = contentRect;
            var c = r.center;
            var rad = Mathf.Min(r.width, r.height) * 0.26f;
            var core = Color.Lerp(new Color(0.75f, 0.72f, 0.55f), new Color(1f, 0.97f, 0.78f), Glow);

            Draw.Glow(p, c, rad, new Color(1f, 0.92f, 0.55f, 1f), 0.25f + Glow);
            Draw.Circle(p, c, rad, core);
            Draw.Circle(p, c + new Vector2(-rad * 0.3f, -rad * 0.35f), rad * 0.35f, new Color(1, 1, 1, 0.35f + 0.3f * Glow));
            // A little flame tuft on top.
            p.fillColor = core;
            p.BeginPath();
            p.MoveTo(c + new Vector2(-rad * 0.35f, -rad * 0.85f));
            p.QuadraticCurveTo(c + new Vector2(rad * 0.1f, -rad * 1.9f + Mathf.Sin(_time * 3) * 6), c + new Vector2(rad * 0.4f, -rad * 0.8f));
            p.ClosePath();
            p.Fill();

            if (Mud > 0)
            {
                var mud = new Color(0.42f, 0.30f, 0.20f, 0.8f * Mud);
                Draw.Circle(p, c + new Vector2(rad * 0.55f, rad * 0.35f), rad * 0.16f, mud);
                Draw.Circle(p, c + new Vector2(-rad * 0.6f, rad * 0.45f), rad * 0.12f, mud);
                Draw.Circle(p, c + new Vector2(rad * 0.15f, rad * 0.7f), rad * 0.10f, mud);
            }

            var eye = new Color(0.16f, 0.12f, 0.24f);
            var blink = _time > _blinkAt && _time < _blinkAt + 0.15f;
            var el = c + new Vector2(-rad * 0.32f, -rad * 0.05f);
            var er = c + new Vector2(rad * 0.32f, -rad * 0.05f);
            if (Sleepy || !EyesOpen || blink)
            {
                Draw.ArcStroke(p, el, rad * 0.14f, 20, 160, eye, 4);
                Draw.ArcStroke(p, er, rad * 0.14f, 20, 160, eye, 4);
            }
            else
            {
                Draw.Circle(p, el, rad * 0.14f, eye);
                Draw.Circle(p, er, rad * 0.14f, eye);
                Draw.Circle(p, el + new Vector2(rad * 0.05f, -rad * 0.05f), rad * 0.05f, Color.white);
                Draw.Circle(p, er + new Vector2(rad * 0.05f, -rad * 0.05f), rad * 0.05f, Color.white);
            }
            // Cheeks and mouth.
            Draw.Circle(p, c + new Vector2(-rad * 0.55f, rad * 0.2f), rad * 0.1f, new Color(1f, 0.6f, 0.6f, 0.5f));
            Draw.Circle(p, c + new Vector2(rad * 0.55f, rad * 0.2f), rad * 0.1f, new Color(1f, 0.6f, 0.6f, 0.5f));
            var open = Talk > 0 ? Mathf.Abs(Mathf.Sin(_time * 14)) * Talk : 0;
            if (open > 0.1f) Draw.Circle(p, c + new Vector2(0, rad * 0.32f), rad * 0.06f + rad * 0.08f * open, eye);
            else Draw.ArcStroke(p, c + new Vector2(0, rad * 0.22f), rad * 0.14f, 20, 160, eye, 3.5f);
        }
    }

    /// <summary>The falling light in the opening scene. Progress runs 0 to 1.</summary>
    internal sealed class FallingLight : VisualElement
    {
        public float Progress = -1;
        public Vector2 From, To;

        public FallingLight()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            generateVisualContent += Paint;
        }

        private void Paint(MeshGenerationContext ctx)
        {
            if (Progress < 0 || Progress > 1.3f) return;
            var p = ctx.painter2D;
            var r = contentRect;
            var a = new Vector2(From.x * r.width, From.y * r.height);
            var b = new Vector2(To.x * r.width, To.y * r.height);
            if (Progress <= 1)
            {
                var t = Progress * Progress;
                var pos = Vector2.Lerp(a, b, t);
                for (var i = 1; i <= 8; i++)
                    Draw.Circle(p, Vector2.Lerp(a, b, Mathf.Max(0, t - i * 0.03f)), 10 - i, new Color(1f, 0.95f, 0.7f, 0.5f - i * 0.05f));
                Draw.Glow(p, pos, 12, new Color(1f, 0.95f, 0.6f), 1.2f);
                Draw.Circle(p, pos, 10, new Color(1f, 1f, 0.9f));
            }
            else
            {
                var k = (Progress - 1) / 0.3f;
                Draw.Glow(p, b, 30 + 80 * k, new Color(1f, 0.95f, 0.6f), 1.5f * (1 - k));
            }
        }
    }

    /// <summary>A picture for a concept (placeholder art). Unknown concepts get a soft light.</summary>
    internal sealed class PictureView : VisualElement
    {
        private readonly string _concept;

        public PictureView(string concept)
        {
            _concept = concept;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        public static bool Has(string concept) => concept != null && Known.Contains(concept);

        private static readonly HashSet<string> Known = new HashSet<string>
        {
            "greet.hello", "greet.bye", "ans.yes", "ans.no", "word.name", "sky.star", "nature.light",
            "time.night", "place.garden", "q.who", "phrase.dont_be_scared",
        };

        private void Paint(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var r = contentRect;
            var c = r.center;
            var s = Mathf.Min(r.width, r.height) * 0.42f;
            var ink = new Color(0.16f, 0.12f, 0.24f);
            var sun = new Color(1f, 0.83f, 0.35f);
            switch (_concept)
            {
                case "greet.hello":
                    Draw.Circle(p, c + new Vector2(-s * 0.15f, s * 0.05f), s * 0.7f, sun);
                    Draw.Circle(p, c + new Vector2(-s * 0.38f, -s * 0.1f), s * 0.08f, ink);
                    Draw.Circle(p, c + new Vector2(s * 0.08f, -s * 0.1f), s * 0.08f, ink);
                    Draw.ArcStroke(p, c + new Vector2(-s * 0.15f, s * 0.1f), s * 0.32f, 20, 160, ink, 5);
                    Hand(p, c + new Vector2(s * 0.72f, -s * 0.35f), s * 0.32f, new Color(1f, 0.7f, 0.5f));
                    Draw.ArcStroke(p, c + new Vector2(s * 0.72f, -s * 0.35f), s * 0.55f, -80, -20, ink, 3);
                    break;
                case "greet.bye":
                    Draw.Moon(p, c + new Vector2(s * 0.45f, -s * 0.5f), s * 0.3f, new Color(1f, 0.95f, 0.7f), new Color(0.98f, 0.95f, 0.88f));
                    Hand(p, c + new Vector2(-s * 0.15f, s * 0.1f), s * 0.55f, new Color(1f, 0.7f, 0.5f));
                    Draw.ArcStroke(p, c + new Vector2(-s * 0.15f, s * 0.1f), s * 0.9f, 200, 250, ink, 3);
                    Draw.ArcStroke(p, c + new Vector2(-s * 0.15f, s * 0.1f), s * 0.9f, -70, -20, ink, 3);
                    break;
                case "ans.yes":
                    Draw.Circle(p, c, s * 0.85f, new Color(0.45f, 0.78f, 0.5f));
                    Draw.Stroke(p, Color.white, s * 0.18f, c + new Vector2(-s * 0.4f, 0), c + new Vector2(-s * 0.1f, s * 0.3f), c + new Vector2(s * 0.45f, -s * 0.35f));
                    break;
                case "ans.no":
                    Draw.Circle(p, c, s * 0.85f, new Color(0.93f, 0.48f, 0.5f));
                    Draw.Stroke(p, Color.white, s * 0.18f, c + new Vector2(-s * 0.35f, -s * 0.35f), c + new Vector2(s * 0.35f, s * 0.35f));
                    Draw.Stroke(p, Color.white, s * 0.18f, c + new Vector2(s * 0.35f, -s * 0.35f), c + new Vector2(-s * 0.35f, s * 0.35f));
                    break;
                case "word.name":
                    Draw.RoundRect(p, new Rect(c.x - s * 0.95f, c.y - s * 0.55f, s * 1.9f, s * 1.1f), s * 0.18f, new Color(0.45f, 0.62f, 0.95f));
                    Draw.RoundRect(p, new Rect(c.x - s * 0.8f, c.y - s * 0.15f, s * 1.6f, s * 0.55f), s * 0.1f, Color.white);
                    Draw.Rect(p, new Rect(c.x - s * 0.95f, c.y - s * 0.42f, s * 1.9f, s * 0.12f), new Color(1, 1, 1, 0.5f));
                    Draw.Stroke(p, ink, 4, c + new Vector2(-s * 0.6f, s * 0.12f), c + new Vector2(s * 0.5f, s * 0.12f));
                    break;
                case "sky.star":
                    Draw.Glow(p, c, s * 0.6f, new Color(1f, 0.9f, 0.4f), 0.8f);
                    Draw.Star(p, c, s * 0.9f, sun);
                    break;
                case "nature.light":
                    for (var i = 0; i < 8; i++)
                    {
                        var a = i * 45 * Mathf.Deg2Rad;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Draw.Stroke(p, sun, 6, c + d * s * 0.55f, c + d * s * 0.9f);
                    }
                    Draw.Glow(p, c, s * 0.4f, new Color(1f, 0.95f, 0.6f), 1f);
                    Draw.Circle(p, c, s * 0.4f, new Color(1f, 0.97f, 0.8f));
                    break;
                case "time.night":
                    Draw.Circle(p, c, s * 0.95f, GardenView.Sky);
                    Draw.Moon(p, c + new Vector2(-s * 0.1f, 0), s * 0.45f, new Color(1f, 0.95f, 0.7f), GardenView.Sky);
                    Draw.Star(p, c + new Vector2(s * 0.5f, -s * 0.4f), s * 0.12f, sun);
                    Draw.Star(p, c + new Vector2(s * 0.45f, s * 0.4f), s * 0.09f, sun);
                    break;
                case "place.garden":
                    Draw.Circle(p, c + new Vector2(-s * 0.4f, s * 0.2f), s * 0.45f, new Color(0.2f, 0.55f, 0.35f));
                    Draw.Circle(p, c + new Vector2(s * 0.4f, s * 0.2f), s * 0.45f, new Color(0.2f, 0.55f, 0.35f));
                    Draw.Circle(p, c + new Vector2(0, -s * 0.1f), s * 0.5f, new Color(0.25f, 0.62f, 0.4f));
                    Draw.Rect(p, new Rect(c.x - s * 0.85f, c.y + s * 0.2f, s * 1.7f, s * 0.45f), new Color(0.2f, 0.55f, 0.35f));
                    Draw.Circle(p, c + new Vector2(-s * 0.3f, -s * 0.2f), s * 0.1f, new Color(1f, 0.6f, 0.75f));
                    Draw.Circle(p, c + new Vector2(s * 0.35f, 0), s * 0.1f, new Color(1f, 0.85f, 0.4f));
                    break;
                case "q.who":
                    Draw.Circle(p, c, s * 0.85f, new Color(0.62f, 0.55f, 0.9f));
                    Draw.ArcStroke(p, c + new Vector2(0, -s * 0.25f), s * 0.28f, 180, 400, Color.white, s * 0.16f);
                    Draw.Stroke(p, Color.white, s * 0.16f, c + new Vector2(0, s * 0.03f), c + new Vector2(0, s * 0.2f));
                    Draw.Circle(p, c + new Vector2(0, s * 0.48f), s * 0.1f, Color.white);
                    break;
                case "phrase.dont_be_scared":
                    Heart(p, c, s * 0.8f, new Color(1f, 0.5f, 0.6f));
                    break;
                default:
                    Draw.Glow(p, c, s * 0.5f, new Color(1f, 0.95f, 0.6f), 1f);
                    Draw.Circle(p, c, s * 0.5f, new Color(1f, 0.97f, 0.8f));
                    break;
            }
        }

        private static void Hand(Painter2D p, Vector2 c, float s, Color skin)
        {
            Draw.RoundRect(p, new Rect(c.x - s * 0.5f, c.y - s * 0.2f, s, s * 0.9f), s * 0.3f, skin);
            for (var i = 0; i < 4; i++)
                Draw.RoundRect(p, new Rect(c.x - s * 0.5f + i * s * 0.26f, c.y - s * 0.85f + Mathf.Abs(i - 1.5f) * s * 0.12f, s * 0.22f, s * 0.8f), s * 0.11f, skin);
            Draw.RoundRect(p, new Rect(c.x - s * 0.85f, c.y, s * 0.5f, s * 0.22f), s * 0.11f, skin);
        }

        private static void Heart(Painter2D p, Vector2 c, float s, Color fill)
        {
            p.fillColor = fill;
            p.BeginPath();
            p.MoveTo(c + new Vector2(0, s * 0.8f));
            p.BezierCurveTo(c + new Vector2(-s * 1.2f, 0), c + new Vector2(-s * 0.6f, -s * 0.9f), c + new Vector2(0, -s * 0.35f));
            p.BezierCurveTo(c + new Vector2(s * 0.6f, -s * 0.9f), c + new Vector2(s * 1.2f, 0), c + new Vector2(0, s * 0.8f));
            p.ClosePath();
            p.Fill();
        }
    }

    /// <summary>Small icons for buttons and reward cards.</summary>
    internal sealed class IconView : VisualElement
    {
        public enum Kind { Next, Speaker, WordBook, Jar, Page }
        private readonly Kind _kind;
        public Color Ink = new Color(0.16f, 0.12f, 0.24f);
        public float Glow;

        public IconView(Kind kind)
        {
            _kind = kind;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        private void Paint(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var r = contentRect;
            var c = r.center;
            var s = Mathf.Min(r.width, r.height) * 0.4f;
            switch (_kind)
            {
                case Kind.Next:
                    p.fillColor = Ink;
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(-s * 0.45f, -s * 0.65f));
                    p.LineTo(c + new Vector2(s * 0.7f, 0));
                    p.LineTo(c + new Vector2(-s * 0.45f, s * 0.65f));
                    p.ClosePath();
                    p.Fill();
                    break;
                case Kind.Speaker:
                    p.fillColor = Ink;
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(-s * 0.8f, -s * 0.3f));
                    p.LineTo(c + new Vector2(-s * 0.4f, -s * 0.3f));
                    p.LineTo(c + new Vector2(s * 0.1f, -s * 0.75f));
                    p.LineTo(c + new Vector2(s * 0.1f, s * 0.75f));
                    p.LineTo(c + new Vector2(-s * 0.4f, s * 0.3f));
                    p.LineTo(c + new Vector2(-s * 0.8f, s * 0.3f));
                    p.ClosePath();
                    p.Fill();
                    Draw.ArcStroke(p, c + new Vector2(s * 0.1f, 0), s * 0.4f, -45, 45, Ink, 4);
                    Draw.ArcStroke(p, c + new Vector2(s * 0.1f, 0), s * 0.75f, -45, 45, Ink, 4);
                    break;
                case Kind.WordBook:
                    Draw.RoundRect(p, new Rect(c.x - s, c.y - s * 0.75f, s * 2, s * 1.5f), s * 0.12f, new Color(0.55f, 0.35f, 0.75f));
                    Draw.Rect(p, new Rect(c.x - s * 0.06f, c.y - s * 0.75f, s * 0.12f, s * 1.5f), new Color(0.4f, 0.25f, 0.6f));
                    Draw.Star(p, c + new Vector2(-s * 0.5f, 0), s * 0.28f, new Color(1f, 0.85f, 0.4f));
                    Draw.Star(p, c + new Vector2(s * 0.5f, 0), s * 0.28f, new Color(1f, 0.85f, 0.4f));
                    break;
                case Kind.Jar:
                    Draw.Glow(p, c + new Vector2(0, s * 0.15f), s * 0.45f, new Color(1f, 0.92f, 0.55f), 0.6f + Glow);
                    Draw.RoundRect(p, new Rect(c.x - s * 0.7f, c.y - s * 0.6f, s * 1.4f, s * 1.5f), s * 0.35f, new Color(0.75f, 0.9f, 1f, 0.35f));
                    Draw.Circle(p, c + new Vector2(0, s * 0.2f), s * 0.35f, new Color(1f, 0.97f, 0.78f));
                    Draw.ArcStroke(p, c + new Vector2(-s * 0.12f, s * 0.18f), s * 0.06f, 20, 160, Ink, 3);
                    Draw.ArcStroke(p, c + new Vector2(s * 0.12f, s * 0.18f), s * 0.06f, 20, 160, Ink, 3);
                    Draw.RoundRect(p, new Rect(c.x - s * 0.55f, c.y - s * 0.85f, s * 1.1f, s * 0.3f), s * 0.08f, new Color(0.8f, 0.55f, 0.35f));
                    break;
                case Kind.Page:
                    Draw.RoundRect(p, new Rect(c.x - s * 0.75f, c.y - s, s * 1.5f, s * 2), s * 0.12f, new Color(1f, 0.97f, 0.9f));
                    Draw.Circle(p, c + new Vector2(0, -s * 0.3f), s * 0.35f, new Color(1f, 0.92f, 0.6f));
                    for (var i = 0; i < 3; i++)
                        Draw.Stroke(p, new Color(0.6f, 0.55f, 0.7f), 4, c + new Vector2(-s * 0.5f, s * (0.3f + 0.22f * i)), c + new Vector2(s * 0.5f, s * (0.3f + 0.22f * i)));
                    break;
            }
        }
    }
}

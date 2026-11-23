using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ekqo.Game
{
    /// <summary>
    /// Drawing helpers and the pieces still drawn in code with UI Toolkit's Painter2D (the
    /// falling light, button icons, and fallback pictures). The world, Ekqo, the bush and the
    /// word pictures are pixel art from PixelArt.txt (see PixelArt.cs).
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

    /// <summary>Night colours shared by the screens.</summary>
    internal static class Night
    {
        public static readonly Color Sky = new Color(0.106f, 0.114f, 0.227f);
        /// <summary>Characters are tinted less than the map, so they stand out at night.</summary>
        public static readonly Color Character = Color.Lerp(Color.white, PixelArt.NightTint, 0.45f);
    }

    /// <summary>The big bush Ekqo hides in: the bush sprite two tiles wide. Rustles when Shake &gt; 0.</summary>
    internal sealed class BushView : PixelSprite
    {
        public float Shake;

        public BushView(Vector2 pos) : base("bush")
        {
            Pos = pos;
            Size = new Vector2(32, 32);
            style.unityBackgroundImageTintColor = PixelArt.NightTint;
        }

        public void Tick(float time)
        {
            Shake = Mathf.MoveTowards(Shake, 0, 0.02f);
            Nudge = new Vector2(Mathf.Sin(time * 30) * 1.5f * Shake, 0);
        }
    }

    /// <summary>The soft light around Ekqo, drawn behind him.</summary>
    internal sealed class HaloView : PixelSprite
    {
        public float Strength;

        public HaloView()
        {
            Size = new Vector2(56, 56);
            generateVisualContent += Paint;
        }

        private void Paint(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0 || Strength <= 0.01f) return;
            Draw.Glow(ctx.painter2D, r.center, r.width * 0.22f, new Color(1f, 0.92f, 0.55f), Strength);
        }
    }

    /// <summary>
    /// Ekqo at Stage 1 (Spark) as a pixel sprite: dim until the child says hello, then glowing.
    /// Glow runs 0 (dim) to 1 (bright). Place Halo in the world before Ekqo so it sits behind.
    /// </summary>
    internal sealed class EkqoView : PixelSprite
    {
        public float Glow = 0.15f;       // eased toward TargetGlow
        public float TargetGlow = 0.15f;
        public bool Sleepy;
        public float Talk;               // squash and stretch while a line plays
        public float Happy;              // hop after a right answer
        public readonly HaloView Halo = new HaloView();

        public EkqoView() : base("ekqo_dim")
        {
            Size = new Vector2(24, 24);
        }

        public void Tick(float time, float dt)
        {
            Glow = Mathf.MoveTowards(Glow, TargetGlow, dt * 0.6f);
            Happy = Mathf.MoveTowards(Happy, 0, dt * 0.8f);
            SetSprite(Sleepy ? "ekqo_sleepy" : Glow < 0.3f ? "ekqo_dim" : "ekqo");
            var bob = Mathf.Sin(time * (Sleepy ? 1.2f : 2.4f)) * (Sleepy ? 0.6f : 1.2f) - Mathf.Abs(Mathf.Sin(time * 10)) * 6 * Happy;
            Nudge = new Vector2(0, Mathf.Round(bob));
            var s = Talk > 0 ? Mathf.Abs(Mathf.Sin(time * 14)) * Talk : 0;
            style.scale = new Scale(new Vector3(1 + 0.08f * s, 1 - 0.08f * s, 1));
            Halo.Pos = Pos + (Size - Halo.Size) * 0.5f;
            Halo.Nudge = Nudge;
            Halo.Strength = 0.35f + Glow * 1.3f;
            Halo.MarkDirtyRepaint();
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
            var sprite = PixelArt.PictureFor(concept);
            if (sprite != null)
            {
                // Pixel art from PixelArt.txt (pic_<word>); the drawing below is the fallback.
                PixelArt.Show(this, sprite);
                AddToClassList("pixel-fit");
            }
            else generateVisualContent += Paint;
        }

        public static bool Has(string concept) => concept != null && (Known.Contains(concept) || PixelArt.PictureFor(concept) != null);

        private static readonly HashSet<string> Known = new HashSet<string>
        {
            "greet.hello", "greet.bye", "ans.yes", "ans.no", "word.name", "sky.star", "nature.light",
            "time.night", "place.garden", "q.who", "phrase.dont_be_scared",
            "phrase.come_to_my_house", "phrase.sleep_there",
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
                    Draw.Circle(p, c, s * 0.95f, Night.Sky);
                    Draw.Moon(p, c + new Vector2(-s * 0.1f, 0), s * 0.45f, new Color(1f, 0.95f, 0.7f), Night.Sky);
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
                case "phrase.come_to_my_house":
                    // A house with a lit door and a little path.
                    p.fillColor = new Color(0.85f, 0.45f, 0.4f);
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(-s * 0.95f, -s * 0.1f));
                    p.LineTo(c + new Vector2(0, -s * 0.9f));
                    p.LineTo(c + new Vector2(s * 0.95f, -s * 0.1f));
                    p.ClosePath();
                    p.Fill();
                    Draw.Rect(p, new Rect(c.x - s * 0.7f, c.y - s * 0.12f, s * 1.4f, s * 0.9f), new Color(0.98f, 0.88f, 0.7f));
                    Draw.RoundRect(p, new Rect(c.x - s * 0.18f, c.y + s * 0.25f, s * 0.36f, s * 0.53f), s * 0.08f, new Color(1f, 0.82f, 0.4f));
                    Draw.Rect(p, new Rect(c.x + s * 0.3f, c.y + s * 0.05f, s * 0.28f, s * 0.24f), new Color(0.55f, 0.75f, 0.95f));
                    break;
                case "phrase.sleep_there":
                    // A little bed under the moon, with "z z".
                    Draw.RoundRect(p, new Rect(c.x - s * 0.9f, c.y + s * 0.05f, s * 1.8f, s * 0.45f), s * 0.12f, new Color(0.55f, 0.65f, 0.95f));
                    Draw.RoundRect(p, new Rect(c.x - s * 0.85f, c.y - s * 0.12f, s * 0.5f, s * 0.3f), s * 0.12f, Color.white);
                    Draw.Rect(p, new Rect(c.x - s * 0.95f, c.y - s * 0.3f, s * 0.12f, s * 1.0f), new Color(0.6f, 0.42f, 0.3f));
                    Draw.Rect(p, new Rect(c.x + s * 0.83f, c.y, s * 0.12f, s * 0.7f), new Color(0.6f, 0.42f, 0.3f));
                    Draw.Moon(p, c + new Vector2(s * 0.45f, -s * 0.6f), s * 0.25f, new Color(1f, 0.92f, 0.6f), new Color(0.98f, 0.95f, 0.88f));
                    Draw.Stroke(p, ink, 4, c + new Vector2(-s * 0.1f, -s * 0.6f), c + new Vector2(s * 0.08f, -s * 0.6f), c + new Vector2(-s * 0.1f, -s * 0.42f), c + new Vector2(s * 0.08f, -s * 0.42f));
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
            var sprite = kind == Kind.WordBook ? "book" : kind == Kind.Jar ? "jar" : kind == Kind.Page ? "page" : null;
            if (sprite != null && PixelArt.Has(sprite))
            {
                PixelArt.Show(this, sprite);
                AddToClassList("pixel-fit");
            }
            else generateVisualContent += Paint;
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

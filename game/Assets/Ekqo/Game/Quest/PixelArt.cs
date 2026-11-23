using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ekqo.Game
{
    /// <summary>
    /// Ekqo's pixel art, read from Resources/PixelArt.txt (a palette and sprites drawn with
    /// letters, made by tools/pixelart/generate.py) and Resources/Garden.txt (a tile map of 32px tiles). Sprites become point-filtered
    /// textures, so they stay crisp at any size. Preview: python3 tools/pixelart/preview.py
    /// </summary>
    internal static class PixelArt
    {
        public const int Tile = 32;
        /// <summary>Night colours: everything is multiplied by this except the light in windows and lanterns.</summary>
        public static readonly Color NightTint = new Color(0.50f, 0.56f, 0.86f);
        private static readonly HashSet<string> LitTiles = new HashSet<string> { "window", "lantern" };
        private const string LightColours = "yY";

        private static Dictionary<char, Color32> _palette;
        private static Dictionary<string, string[]> _sprites;
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        public static bool Has(string name)
        {
            Load();
            return name != null && _sprites.ContainsKey(name);
        }

        /// <summary>The sprite as a texture (its own size, usually 32x32), or null when there is no such sprite.</summary>
        public static Texture2D Sprite(string name)
        {
            if (!Has(name)) return null;
            if (Textures.TryGetValue(name, out var tex) && tex != null) return tex;
            var rows = _sprites[name];
            var w = 1;
            foreach (var r in rows) w = Mathf.Max(w, r.Length);
            var h = Mathf.Max(1, rows.Length);
            var pixels = new Color32[w * h];
            Paint(pixels, w, h, 0, 0, rows, null, false);
            tex = MakeTexture(w, h, pixels);
            Textures[name] = tex;
            return tex;
        }

        /// <summary>The word picture for a concept: greet.hello → pic_hello.</summary>
        public static string PictureFor(string concept)
        {
            if (string.IsNullOrEmpty(concept)) return null;
            var dot = concept.IndexOf('.');
            var name = "pic_" + (dot >= 0 ? concept.Substring(dot + 1) : concept);
            return Has(name) ? name : null;
        }

        /// <summary>Puts a sprite on an element as its background, filling the element.</summary>
        public static void Show(VisualElement element, string sprite)
        {
            var tex = Sprite(sprite);
            element.style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
        }

        internal static Texture2D MakeTexture(int width, int height, Color32[] pixels)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>
        /// Draws sprite rows into a pixel buffer whose row 0 is the bottom (Unity's order).
        /// x, y are the sprite's top-left corner counted from the top-left of the image.
        /// </summary>
        internal static void Paint(Color32[] pixels, int width, int height, int x, int y, string[] rows, Color? tint, bool keepLight)
        {
            for (var row = 0; row < rows.Length; row++)
            {
                var line = rows[row];
                for (var col = 0; col < line.Length; col++)
                {
                    var ch = line[col];
                    if (ch == '.' || !_palette.TryGetValue(ch, out var c)) continue;
                    var px = x + col;
                    var py = height - 1 - (y + row);
                    if (px < 0 || px >= width || py < 0 || py >= height) continue;
                    if (tint.HasValue && !(keepLight && LightColours.IndexOf(ch) >= 0))
                    {
                        var t = tint.Value;
                        c = new Color32((byte)(c.r * t.r), (byte)(c.g * t.g), (byte)(c.b * t.b), c.a);
                    }
                    pixels[py * width + px] = c;
                }
            }
        }

        internal static string[] Rows(string name)
        {
            Load();
            return name != null && _sprites.TryGetValue(name, out var rows) ? rows : null;
        }

        internal static bool IsLit(string tile) => LitTiles.Contains(tile);

        private static void Load()
        {
            if (_sprites != null) return;
            _palette = new Dictionary<char, Color32>();
            _sprites = new Dictionary<string, string[]>();
            var asset = Resources.Load<TextAsset>("PixelArt");
            if (asset == null)
            {
                Debug.LogError("[PixelArt] Resources/PixelArt.txt is missing.");
                return;
            }
            string section = null, name = null;
            var rows = new List<string>();
            foreach (var raw in asset.text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line == "palette") { section = "palette"; continue; }
                if (line.StartsWith("sprite "))
                {
                    section = "sprite";
                    name = line.Substring(7).Trim();
                    rows.Clear();
                    continue;
                }
                if (line == "end")
                {
                    if (section == "sprite" && name != null)
                    {
                        _sprites[name] = rows.ToArray();
                    }
                    section = null;
                    continue;
                }
                if (section == "palette")
                {
                    var parts = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && parts[0].Length == 1 && ColorUtility.TryParseHtmlString("#" + parts[1], out var col))
                        _palette[parts[0][0]] = col;
                }
                else if (section == "sprite")
                {
                    rows.Add(line);
                }
            }
        }
    }

    /// <summary>A tile map from Resources (Garden.txt), drawn into one texture.</summary>
    internal sealed class PixelMap
    {
        public int Columns, Rows;
        public int PixelWidth => Columns * PixelArt.Tile;
        public int PixelHeight => Rows * PixelArt.Tile;
        public Texture2D Texture;
        private readonly Dictionary<string, Vector2Int> _marks = new Dictionary<string, Vector2Int>();

        /// <summary>A named place from an "@name column row" line, as the tile's top-left in map pixels.</summary>
        public Vector2 Mark(string name) =>
            _marks.TryGetValue(name, out var m) ? new Vector2(m.x * PixelArt.Tile, m.y * PixelArt.Tile) : Vector2.zero;

        public static PixelMap Load(string resource, bool night)
        {
            var map = new PixelMap();
            var asset = Resources.Load<TextAsset>(resource);
            if (asset == null)
            {
                Debug.LogError($"[PixelArt] Resources/{resource}.txt is missing.");
                map.Columns = map.Rows = 1;
                map.Texture = PixelArt.MakeTexture(1, 1, new[] { new Color32(30, 40, 60, 255) });
                return map;
            }
            var legend = new Dictionary<char, string>();
            var lines = new List<string>();
            foreach (var raw in asset.text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("legend "))
                {
                    foreach (var kv in line.Substring(7).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
                        if (kv.Length > 2 && kv[1] == '=') legend[kv[0]] = kv.Substring(2);
                }
                else if (line.StartsWith("@"))
                {
                    var p = line.Substring(1).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length == 3 && int.TryParse(p[1], out var cx) && int.TryParse(p[2], out var cy))
                        map._marks[p[0]] = new Vector2Int(cx, cy);
                }
                else lines.Add(line);
            }
            map.Rows = lines.Count;
            foreach (var l in lines) map.Columns = Mathf.Max(map.Columns, l.Length);
            var w = map.PixelWidth;
            var h = map.PixelHeight;
            var pixels = new Color32[w * h];
            Color? tint = night ? PixelArt.NightTint : (Color?)null;
            var grass = PixelArt.Rows("grass");
            for (var r = 0; r < lines.Count; r++)
            {
                for (var c = 0; c < map.Columns; c++)
                {
                    var x = c * PixelArt.Tile;
                    var y = r * PixelArt.Tile;
                    if (grass != null) PixelArt.Paint(pixels, w, h, x, y, grass, tint, false);
                    if (c >= lines[r].Length || !legend.TryGetValue(lines[r][c], out var tile)) continue;
                    var rows = PixelArt.Rows(tile);
                    if (rows == null || tile == "grass") continue;
                    PixelArt.Paint(pixels, w, h, x, y, rows, tint, PixelArt.IsLit(tile));
                }
            }
            map.Texture = PixelArt.MakeTexture(w, h, pixels);
            return map;
        }
    }

    /// <summary>
    /// The top-down world: the map as a background, with sprites placed in map pixels on top.
    /// The camera keeps Focus (in map pixels) near the middle of the screen, a little above
    /// centre so the dialogue box at the bottom never hides the action.
    /// </summary>
    internal sealed class PixelWorld : VisualElement
    {
        /// <summary>How many tiles tall the view is at most; fewer tiles means a closer camera.</summary>
        public float RowsInView = 9;
        public Vector2 Focus;
        public float Scale { get; private set; } = 1;
        public Vector2 Offset { get; private set; }
        public readonly PixelMap Map;
        private readonly VisualElement _ground;
        private readonly List<PixelSprite> _sprites = new List<PixelSprite>();

        public PixelWorld(PixelMap map)
        {
            Map = map;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            style.overflow = Overflow.Hidden;
            _ground = new VisualElement { pickingMode = PickingMode.Ignore };
            _ground.style.position = Position.Absolute;
            _ground.style.backgroundImage = new StyleBackground(map.Texture);
            Add(_ground);
            RegisterCallback<GeometryChangedEvent>(_ => Layout());
        }

        /// <summary>Adds a sprite element (or any element sized in map pixels) to the world.</summary>
        public T Place<T>(T sprite) where T : PixelSprite
        {
            _sprites.Add(sprite);
            Add(sprite);
            PlaceAt(sprite);
            return sprite;
        }

        /// <summary>Converts a point in map pixels to a point in this element.</summary>
        public Vector2 ToScreen(Vector2 mapPoint) => Offset + mapPoint * Scale;

        public void Layout()
        {
            var w = resolvedStyle.width;
            var h = resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0 || h <= 0) return;
            var mw = Map.PixelWidth;
            var mh = Map.PixelHeight;
            Scale = Mathf.Max(w / mw, h / mh, h / (RowsInView * PixelArt.Tile));
            var ox = Mathf.Clamp(w * 0.5f - Focus.x * Scale, w - mw * Scale, 0);
            var oy = Mathf.Clamp(h * 0.42f - Focus.y * Scale, h - mh * Scale, 0);
            Offset = new Vector2(Mathf.Round(ox), Mathf.Round(oy));
            _ground.style.left = Offset.x;
            _ground.style.top = Offset.y;
            _ground.style.width = mw * Scale;
            _ground.style.height = mh * Scale;
            foreach (var s in _sprites) PlaceAt(s);
        }

        public void PlaceAt(PixelSprite s)
        {
            var size = s.Size * Scale;
            var p = ToScreen(s.Pos + s.Nudge);
            s.style.left = p.x;
            s.style.top = p.y;
            s.style.width = size.x;
            s.style.height = size.y;
        }
    }

    /// <summary>Something in the world, placed by its top-left corner in map pixels.</summary>
    internal class PixelSprite : VisualElement
    {
        public Vector2 Pos;
        public Vector2 Size = new Vector2(PixelArt.Tile, PixelArt.Tile);
        /// <summary>A small extra offset for bobbing and shaking, in map pixels.</summary>
        public Vector2 Nudge;
        private string _shown;

        public PixelSprite(string sprite = null)
        {
            pickingMode = PickingMode.Ignore;
            style.position = UnityEngine.UIElements.Position.Absolute;
            if (sprite == null) return;
            SetSprite(sprite);
            var tex = PixelArt.Sprite(sprite);
            if (tex != null) Size = new Vector2(tex.width, tex.height);   // one art pixel = one map pixel
        }

        public void SetSprite(string sprite)
        {
            if (sprite == _shown) return;
            _shown = sprite;
            PixelArt.Show(this, sprite);
        }

        /// <summary>Faces left by mirroring the sprite.</summary>
        public void FaceLeft(bool left) => style.scale = new Scale(new Vector3(left ? -1 : 1, 1, 1));
    }
}

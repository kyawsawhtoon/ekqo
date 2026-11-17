using System.Collections;
using Ekqo.Content;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace Ekqo.Game
{
    /// <summary>
    /// Week-1 spike (architecture 9.4): renders every lexicon entry and pattern template
    /// with the Advanced Text Generator so a native speaker can check Burmese shaping on
    /// iPad Safari and Android Chrome. The Ekqo menu → "Build Burmese text test" makes the
    /// scene and the web build (see Editor/TextTestBuilder.cs).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TextSpike : MonoBehaviour
    {
        [SerializeField] private string edition = "my";
        [Tooltip("Noto Sans Myanmar. Set directly so the test does not depend on a generated font asset.")]
        [SerializeField] private Font font;
        [SerializeField] private StyleSheet styles;

        private IEnumerator Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (styles != null) root.styleSheets.Add(styles);
            if (font != null) root.style.unityFontDefinition = FontDefinition.FromFont(font);
            root.style.backgroundColor = new Color(1f, 0.98f, 0.92f);

            var title = new Label("Ekqo Burmese text test. Read every line. If one looks wrong, note its grey name.");
            title.AddToClassList("spike-title");
            root.Add(title);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            root.Add(scroll);

            var url = $"{Application.streamingAssetsPath}/content/{edition}/bundle.json";
            using var req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                scroll.Add(new Label($"Could not load {url}: {req.error}"));
                yield break;
            }

            var bundle = ContentLoader.Parse(req.downloadHandler.text);
            title.text += $"  (content {bundle.Version})";
            foreach (var e in bundle.Edition.Lexicon.Entries) scroll.Add(Row(e.Concept, e.Text));
            foreach (var p in bundle.Edition.Patterns.Patterns) scroll.Add(Row(p.Id, p.Template));
            foreach (var q in bundle.Edition.Quests)
                foreach (var kv in q.Lines)
                    if (!string.IsNullOrEmpty(kv.Value.Text ?? kv.Value.Prompt))
                        scroll.Add(Row($"{q.Quest}#{kv.Key}", kv.Value.Text ?? kv.Value.Prompt));
        }

        private static VisualElement Row(string id, string text)
        {
            var row = new VisualElement();
            row.AddToClassList("spike-row");
            var idLabel = new Label(id);
            idLabel.AddToClassList("spike-row__id");
            var textLabel = new Label(text);
            textLabel.AddToClassList("spike-row__text");
            row.Add(idLabel);
            row.Add(textLabel);
            return row;
        }
    }
}

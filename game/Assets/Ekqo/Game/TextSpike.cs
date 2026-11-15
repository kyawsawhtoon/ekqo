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
    /// iPad Safari and Android Chrome. Put on a GameObject with a UIDocument using Ekqo.uss.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TextSpike : MonoBehaviour
    {
        [SerializeField] private string edition = "my";

        private IEnumerator Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            var scroll = new ScrollView();
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

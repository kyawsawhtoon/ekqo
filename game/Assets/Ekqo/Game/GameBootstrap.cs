using System.Collections;
using Ekqo.Content;
using Ekqo.Core.Engine;
using UnityEngine;
using UnityEngine.Networking;

namespace Ekqo.Game
{
    /// <summary>
    /// Loads the content bundle, creates the quest engine and starts the next quest.
    /// Presentation (scenes, dialogue, mic, tap options) subscribes to Engine.Events.
    /// TODO(WS1): scenes and UI presenters; TODO(WS5): load and sync the save.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string edition = "my";

        public QuestEngine Engine { get; private set; }

        private IEnumerator Start()
        {
            var url = $"{Application.streamingAssetsPath}/content/{edition}/bundle.json";
            using var req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Content bundle failed to load from {url}: {req.error}");
                yield break;
            }

            Engine = new QuestEngine(ContentLoader.Parse(req.downloadHandler.text));
            Engine.Events += e => Debug.Log($"[engine] {e.GetType().Name} {e.QuestId}#{e.StepId}");
            var next = Engine.NextQuestId();
            if (next != null) Engine.StartQuest(next);
        }

        private void Update()
        {
            // Needs rise with active play time only. Time.deltaTime is capped per frame
            // (Maximum Allowed Timestep), so a hidden tab or a sleeping tablet adds nothing.
            Engine?.Tick(Time.deltaTime);
        }
    }
}

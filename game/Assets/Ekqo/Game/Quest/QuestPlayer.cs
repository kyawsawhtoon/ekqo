using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Ekqo.Content;
using Ekqo.Core.Content;
using Ekqo.Core.Engine;
using Ekqo.Speech;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace Ekqo.Game
{
    /// <summary>
    /// Plays one quest from the content bundle with tap answers (architecture 3, build order:
    /// "Q1 as the vertical slice played with tap answers"). The quest engine decides every step;
    /// this class only shows them: lines, listen-first teaching, say steps, review and rewards.
    /// Speech recognition plugs into the say steps later (architecture 4.2).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class QuestPlayer : MonoBehaviour
    {
        [SerializeField] private string edition = "my";
        [SerializeField] private string questId = "q01";
        [Tooltip("The edition font (Noto Sans Myanmar). Set by Ekqo → Build Quest 1.")]
        [SerializeField] private Font font;
        [Tooltip("Used in the editor, where there is no start page to type a name.")]
        [SerializeField] private string editorPlayerName = "Su";

        private const string SaveKey = "ekqo.save.v1";
        private const string HintsKey = "ekqo.hints";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern string EkqoPage_GetPlayerName();
#endif

        private readonly Queue<EngineEvent> _events = new Queue<EngineEvent>();
        private readonly System.Random _rng = new System.Random();
        private Bundle _bundle;
        private Quest _quest;
        private QuestEngine _engine;
        private LineText _text;
        private VoiceService _voice;
        private string _locale;
        private string _cacheBust;
        private bool _hints = true;
        private bool _restartFresh;
        private float _time;

        // Screen
        private VisualElement _root, _ui, _bubble, _hearButton, _nextButton;
        private PixelWorld _world;
        private PixelSprite _child;
        private BushView _bush;
        private EkqoView _ekqo;
        private FallingLight _fall;
        private MixedText _bubbleSpeaker, _bubbleText;
        private Label _bubbleSupport, _hintToggle, _stamp;
        private bool _nextPressed;
        private Func<IEnumerator> _hearAgain;

        // Places in Grandma's garden (map pixels, from Garden.txt).
        private Vector2 _ekqoHidden, _ekqoOut, _childStart, _childCorner, _childStop, _focus;
        private const float WalkSpeed = 42;   // map pixels a second

        // ---------- start ----------

        private void Awake()
        {
            var doc = GetComponent<UIDocument>();
            if (doc.panelSettings == null) doc.panelSettings = Resources.Load<PanelSettings>("TextTestPanel");
            _voice = gameObject.AddComponent<VoiceService>();
            if (font == null) Debug.LogError("[Quest] No font set: Burmese will not show. Use Ekqo → Build Quest 1, which sets it.");
        }

        private IEnumerator Start()
        {
            BuildScreen();
            try { _hints = PlayerPrefs.GetInt(HintsKey, 1) == 1; } catch (Exception) { _hints = true; }
            ApplyHints();

            _cacheBust = Application.buildGUID;
            var stamp = "editor";
            using (var req = UnityWebRequest.Get($"{Application.streamingAssetsPath}/build.txt?v={_cacheBust}"))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) stamp = req.downloadHandler.text.Trim();
            }

            var url = $"{Application.streamingAssetsPath}/content/{edition}/bundle.json?v={_cacheBust}";
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    ShowError($"Could not load the game content ({req.error}). Check the internet connection and reload the page.");
                    yield break;
                }
                string parseError = null;
                try { _bundle = ContentLoader.Parse(req.downloadHandler.text); }
                catch (Exception ex) { Debug.LogException(ex); parseError = ex.Message; }
                if (parseError != null)
                {
                    ShowError("The game content could not be read. Send Claude a photo of this screen.\n" + parseError);
                    yield break;
                }
            }

            _quest = _bundle.Core.Quests.FirstOrDefault(q => q.Id == questId);
            if (_quest == null) { ShowError($"Quest {questId} is not in the content."); yield break; }
            _locale = _bundle.Edition.Edition?.Locale ?? "my-MM";
            _text = new LineText(_bundle) { PlayerName = PlayerName() };
            yield return _voice.Init(edition, _locale, _cacheBust);
            _stamp.text = $"{stamp} · content {_bundle.Version} · voice {_voice.Mode}";

            while (true)
            {
                var fresh = _restartFresh;
                _restartFresh = false;
                yield return PlayQuest(fresh);
            }
        }

        private string PlayerName()
        {
            var name = "";
#if UNITY_WEBGL && !UNITY_EDITOR
            try { name = EkqoPage_GetPlayerName(); } catch (Exception) { name = ""; }
#endif
            name = (name ?? "").Trim();
            if (name.Length > 20) name = name.Substring(0, 20);
            return name.Length > 0 ? name : editorPlayerName;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            _time += dt;
            _bush?.Tick(_time);
            if (_ekqo != null)
            {
                _ekqo.Talk = _voice != null && _voice.Speaking && _talking ? 1 : 0;
                _ekqo.Tick(_time, dt);
            }
            _world?.Layout();
            _fall?.MarkDirtyRepaint();
            // Needs rise with active play only (Q1 has none unlocked yet).
            _engine?.Tick(dt);
        }

        // ---------- one quest ----------

        private IEnumerator PlayQuest(bool fresh)
        {
            ResetStage();
            SaveSnapshot save = null;
            if (!fresh)
            {
                try { save = SaveStore.FromJson(PlayerPrefs.GetString(SaveKey, "")); } catch (Exception) { save = null; }
            }
            if (save != null) { save.CurrentQuest = null; save.StepIndex = 0; }   // quests restart from the top
            var playedBefore = save != null && save.CompletedQuests.Contains(questId);

            _engine = new QuestEngine(_bundle, save);
            _events.Clear();
            _engine.Events += _events.Enqueue;

            yield return TitleCard(playedBefore);
            _engine.StartQuest(questId);

            while (true)
            {
                while (_events.Count == 0) yield return null;
                var e = _events.Dequeue();
                Debug.Log($"[Quest] {e.GetType().Name} {e.StepId}");
                if (e is QuestCompleteEvent done) { yield return Complete(done); yield break; }
                yield return Handle(e);
            }
        }

        private IEnumerator Handle(EngineEvent e)
        {
            switch (e)
            {
                case PresentEvent p: yield return Present(p.Step); break;
                case TeachEvent t: yield return Teach(t); break;
                case AskEvent a: yield return Ask(a); break;
                case FeedbackEvent f: yield return Feedback(f); break;
                case ReviewEvent r: yield return Review(r); break;
                case CareEvent c: _engine.CareDone(c.Action); break;   // care starts in Q2
            }
        }

        private IEnumerator TitleCard(bool playedBefore)
        {
            ClearUI();
            var box = Layer();
            box.AddToClassList("veil");
            box.style.justifyContent = Justify.Center;
            box.style.alignItems = Align.Center;
            var chapter = _bundle.Core.World.Chapters.FirstOrDefault(c => c.Quests.Contains(questId));
            box.Add(Text(chapter != null ? $"Chapter {chapter.Number} · Quest {chapter.Quests.IndexOf(questId) + 1}" : "Quest", "subtitle"));
            box.Add(Text(_quest.Title, "title"));
            if (playedBefore) box.Add(Text($"Welcome back, {_text.PlayerName}! Ekqo remembers you.", "subtitle"));
            else box.Add(Text($"Hi {_text.PlayerName}! Tap the arrow to start.", "subtitle"));
            yield return WaitNext();
        }

        // ---------- present steps ----------

        private IEnumerator Present(Step step)
        {
            switch (step.Type)
            {
                case "scene":
                    yield return Scene(step);
                    break;
                case "line":
                    ClearUI();
                    if (Contains(step.Meaning, "sleep")) _ekqo.Sleepy = true;
                    var line = _text.ForStep(_quest.Id, step);
                    yield return SayLine(step.Speaker, line.Text, line.Support, $"{_quest.Id}/{step.Id}");
                    yield return WaitNext();
                    break;
                case "discover":
                    var d = _bundle.Core.Discoveries?.Discoveries.FirstOrDefault(x => x.Id == step.Discovery);
                    yield return PanelCard(IconView.Kind.Page, "About Ekqo", d?.Meaning ?? step.Discovery, "A new page in the About Ekqo book.");
                    break;
                case "reward":
                    foreach (var a in step.Abilities)
                    {
                        var ab = _bundle.Core.World.Ekqo?.Abilities.FirstOrDefault(x => x.Id == a);
                        var isBook = a == "word_book";
                        yield return PanelCard(isBook ? IconView.Kind.WordBook : IconView.Kind.Page,
                            isBook ? "You got the word book!" : "New power!", ab?.Meaning ?? a,
                            isBook ? "Every word you learn with Ekqo goes in here." : null);
                    }
                    foreach (var i in step.Items)
                    {
                        var item = _bundle.Core.World.Items.FirstOrDefault(x => x.Id == i);
                        yield return PanelCard(i == "glow_jar" ? IconView.Kind.Jar : IconView.Kind.Page, "A present for Ekqo!", item?.Meaning ?? i, null);
                    }
                    break;
            }
            _engine.Continue();
        }

        private IEnumerator Scene(Step step)
        {
            ClearUI();
            if (_child.Pos != _childStop) yield return WalkToBush();
            var caption = Text(LineText.CleanMeaning(step.Meaning), "caption");
            _ui.Add(caption);
            yield return new WaitForSeconds(0.8f);
            yield return null;
            var rb = _root.worldBound;
            var target = _bush.worldBound.center;
            _fall.From = new Vector2(0.12f, -0.02f);
            _fall.To = new Vector2((target.x - rb.x) / rb.width, (target.y - rb.y) / rb.height);
            for (var t = 0f; t < 1.3f; t += Time.deltaTime * 0.55f)
            {
                _fall.Progress = t;
                if (t > 1 && _bush.Shake < 0.5f) { _bush.Shake = 1; _ekqo.TargetGlow = 0.25f; }
                yield return null;
            }
            _fall.Progress = -1;
            yield return English(caption.text);
            yield return WaitNext();
        }

        /// <summary>The child walks out of Grandma's door, down the path to the bush, and the camera follows.</summary>
        private IEnumerator WalkToBush()
        {
            _child.Pos = _childStart;
            yield return Walk(_childCorner, "child_down", "child_down_step", false);
            yield return Walk(_childStop, "child_side", "child_side_step", false);
            _child.SetSprite("child_side");
            var from = _world.Focus;
            for (var t = 0f; t < 1; t += Time.deltaTime * 1.5f)
            {
                _world.Focus = Vector2.Lerp(from, _focus, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            _world.Focus = _focus;
        }

        private IEnumerator Walk(Vector2 to, string still, string step, bool left)
        {
            _child.FaceLeft(left);
            var steps = 0f;
            while (_child.Pos != to)
            {
                _child.Pos = Vector2.MoveTowards(_child.Pos, to, WalkSpeed * Time.deltaTime);
                steps += Time.deltaTime;
                _child.SetSprite(Mathf.FloorToInt(steps / 0.18f) % 2 == 0 ? still : step);
                _world.Focus = _child.Pos + new Vector2(8, 8);
                yield return null;
            }
            _child.SetSprite(still);
        }

        // ---------- teaching: hear, show, echo ----------

        private IEnumerator Teach(TeachEvent e)
        {
            var outcomes = e.Concepts.ToDictionary(c => c, c => new WordOutcome { Concept = c, Heard = true });

            // 1. Hear: each word with its picture.
            foreach (var c in e.Concepts)
            {
                ClearUI();
                var word = _text.Word(c);
                _ui.Add(WordCard(c, word, _text.Gloss(c)));
                SetInstruction("Listen to Ekqo", null);
                yield return Speak("ekqo", $"word/{c}", word);
                yield return English(_text.Gloss(c));
                _hearAgain = () => Speak("ekqo", $"word/{c}", word);
                yield return WaitNext();
            }

            // 2. Show: listen and tap the right picture.
            var show = e.Concepts.Where(c => !e.SkipShowEcho.Contains(c) && PictureView.Has(c)).ToList();
            foreach (var c in show)
            {
                var choices = e.Concepts.Where(PictureView.Has).ToList();
                if (choices.Count < 2) choices.AddRange(Distractors(c, 1));
                yield return PickPicture(c, Shuffle(choices), "Which picture is it? Listen and tap.", _ => { });
                outcomes[c].Tapped = true;
            }

            // 3. Echo: say it with Ekqo (optional, never checked).
            foreach (var c in e.Concepts.Where(c => !e.SkipShowEcho.Contains(c)))
            {
                ClearUI();
                var word = _text.Word(c);
                _ui.Add(WordCard(c, word, _text.Gloss(c)));
                SetInstruction("Say it with Ekqo!", "Saying it out loud is the goal. Tap the arrow when you're ready.");
                yield return Speak("ekqo", $"word/{c}", word);
                _hearAgain = () => Speak("ekqo", $"word/{c}", word);
                yield return WaitNext();
            }

            // Sentence patterns: hear the whole sentence, then say it along.
            foreach (var p in e.Patterns)
            {
                ClearUI();
                var sentence = _text.Pattern(p);
                var meaning = _text.PatternMeaning(p).Replace("___", _text.PlayerName);
                _ui.Add(WordCard(PatternPicture(p), sentence, meaning));
                SetInstruction("Say it with Ekqo!", "Use your own name.");
                yield return Speak("ekqo", null, sentence);
                _hearAgain = () => Speak("ekqo", null, sentence);
                yield return WaitNext();
            }

            ClearUI();
            _engine.TeachDone(outcomes.Values);
        }

        private string PatternPicture(string pattern) =>
            _bundle.Core.Concepts.Patterns.FirstOrDefault(x => x.Id == pattern)?.Slots.Values.Contains("player_name") == true ? "word.name" : null;

        /// <summary>Plays a word and waits for the child to tap its picture. A wrong tap replays the word and lights up the right one.</summary>
        private IEnumerator PickPicture(string target, List<string> choices, string instruction, Action<bool> done)
        {
            ClearUI();
            var word = _text.Word(target);
            ShowBubble("ekqo", word, $"Tap the picture: {_text.Gloss(target)}");
            SetInstruction(instruction, null);
            var sides = ChoiceColumns();
            var cards = new Dictionary<string, VisualElement>();
            string picked = null;
            foreach (var c in choices.Distinct())
            {
                var card = new VisualElement();
                card.AddToClassList("card");
                var pic = new PictureView(c);
                pic.AddToClassList("card__picture");
                card.Add(pic);
                var concept = c;
                card.RegisterCallback<ClickEvent>(_ => picked = concept);
                sides[cards.Count % 2].Add(card);
                cards[c] = card;
            }
            yield return Speak("ekqo", $"word/{target}", word);
            _hearAgain = () => Speak("ekqo", $"word/{target}", word);
            ShowHear(true);

            var firstTry = true;
            while (true)
            {
                picked = null;
                while (picked == null) yield return null;
                var choice = picked;
                if (choice == target) break;
                firstTry = false;
                cards[choice].AddToClassList("card--wrong");
                cards[target].AddToClassList("card--glow");   // hint glow (Q1 ability)
                yield return Speak("ekqo", $"word/{target}", word);
            }
            cards[target].RemoveFromClassList("card--glow");
            cards[target].AddToClassList("card--right");
            _ekqo.Happy = 1;
            Burst();
            yield return new WaitForSeconds(0.9f);
            ShowHear(false);
            done(firstTry);
        }

        private IEnumerable<string> Distractors(string notThis, int count) =>
            _quest.Topics.Where(c => c != notThis && PictureView.Has(c) && !_text.EditionQuest(_quest.Id).Say.Contains(c))
                .OrderBy(_ => _rng.Next()).Take(count);

        // ---------- say steps (tap answers) ----------

        private string _lastAnswer;
        private string _lastAnswerConcept;

        private IEnumerator Ask(AskEvent e)
        {
            var step = e.Step;
            ClearUI();
            if (Contains(step.Meaning, "yawn") || Contains(step.Meaning, "sleep")) _ekqo.Sleepy = true;
            var prompt = _text.ForStep(_quest.Id, step);
            if (prompt.Text.Length > 0)
            {
                yield return SayLine(step.Speaker, prompt.Text, prompt.Support, $"{_quest.Id}/{step.Id}");
            }
            SetInstruction("Your turn! Say it out loud, then tap it.", LineText.CleanMeaning(step.Meaning));
            yield return English("Your turn! Say it out loud, then tap it.");

            var options = Shuffle(_text.Options(_quest.Id, step));
            var sides = ChoiceColumns();
            var cards = new Dictionary<TapOption, VisualElement>();
            TapOption picked = null, answer = null;
            foreach (var o in options)
            {
                var card = new VisualElement();
                card.AddToClassList("card");
                card.AddToClassList("card--phrase");
                if (o.Concept != null && PictureView.Has(o.Concept))
                {
                    var pic = new PictureView(o.Concept);
                    pic.AddToClassList("card__picture");
                    card.Add(pic);
                }
                var text = new MixedText(font, "card__text");
                text.SetText(o.Text);
                card.Add(text);
                var option = o;
                card.RegisterCallback<ClickEvent>(_ => picked = option);
                sides[cards.Count % 2].Add(card);
                cards[o] = card;
            }

            while (true)
            {
                picked = null;
                while (picked == null) yield return null;
                answer = picked;
                // A right tap goes straight on: Ekqo says it back in the feedback (tap fallback, 4.2).
                if (answer.Correct) break;
                cards[answer].AddToClassList("card--wrong");
                var right = options.First(o => o.Correct);
                cards[right].AddToClassList("card--glow");
                ShowBubble(step.Speaker, right.Text, "Let's say it together!");
                yield return Speak(step.Speaker, right.Concept != null ? $"word/{right.Concept}" : null, right.Text);
            }

            cards[answer].AddToClassList("card--right");
            _lastAnswer = answer.Text;
            _lastAnswerConcept = answer.Concept;
            var matched = answer.Concept != null ? new List<string> { answer.Concept } : new List<string>();
            _engine.Answer(new AnswerResult { Verdict = Verdict.Correct, Mode = AnswerMode.Tap, Matched = matched });
        }

        private IEnumerator Feedback(FeedbackEvent e)
        {
            var step = _quest.Steps.FirstOrDefault(s => s.Id == e.StepId);
            ClearUI();
            if (e.Verdict == Verdict.Correct)
            {
                _ekqo.Happy = 1;
                Burst();
                if (step?.OnSuccess?.Effect == "ekqo_glow_up") yield return Emerge();
            }
            if (e.Recast && !string.IsNullOrEmpty(_lastAnswer))
            {
                // Ekqo says it back, so the child hears it once more ("Let's say it together").
                yield return SayLine("ekqo", _lastAnswer, "Yes! Let's say it together.", _lastAnswerConcept != null ? $"word/{_lastAnswerConcept}" : null);
            }
            else
            {
                ShowBubble("ekqo", "", "Yes!");
            }
            yield return WaitNext();
        }

        private IEnumerator Emerge()
        {
            _ekqo.TargetGlow = 0.8f;
            _bush.Shake = 1;
            var from = _ekqo.Pos;
            for (var t = 0f; t < 1; t += Time.deltaTime * 1.2f)
            {
                _ekqo.Pos = Vector2.Lerp(from, _ekqoOut, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            _ekqo.Pos = _ekqoOut;
        }

        // ---------- review ----------

        private IEnumerator Review(ReviewEvent e)
        {
            var words = e.Concepts.Where(c => _text.Word(c).Length > 0 && PictureView.Has(c)).ToList();
            yield return PanelCard(IconView.Kind.WordBook, "Ekqo is sleepy...", "He is forgetting words! Help him remember.", "Listen, then tap the right picture.");
            var outcomes = new List<WordOutcome>();
            foreach (var c in words)
            {
                var others = words.Where(x => x != c).OrderBy(_ => _rng.Next()).Take(2).ToList();
                if (others.Count < 2) others.AddRange(Distractors(c, 2 - others.Count));
                others.Add(c);
                yield return PickPicture(c, Shuffle(others), "Which picture is it? Listen and tap.", _ => { });
                outcomes.Add(new WordOutcome { Concept = c, Heard = true, Tapped = true });
            }
            ClearUI();
            _engine.ReviewDone(outcomes);
        }

        // ---------- end ----------

        private IEnumerator Complete(QuestCompleteEvent e)
        {
            try
            {
                PlayerPrefs.SetString(SaveKey, SaveStore.ToJson(_engine.State));
                PlayerPrefs.Save();
            }
            catch (Exception ex) { Debug.LogWarning($"[Quest] Could not save: {ex.Message}"); }

            ClearUI();
            _ekqo.Sleepy = true;
            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.style.top = 8f;
            panel.style.left = Length.Percent(14);
            panel.style.right = Length.Percent(14);
            var jar = new IconView(IconView.Kind.Jar);
            jar.AddToClassList("panel__icon");
            panel.Add(jar);
            panel.Add(Text("Quest complete!", "panel__title"));
            panel.Add(Text("Ekqo is fast asleep in his glowing jar. Good night!", "panel__text"));
            panel.Add(Text("Words for your word book", "panel__text"));
            var chips = new VisualElement();
            chips.AddToClassList("panel__words");
            foreach (var c in _text.EditionQuest(_quest.Id)?.Say ?? new List<string>())
            {
                var chip = new VisualElement();
                chip.AddToClassList("chip");
                var w = new MixedText(font, "chip__text");
                w.SetText(_text.Word(c));
                chip.Add(w);
                chip.Add(Text(_text.Gloss(c), "chip__gloss", "hint"));
                var concept = c;
                chip.RegisterCallback<ClickEvent>(_ => StartCoroutine(Speak("ekqo", $"word/{concept}", _text.Word(concept))));
                chips.Add(chip);
            }
            panel.Add(chips);
            var choice = 0;
            var again = Text("Play again", "button");
            again.RegisterCallback<ClickEvent>(_ => choice = 1);
            panel.Add(again);
            var reset = Text("Start over as a new player", "button", "button--quiet");
            reset.RegisterCallback<ClickEvent>(_ => choice = 2);
            panel.Add(reset);
            _ui.Add(panel);

            while (choice == 0) yield return null;
            if (choice == 2)
            {
                try { PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save(); } catch (Exception) { }
                _restartFresh = true;
            }
        }

        // ---------- screen ----------

        private void BuildScreen()
        {
            var doc = GetComponent<UIDocument>();
            _root = doc.rootVisualElement;
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.top = 0; _root.style.right = 0; _root.style.bottom = 0;
            _root.style.backgroundColor = Night.Sky;
            var styles = Resources.Load<StyleSheet>("QuestStyles");
            if (styles != null) _root.styleSheets.Add(styles);
            else Debug.LogError("[Quest] Resources/QuestStyles.uss is missing.");

            // Grandma's garden at night, top-down (Resources/Garden.txt and PixelArt.txt).
            var map = PixelMap.Load("Garden", true);
            _world = new PixelWorld(map);
            _root.Add(_world);
            var bushAt = map.Mark("ekqo_bush");
            _ekqoHidden = bushAt + new Vector2(4, 4);     // only his tuft shows over the bush
            _ekqoOut = bushAt + new Vector2(4, -16);      // floating above the bush
            _childStart = map.Mark("door") + new Vector2(0, 16);
            _childStop = map.Mark("player_stop");
            _childCorner = new Vector2(_childStart.x, _childStop.y);
            _focus = map.Mark("focus") + new Vector2(8, 8);

            _child = new PixelSprite("child_side");
            _child.style.unityBackgroundImageTintColor = Night.Character;
            _world.Place(_child);
            _ekqo = new EkqoView();
            _world.Place(_ekqo.Halo);
            _world.Place(_ekqo);
            _bush = new BushView(bushAt);
            _world.Place(_bush);

            _fall = new FallingLight();
            _root.Add(_fall);

            _ui = Layer();
            _root.Add(_ui);

            _bubble = new VisualElement();
            _bubble.AddToClassList("bubble");
            _bubbleSpeaker = new MixedText(font, "bubble__speaker");
            _bubbleSpeaker.AddToClassList("bubble__name");
            _bubble.Add(_bubbleSpeaker);
            _bubbleText = new MixedText(font, "bubble__text");
            _bubbleText.AddToClassList("bubble__line");
            _bubble.Add(_bubbleText);
            _bubbleSupport = Text("", "bubble__support", "hint");
            _bubble.Add(_bubbleSupport);
            _root.Add(_bubble);

            _hearButton = RoundButton(IconView.Kind.Speaker, "round--hear");
            _hearButton.RegisterCallback<ClickEvent>(_ => { if (_hearAgain != null) StartCoroutine(_hearAgain()); });
            _root.Add(_hearButton);
            _nextButton = RoundButton(IconView.Kind.Next, "round--next");
            _nextButton.RegisterCallback<ClickEvent>(_ => _nextPressed = true);
            _root.Add(_nextButton);

            _hintToggle = Text("", "hint-toggle");
            _hintToggle.RegisterCallback<ClickEvent>(_ =>
            {
                _hints = !_hints;
                try { PlayerPrefs.SetInt(HintsKey, _hints ? 1 : 0); PlayerPrefs.Save(); } catch (Exception) { }
                ApplyHints();
            });
            _root.Add(_hintToggle);
            _stamp = Text("loading...", "stamp");
            _stamp.pickingMode = PickingMode.Ignore;
            _root.Add(_stamp);
            ClearUI();
        }

        private void ResetStage()
        {
            _ekqo.Sleepy = false;
            _ekqo.Glow = _ekqo.TargetGlow = 0.12f;
            _ekqo.Pos = _ekqoHidden;
            // The child starts at Grandma's door and walks out in the opening scene.
            _child.Pos = _childStart;
            _child.FaceLeft(false);
            _child.SetSprite("child_down");
            _world.Focus = _childStart + new Vector2(8, 8);
            _fall.Progress = -1;
        }

        private void ApplyHints()
        {
            _root.EnableInClassList("no-hints", !_hints);
            _hintToggle.text = _hints ? "English help: on" : "English help: off";
        }

        private void ClearUI()
        {
            _ui.Clear();
            _bubble.AddToClassList("hidden");
            ShowHear(false);
            _nextButton.AddToClassList("hidden");
            _hearAgain = null;
        }

        private VisualElement Layer()
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.AddToClassList("layer");
            if (_ui != null) _ui.Add(layer);
            return layer;
        }

        private static Label Text(string text, params string[] classes)
        {
            var label = new Label(text);
            foreach (var c in classes) label.AddToClassList(c);
            return label;
        }

        private static VisualElement RoundButton(IconView.Kind kind, string modifier)
        {
            var b = new VisualElement();
            b.AddToClassList("round");
            b.AddToClassList(modifier);
            var icon = new IconView(kind);
            icon.AddToClassList("round__icon");
            b.Add(icon);
            return b;
        }

        private void ShowBubble(string speaker, string text, string support)
        {
            _bubble.RemoveFromClassList("hidden");
            var name = speaker != null ? _text.Name(speaker) : "";
            _bubbleSpeaker.SetText(name);
            _bubbleSpeaker.EnableInClassList("hidden", string.IsNullOrEmpty(name));
            _bubbleText.SetText(text);
            _bubbleText.EnableInClassList("hidden", string.IsNullOrEmpty(text));
            _bubbleSupport.text = support ?? "";
            _bubbleSupport.style.display = string.IsNullOrEmpty(support)
                ? new StyleEnum<DisplayStyle>(DisplayStyle.None)
                : new StyleEnum<DisplayStyle>(StyleKeyword.Null);
        }

        /// <summary>Two columns for tap choices, left and right of Ekqo, so he stays in view.</summary>
        private VisualElement[] ChoiceColumns()
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("choices");
            var left = new VisualElement { pickingMode = PickingMode.Ignore };
            var right = new VisualElement { pickingMode = PickingMode.Ignore };
            left.AddToClassList("choices__side");
            right.AddToClassList("choices__side");
            row.Add(left);
            row.Add(right);
            _ui.Add(row);
            return new[] { left, right };
        }

        private void ShowHear(bool on) => _hearButton.EnableInClassList("hidden", !on);

        /// <summary>What to do now, for the child, plus an English hint for the grown-up.</summary>
        private void SetInstruction(string forChild, string hint)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList("instruction");
            box.Add(Text(forChild, "instruction__main"));
            if (!string.IsNullOrEmpty(hint)) box.Add(Text(hint, "instruction__hint", "hint"));
            _ui.Add(box);
        }

        private VisualElement WordCard(string concept, string word, string gloss)
        {
            var card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.AddToClassList("word");
            if (concept != null)
            {
                var pic = new PictureView(concept);
                pic.AddToClassList("word__picture");
                card.Add(pic);
            }
            var text = new MixedText(font, "word__text");
            text.SetText(word);
            card.Add(text);
            card.Add(Text(gloss, "word__gloss", "hint"));
            return card;
        }

        private IEnumerator PanelCard(IconView.Kind icon, string title, string text, string note)
        {
            ClearUI();
            var panel = new VisualElement();
            panel.AddToClassList("panel");
            var i = new IconView(icon);
            i.AddToClassList("panel__icon");
            panel.Add(i);
            panel.Add(Text(title, "panel__title"));
            panel.Add(Text(text, "panel__text"));
            if (!string.IsNullOrEmpty(note)) panel.Add(Text(note, "panel__text"));
            _ui.Add(panel);
            Burst();
            yield return English($"{title} {text}");
            yield return WaitNext();
        }

        private void ShowError(string message)
        {
            ClearUI();
            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.Add(Text("Something went wrong", "panel__title"));
            panel.Add(Text(message, "panel__text"));
            _ui.Add(panel);
        }

        // ---------- speaking and waiting ----------

        private bool _talking;

        /// <summary>Shows a character's line in the bubble and plays it.</summary>
        private IEnumerator SayLine(string speaker, string text, string support, string voiceKey)
        {
            ShowBubble(speaker, text, support);
            _hearAgain = () => Speak(speaker, voiceKey, text);
            yield return Speak(speaker, voiceKey, text);
            yield return English(support);
        }

        /// <summary>Reads an English hint aloud when "English help" is on.</summary>
        private IEnumerator English(string text)
        {
            if (_hints && !string.IsNullOrEmpty(text)) yield return _voice.SayEnglish(text);
        }

        private IEnumerator Speak(string speaker, string key, string text)
        {
            if (string.IsNullOrEmpty(text) && key == null) yield break;
            _talking = speaker == "ekqo";
            yield return _voice.Say(key, text, speaker);
            _talking = false;
        }

        /// <summary>Shows ▶ (and 🔊 when there is something to hear again) and waits for ▶.</summary>
        private IEnumerator WaitNext()
        {
            _nextPressed = false;
            _nextButton.RemoveFromClassList("hidden");
            ShowHear(_hearAgain != null);
            while (!_nextPressed) yield return null;
            _nextPressed = false;
            _voice.Stop();
            _nextButton.AddToClassList("hidden");
            ShowHear(false);
        }

        private void Burst()
        {
            var rb = _root.worldBound;
            var c = _ekqo.worldBound.center;
            if (rb.width <= 0) return;
            _fall.From = _fall.To = new Vector2((c.x - rb.x) / rb.width, (c.y - rb.y) / rb.height);
            StartCoroutine(BurstAnim());
        }

        private IEnumerator BurstAnim()
        {
            for (var t = 1f; t < 1.3f; t += Time.deltaTime * 0.6f) { _fall.Progress = t; yield return null; }
            _fall.Progress = -1;
        }

        private List<T> Shuffle<T>(IEnumerable<T> items) => items.OrderBy(_ => _rng.Next()).ToList();

        private static bool Contains(string text, string word) =>
            text != null && text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

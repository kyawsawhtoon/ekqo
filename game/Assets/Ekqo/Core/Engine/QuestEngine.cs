using System;
using System.Collections.Generic;
using System.Linq;
using Ekqo.Core.Content;

namespace Ekqo.Core.Engine
{
    /// <summary>
    /// Runs quests step by step (architecture 4.1). Pure C#: no rendering, audio or network.
    /// The game listens to <see cref="Events"/> and answers with Continue / Answer / TeachDone /
    /// ReviewDone / CareDone. The AI never decides what happens next; this class does.
    /// </summary>
    public sealed class QuestEngine
    {
        public const int MissesBeforeTapOffer = 2;

        private readonly Bundle _bundle;
        private readonly Dictionary<string, Quest> _quests;
        private readonly Dictionary<string, EditionQuest> _editionQuests;
        private Quest _quest;
        private int _attempt;
        private int _repeatDone;
        private bool _waitingForCare;

        public SaveSnapshot State { get; }
        public event Action<EngineEvent> Events;

        public QuestEngine(Bundle bundle, SaveSnapshot save = null)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _quests = bundle.Core.Quests.ToDictionary(q => q.Id);
            _editionQuests = bundle.Edition.Quests.ToDictionary(q => q.Quest);
            State = save ?? new SaveSnapshot();
            if (State.UnlockedLocations.Count == 0)
                State.UnlockedLocations.AddRange(bundle.Core.World.Locations.Where(l => l.UnlockedBy == "start").Select(l => l.Id));
            if (State.CurrentQuest != null && _quests.TryGetValue(State.CurrentQuest, out var q)) _quest = q;
        }

        public Quest CurrentQuest => _quest;
        public Step CurrentStep => _quest != null && State.StepIndex < _quest.Steps.Count ? _quest.Steps[State.StepIndex] : null;

        /// <summary>The next quest in chapter order that is not completed yet.</summary>
        public string NextQuestId() =>
            _bundle.Core.World.Chapters.OrderBy(c => c.Number).SelectMany(c => c.Quests)
                .FirstOrDefault(id => !State.CompletedQuests.Contains(id));

        public void StartQuest(string questId)
        {
            if (!_quests.TryGetValue(questId, out var quest)) throw new ArgumentException($"Unknown quest {questId}");
            _quest = quest;
            State.CurrentQuest = questId;
            State.StepIndex = 0;
            if (quest.Ekqo?.StageOnStart is int stage) State.EkqoStage = Math.Max(State.EkqoStage, stage);
            EnterStep();
        }

        /// <summary>Re-emits the current step, for example after loading a save.</summary>
        public void Resume()
        {
            if (CurrentStep != null) EnterStep();
        }

        /// <summary>Counts active play time. Needs rise only here, never while the game is closed.</summary>
        public void Tick(double activeSeconds) => State.Care.Tick(_bundle.Core.Care, activeSeconds);

        // ---------- answers from the game ----------

        public void Continue()
        {
            var step = CurrentStep;
            if (step == null) return;
            switch (step.Type)
            {
                case "scene": case "line": case "discover": case "stage": case "reward":
                    Advance();
                    break;
                default:
                    throw new InvalidOperationException($"Step {step.Id} ({step.Type}) does not take Continue()");
            }
        }

        public void TeachDone(IEnumerable<WordOutcome> outcomes)
        {
            RequireStep("teach");
            Record(outcomes, $"teach:{_quest.Id}");
            Advance();
        }

        public void ReviewDone(IEnumerable<WordOutcome> outcomes)
        {
            RequireStep("review", "review_round");
            Record(outcomes, $"review:{_quest.Id}");
            Advance();
        }

        public void CareDone(string action, IEnumerable<WordOutcome> outcomes = null)
        {
            State.Care.Meet(_bundle.Core.Care, action);
            if (outcomes != null) Record(outcomes, $"care:{action}");
            if (_waitingForCare && CurrentStep?.Type == "care" && CurrentStep.Action == action)
            {
                _waitingForCare = false;
                Advance();
            }
        }

        public void Answer(AnswerResult result)
        {
            var step = CurrentStep;
            if (step == null || (step.Type != "say" && step.Type != "use" && step.Type != "conversation"))
                throw new InvalidOperationException("No question is waiting for an answer");

            var accepted = result.Verdict == Verdict.Correct || result.Mode == AnswerMode.Tap;
            var situation = $"{step.Type}:{_quest.Id}:{step.Id}";
            foreach (var concept in ExpectedConcepts(step).Union(result.Matched ?? new List<string>()))
            {
                if (!accepted) break;
                var w = Word(concept);
                if (result.Mode == AnswerMode.Speech) { w.Said++; if (_attempt == 1 && w.Said == 1) w.FirstTryCorrect = true; }
                else w.Tapped++;
                w.Situations.Add(situation);
            }

            if (accepted)
            {
                Emit(new FeedbackEvent { Verdict = Verdict.Correct, Recast = result.Mode == AnswerMode.Tap });
                _repeatDone++;
                var needed = step.Type == "conversation" ? step.Turns ?? 1 : step.Repeat ?? 1;
                if (_repeatDone >= needed) { Advance(); return; }
                _attempt = 0;
            }
            else
            {
                Emit(new FeedbackEvent { Verdict = result.Verdict, Recast = true });
            }
            Ask(step);
        }

        // ---------- step flow ----------

        private void EnterStep()
        {
            var step = CurrentStep;
            if (step == null) { CompleteQuest(); return; }
            _attempt = 0;
            _repeatDone = 0;

            switch (step.Type)
            {
                case "teach":
                    var teach = new TeachEvent { Concepts = step.Concepts.ToList(), Patterns = step.Patterns.ToList() };
                    foreach (var c in step.Concepts)
                        if (State.Words.TryGetValue(c, out var w) && w.FirstTryCorrect) teach.SkipShowEcho.Add(c);
                    foreach (var c in step.Concepts) Word(c).Heard++;
                    Emit(teach);
                    break;
                case "say": case "use": case "conversation":
                    Ask(step);
                    break;
                case "care":
                    if (step.FirstTime) State.Care.UnlockedActions.Add(step.Action);
                    _waitingForCare = true;
                    Emit(new CareEvent { Action = step.Action, FirstTime = step.FirstTime, Required = step.Required });
                    break;
                case "review": case "review_round":
                    Emit(new ReviewEvent { Concepts = PickReview() });
                    break;
                case "discover":
                    if (!State.Discoveries.Contains(step.Discovery)) State.Discoveries.Add(step.Discovery);
                    Emit(new PresentEvent { Step = step });
                    break;
                case "stage":
                    if (step.Stage is int stage) State.EkqoStage = Math.Max(State.EkqoStage, stage);
                    Emit(new PresentEvent { Step = step });
                    break;
                case "reward":
                    AddAll(State.Items, step.Items);
                    AddAll(State.Abilities, step.Abilities);
                    AddAll(State.Friends, step.Friends);
                    State.Sparks += step.Sparks ?? 0;
                    Emit(new PresentEvent { Step = step });
                    break;
                case "line":
                    foreach (var c in step.Concepts) Word(c).Heard++;
                    Emit(new PresentEvent { Step = step });
                    break;
                default: // scene
                    Emit(new PresentEvent { Step = step });
                    break;
            }
        }

        private void Ask(Step step)
        {
            _attempt++;
            Emit(new AskEvent { Step = step, Attempt = _attempt, RepeatIndex = _repeatDone + 1, OfferTap = _attempt > MissesBeforeTapOffer });
        }

        private void Advance()
        {
            State.StepIndex++;
            EnterStep();
        }

        private void CompleteQuest()
        {
            var q = _quest;
            if (q.Ekqo?.StageOnComplete is int stage) State.EkqoStage = Math.Max(State.EkqoStage, stage);
            AddAll(State.Abilities, q.Ekqo?.Abilities);
            AddAll(State.Items, q.Rewards);
            if (q.Care?.Unlocks != null) State.Care.UnlockedActions.Add(q.Care.Unlocks);
            var newLocations = _bundle.Core.World.Locations.Where(l => l.UnlockedBy == q.Id).Select(l => l.Id).ToList();
            AddAll(State.UnlockedLocations, newLocations);
            if (!State.CompletedQuests.Contains(q.Id)) State.CompletedQuests.Add(q.Id);
            State.CurrentQuest = null;
            State.StepIndex = 0;
            _quest = null;
            Emit(new QuestCompleteEvent { QuestId = q.Id, EkqoStage = State.EkqoStage, Rewards = q.Rewards.ToList(), Unlocks = newLocations });
        }

        // ---------- review ----------

        /// <summary>3-4 words from this quest's say list and 1-2 earlier ones, closest to learned first.</summary>
        private List<string> PickReview()
        {
            var own = _editionQuests.TryGetValue(_quest.Id, out var eq) ? eq.Say : new List<string>();
            var earlier = State.CompletedQuests
                .Where(id => _editionQuests.ContainsKey(id))
                .SelectMany(id => _editionQuests[id].Say)
                .Except(own)
                .Distinct();
            int Closeness(string c) => State.Words.TryGetValue(c, out var w) && !w.Learned ? w.Situations.Count : -1;
            var fromThis = own.OrderByDescending(Closeness).Take(4).ToList();
            var fromEarlier = earlier.Where(c => !(State.Words.TryGetValue(c, out var w) && w.Learned))
                .OrderByDescending(Closeness).Take(2);
            return fromThis.Concat(fromEarlier).ToList();
        }

        // ---------- helpers ----------

        private IEnumerable<string> ExpectedConcepts(Step step) =>
            (step.Expects?.Concepts ?? new List<string>()).Where(c => !c.Contains(":"));

        private void Record(IEnumerable<WordOutcome> outcomes, string situation)
        {
            foreach (var o in outcomes ?? Enumerable.Empty<WordOutcome>())
            {
                var w = Word(o.Concept);
                if (o.Heard) w.Heard++;
                if (o.Said) { w.Said++; w.Situations.Add(situation); }
                if (o.Tapped) { w.Tapped++; w.Situations.Add(situation); }
            }
        }

        private WordProgress Word(string concept)
        {
            if (!State.Words.TryGetValue(concept, out var w)) State.Words[concept] = w = new WordProgress();
            return w;
        }

        private void RequireStep(params string[] types)
        {
            if (CurrentStep == null || !types.Contains(CurrentStep.Type))
                throw new InvalidOperationException($"Expected a {string.Join(" or ", types)} step");
        }

        private static void AddAll(List<string> target, IEnumerable<string> values)
        {
            if (values == null) return;
            foreach (var v in values) if (!target.Contains(v)) target.Add(v);
        }

        private void Emit(EngineEvent e)
        {
            if (_quest != null) { e.QuestId ??= _quest.Id; e.StepId ??= CurrentStep?.Id; }
            Events?.Invoke(e);
        }
    }
}

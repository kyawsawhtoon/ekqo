using System.Collections.Generic;
using Ekqo.Core.Content;

namespace Ekqo.Core.Engine
{
    /// <summary>Everything the engine asks the game to show. The game answers with a dispatch.</summary>
    public abstract class EngineEvent
    {
        public string QuestId;
        public string StepId;
    }

    /// <summary>A scene, line, discovery, stage or reward step. The game calls Continue() when done.</summary>
    public sealed class PresentEvent : EngineEvent { public Step Step; }

    /// <summary>Listen-first teaching: hear, show (tap), echo. Skip show/echo for words in SkipShowEcho.</summary>
    public sealed class TeachEvent : EngineEvent
    {
        public List<string> Concepts = new List<string>();
        public List<string> Patterns = new List<string>();
        public HashSet<string> SkipShowEcho = new HashSet<string>();
    }

    /// <summary>A say/use/conversation step: play the prompt, show the mic and the tap options.</summary>
    public sealed class AskEvent : EngineEvent
    {
        public Step Step;
        public int Attempt;          // 1-based
        public int RepeatIndex;      // which repeat of a repeated step, 1-based
        public bool OfferTap;        // true after repeated misses: make the tap options prominent
    }

    /// <summary>Feedback after an answer: praise the attempt, recast on a miss. Never a failure screen.</summary>
    public sealed class FeedbackEvent : EngineEvent
    {
        public Verdict Verdict;
        public bool Recast;          // Ekqo says the right phrase ("Let's say it together")
    }

    public sealed class CareEvent : EngineEvent { public string Action; public bool FirstTime; public bool Required; }

    /// <summary>End-of-quest review: words from this quest and earlier ones.</summary>
    public sealed class ReviewEvent : EngineEvent { public List<string> Concepts = new List<string>(); }

    public sealed class QuestCompleteEvent : EngineEvent
    {
        public int EkqoStage;
        public List<string> Rewards = new List<string>();
        public List<string> Unlocks = new List<string>();
    }

    public enum Verdict { Correct, Partial, OtherLanguage, OffTopic, Unclear }
    public enum AnswerMode { Speech, Tap }

    public sealed class AnswerResult
    {
        public Verdict Verdict;
        public AnswerMode Mode;
        public List<string> Matched = new List<string>();
        /// <summary>The child used the hint (or got help after a wrong tap) before answering.</summary>
        public bool Hinted;
    }

    /// <summary>What happened to one word during teaching or review.</summary>
    public sealed class WordOutcome
    {
        public string Concept;
        public bool Heard;
        public bool Said;
        public bool Tapped;
    }
}

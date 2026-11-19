// Plain C# mirror of the compiled content bundle (packages/content/src/bundle.ts).
// Field names are PascalCase; Ekqo.Content maps them from the snake_case JSON.
// TODO(WS6): generate this file from the zod schema instead of keeping it by hand.
using System.Collections.Generic;

namespace Ekqo.Core.Content
{
    public sealed class Bundle
    {
        public string Version;
        public CoreContent Core;
        public EditionContent Edition;
    }

    public sealed class CoreContent
    {
        public World World;
        public ConceptRegistry Concepts;
        public Care Care;
        public DiscoveryList Discoveries;
        public List<Quest> Quests = new List<Quest>();
    }

    // ---------- world ----------
    public sealed class World
    {
        public EkqoInfo Ekqo;
        public List<Character> Characters = new List<Character>();
        public List<Location> Locations = new List<Location>();
        public List<Chapter> Chapters = new List<Chapter>();
        public List<Item> Items = new List<Item>();
    }

    public sealed class EkqoInfo
    {
        public List<EkqoStage> Stages = new List<EkqoStage>();
        public List<Ability> Abilities = new List<Ability>();
    }

    public sealed class EkqoStage { public int Stage; public string Id; public string Name; public string Reached; public string Moves; public string Power; }
    public sealed class Ability { public string Id; public string Meaning; public string UnlockedBy; }
    public sealed class Character { public string Id; public string Role; public string Personality; public string SpeaksToChild; public string ChildSpeaksTo; public int FirstChapter; }
    public sealed class Location { public string Id; public string Meaning; public string UnlockedBy; public List<string> Areas = new List<string>(); }
    public sealed class Chapter { public string Id; public int Number; public string Title; public List<string> Quests = new List<string>(); public string Tune; public int EndsWithStage; public List<string> ICan = new List<string>(); }
    public sealed class Item { public string Id; public string Kind; public string Meaning; }

    // ---------- concepts ----------
    public sealed class ConceptRegistry
    {
        public List<Concept> Concepts = new List<Concept>();
        public List<Pattern> Patterns = new List<Pattern>();
    }

    public sealed class Concept { public string Id; public string Gloss; public string Kind; public string Care; public double? Value; public string Image; }
    public sealed class Pattern { public string Id; public string Meaning; public Dictionary<string, string> Slots = new Dictionary<string, string>(); }

    // ---------- quests ----------
    public sealed class Quest
    {
        public string Id;
        public string Chapter;
        public string Title;
        public string Location;
        public string TimeOfDay;
        public List<string> Characters = new List<string>();
        public string Function;
        public List<string> Topics = new List<string>();
        public List<string> Patterns = new List<string>();
        public string ListeningWeight;
        public List<string> Reuses = new List<string>();
        public QuestCare Care;
        public List<string> Discoveries = new List<string>();
        public QuestEkqo Ekqo;
        public List<string> Rewards = new List<string>();
        public List<string> Unlocks = new List<string>();
        public string Music;
        public List<Step> Steps = new List<Step>();
    }

    public sealed class QuestCare { public string Unlocks; }
    public sealed class QuestEkqo { public int? StageOnStart; public int? StageOnComplete; public List<string> Abilities = new List<string>(); public bool FullShine; }

    /// <summary>One step of a quest. Which fields are set depends on <see cref="Type"/>.</summary>
    public sealed class Step
    {
        public string Id;
        public string Type;            // scene, line, teach, say, use, conversation, review_round, care, discover, stage, review, reward
        public string Meaning;
        public string Speaker;
        public List<string> Concepts = new List<string>();
        public List<string> Patterns = new List<string>();
        public string Pattern;
        public Dictionary<string, string> Fill;
        public string Speed;
        public Expects Expects;
        public AiPolicy Ai;
        public int? Repeat;
        public int? Turns;             // conversation
        public List<ReviewAsk> Asks;   // review_round
        public string Action;          // care
        public bool FirstTime;
        public bool Required;
        public string Discovery;       // discover
        public int? Stage;             // stage
        public bool FullShine;
        public List<string> Items = new List<string>();
        public List<string> Abilities = new List<string>();
        public int? Sparks;
        public List<string> Friends = new List<string>();
        public OnSuccess OnSuccess;    // say/use: what the game shows after a right answer
    }

    public sealed class OnSuccess { public string Effect; }

    public sealed class Expects
    {
        public string Intent;
        public List<string> Concepts = new List<string>();
        public List<string> AnyOf = new List<string>();
        public string Pattern;
        public List<string> PatternsAny = new List<string>();
        public Dictionary<string, string> SlotValues;
        public string FromTask;
        public bool Open;
    }

    public sealed class AiPolicy { public bool NpcVary; public string Judge; }
    public sealed class ReviewAsk { public string Speaker; public string FromQuest; public string Intent; }

    // ---------- care and discoveries ----------
    public sealed class Care
    {
        public List<Need> Needs = new List<Need>();
        public List<CareAction> Actions = new List<CareAction>();
    }

    public sealed class Need { public string Id; public string UnlockedBy; public double RisePerActiveMinute; public Dictionary<string, double> RiseOnEvents; public List<string> Cues = new List<string>(); public string MetBy; }
    public sealed class CareAction { public string Id; public string Need; public string ItemsFrom; public string Music; public List<CareMoment> Moments = new List<CareMoment>(); }
    public sealed class CareMoment { public string Id; public string Role; public string Meaning; public string Pattern; public Dictionary<string, string> Fill; public List<string> Concepts = new List<string>(); public string DrawsFrom; }

    public sealed class DiscoveryList { public List<Discovery> Discoveries = new List<Discovery>(); }
    public sealed class Discovery { public string Id; public string Category; public string Meaning; public List<string> Concepts = new List<string>(); }

    // ---------- edition ----------
    public sealed class EditionContent
    {
        public EditionInfo Edition;
        public Lexicon Lexicon;
        public EditionPatterns Patterns;
        public List<EditionQuest> Quests = new List<EditionQuest>();
    }

    public sealed class EditionInfo { public string Id; public string Name; public string NativeName; public string Locale; public string Script; public string SupportLanguage; public FontInfo Font; public Dictionary<string, CharacterName> Names = new Dictionary<string, CharacterName>(); }

    /// <summary>A character's name in this edition. Text is a string, or an object keyed by gender (parent).</summary>
    public sealed class CharacterName { public object Text; public string Roman; public string Status; }
    public sealed class FontInfo { public string Family; public bool Bundled; }
    public sealed class Lexicon { public List<LexiconEntry> Entries = new List<LexiconEntry>(); }
    public sealed class LexiconEntry { public string Concept; public string Text; public string Roman; public string Stem; public string Classifier; public bool Local; public string Gloss; public string Status; }
    public sealed class EditionPatterns { public List<PatternTemplate> Patterns = new List<PatternTemplate>(); }
    public sealed class PatternTemplate { public string Id; public string Template; public string Status; }

    public sealed class EditionQuest
    {
        public string Quest;
        public List<string> Say = new List<string>();
        public string SayReview;
        public List<string> Hear = new List<string>();
        public Dictionary<string, Line> Lines = new Dictionary<string, Line>();
    }

    public sealed class Line
    {
        public string Text;
        public string Prompt;
        public string Support;
        public string Roman;
        public List<string> Accept = new List<string>();
        public bool AcceptFromPattern;
        public List<LineVariant> Variants = new List<LineVariant>();
        public int? LiveUntilVariants;
        public string Status;
    }

    public sealed class LineVariant { public string Text; public string Support; public string Roman; public string Source; public string Status; }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Ekqo.Core.Content;

namespace Ekqo.Core.Engine
{
    /// <summary>What a character says on one step, in the edition language, with the support-language hint.</summary>
    public sealed class SpokenLine
    {
        public string Text;      // edition language; may be empty when the character only waits
        public string Support;   // support language (English), for the grown-up hint
    }

    /// <summary>One answer the child can tap on a say/use step.</summary>
    public sealed class TapOption
    {
        public string Text;
        public string Concept;   // the concept it stands for, when it is one word
        public bool Correct;
    }

    /// <summary>
    /// Turns content into the words on screen: edition lines, pattern templates filled with
    /// names and words, lexicon words and the tap options for say steps. Language-neutral:
    /// everything comes from the bundle, so a new edition needs no code here.
    /// </summary>
    public sealed class LineText
    {
        private static readonly Regex Slot = new Regex(@"\{([a-z_]+)(\.[a-z_]+)?\}");
        private static readonly Regex LeadingDirection = new Regex(@"^\s*\([^)]*\)\s*");

        private readonly Bundle _bundle;
        private readonly Dictionary<string, LexiconEntry> _lexicon;
        private readonly Dictionary<string, Concept> _concepts;
        private readonly Dictionary<string, Pattern> _corePatterns;
        private readonly Dictionary<string, PatternTemplate> _templates;
        private readonly Dictionary<string, EditionQuest> _editionQuests;

        /// <summary>The child's nickname, used for {player_name}.</summary>
        public string PlayerName = "";

        public LineText(Bundle bundle)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _lexicon = (bundle.Edition.Lexicon?.Entries ?? new List<LexiconEntry>())
                .GroupBy(e => e.Concept).ToDictionary(g => g.Key, g => g.First());
            _concepts = (bundle.Core.Concepts?.Concepts ?? new List<Concept>()).ToDictionary(c => c.Id);
            _corePatterns = (bundle.Core.Concepts?.Patterns ?? new List<Pattern>()).ToDictionary(p => p.Id);
            _templates = (bundle.Edition.Patterns?.Patterns ?? new List<PatternTemplate>()).ToDictionary(p => p.Id);
            _editionQuests = bundle.Edition.Quests.ToDictionary(q => q.Quest);
        }

        // ---------- words and names ----------

        /// <summary>The edition word for a concept, or "" if the lexicon has none.</summary>
        public string Word(string concept) =>
            concept != null && _lexicon.TryGetValue(concept, out var e) ? e.Text ?? "" : "";

        /// <summary>The support-language gloss for a concept ("hello").</summary>
        public string Gloss(string concept) =>
            concept != null && _concepts.TryGetValue(concept, out var c) ? c.Gloss ?? concept : concept ?? "";

        public string Kind(string concept) =>
            concept != null && _concepts.TryGetValue(concept, out var c) ? c.Kind : null;

        /// <summary>A character's name in this edition. Falls back to the id.</summary>
        public string Name(string character)
        {
            var names = _bundle.Edition.Edition?.Names;
            if (names != null && names.TryGetValue(character, out var n) && n.Text is string s && s.Length > 0) return s;
            return character.Length > 0 ? char.ToUpperInvariant(character[0]) + character.Substring(1) : character;
        }

        public string PatternMeaning(string patternId) =>
            patternId != null && _corePatterns.TryGetValue(patternId, out var p) ? p.Meaning : "";

        /// <summary>The edition template for a pattern with its slots filled.</summary>
        public string Pattern(string patternId, IDictionary<string, string> fill = null)
        {
            if (patternId == null || !_templates.TryGetValue(patternId, out var t)) return "";
            return Slot.Replace(t.Template, m =>
            {
                var slot = m.Groups[1].Value;
                string value = null;
                if (fill != null && fill.TryGetValue(slot, out var v)) value = Value(v);
                else if (_corePatterns.TryGetValue(patternId, out var p) && p.Slots.TryGetValue(slot, out var kind) && kind == "player_name")
                    value = PlayerName;
                return string.IsNullOrEmpty(value) ? "___" : value;
            });
        }

        /// <summary>A fill value: "{ekqo}" is a character name, "{player_name}" the child, a concept id its word.</summary>
        public string Value(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            if (raw.StartsWith("{") && raw.EndsWith("}"))
            {
                var key = raw.Substring(1, raw.Length - 2);
                return key == "player_name" ? PlayerName : Name(key);
            }
            var word = Word(raw);
            return word.Length > 0 ? word : raw;
        }

        // ---------- steps ----------

        public EditionQuest EditionQuest(string questId) =>
            _editionQuests.TryGetValue(questId, out var q) ? q : null;

        public Line EditionLine(string questId, string stepId)
        {
            var q = EditionQuest(questId);
            return q != null && q.Lines.TryGetValue(stepId, out var l) ? l : null;
        }

        /// <summary>
        /// The line for a `line` step, or the prompt for a `say` step. A say step without a
        /// prompt returns empty text: the character just waits for the child.
        /// </summary>
        public SpokenLine ForStep(string questId, Step step)
        {
            var line = EditionLine(questId, step.Id);
            var support = line?.Support ?? CleanMeaning(step.Meaning);
            if (step.Type == "say" || step.Type == "use" || step.Type == "conversation")
                return new SpokenLine { Text = line?.Prompt ?? "", Support = line?.Prompt != null ? line.Support ?? "" : "" };

            string text = line?.Text;
            if (string.IsNullOrEmpty(text) && step.Pattern != null) text = Pattern(step.Pattern, step.Fill);
            if (string.IsNullOrEmpty(text) && step.Concepts.Count > 0) text = Word(step.Concepts[0]);
            return new SpokenLine { Text = text ?? "", Support = support };
        }

        /// <summary>
        /// Tap answers for a say/use step: the right answers from the content, plus one
        /// distractor when there is only one right answer, so tapping is still a choice.
        /// </summary>
        public List<TapOption> Options(string questId, Step step)
        {
            var options = new List<TapOption>();
            var ex = step.Expects ?? new Expects();
            var line = EditionLine(questId, step.Id);
            var expected = new HashSet<string>(ex.AnyOf.Concat(ex.Concepts));

            if (ex.AnyOf.Count > 0)
                foreach (var c in ex.AnyOf) Add(options, Word(c), c, true);
            else if (ex.Pattern != null)
            {
                Add(options, Pattern(ex.Pattern, ex.SlotValues), null, true);
                // A name pattern gets the same sentence with someone else's name as the distractor.
                if (ex.SlotValues != null && ex.SlotValues.Values.Contains("{player_name}"))
                {
                    var other = ex.SlotValues.ToDictionary(kv => kv.Key, kv => kv.Value == "{player_name}" ? "{ekqo}" : kv.Value);
                    if (Value("{ekqo}") != PlayerName) Add(options, Pattern(ex.Pattern, other), null, false);
                }
            }
            else if (ex.Concepts.Count > 0)
                Add(options, Word(ex.Concepts[0]), ex.Concepts[0], true);
            else if (line?.Accept?.Count > 0)
                Add(options, line.Accept[0], null, true);

            if (options.Count(o => o.Correct) == 1 && options.Count == 1)
            {
                var d = Distractor(questId, step, expected);
                if (d != null) Add(options, Word(d), d, false);
            }
            return options;
        }

        /// <summary>A say-word of this quest that is not expected here, of the same kind when possible.</summary>
        public string Distractor(string questId, Step step, ICollection<string> expected)
        {
            var say = EditionQuest(questId)?.Say ?? new List<string>();
            var kind = expected.Select(Kind).FirstOrDefault(k => k != null);
            var pool = say.Where(c => !expected.Contains(c) && Word(c).Length > 0).ToList();
            var same = pool.Where(c => Kind(c) == kind).ToList();
            if (same.Count > 0) pool = same;
            if (pool.Count == 0) return null;
            return pool[StableIndex(step.Id, pool.Count)];
        }

        private static void Add(List<TapOption> options, string text, string concept, bool correct)
        {
            if (string.IsNullOrEmpty(text) || options.Any(o => o.Text == text)) return;
            options.Add(new TapOption { Text = text, Concept = concept, Correct = correct });
        }

        /// <summary>Drops stage directions like "(very quietly)" from a step's meaning.</summary>
        public static string CleanMeaning(string meaning) =>
            meaning == null ? "" : LeadingDirection.Replace(meaning, "").Trim();

        private static int StableIndex(string seed, int count)
        {
            unchecked
            {
                var h = 17;
                foreach (var ch in seed ?? "") h = h * 31 + ch;
                return Math.Abs(h % count);
            }
        }
    }
}

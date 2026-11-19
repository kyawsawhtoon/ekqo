using System.Collections.Generic;
using System.Linq;
using Ekqo.Core.Content;
using Ekqo.Core.Engine;
using NUnit.Framework;

namespace Ekqo.Core.Tests
{
    public class LineTextTests
    {
        private static Bundle Q1Bundle() => new Bundle
        {
            Core = new CoreContent
            {
                World = new World(),
                Concepts = new ConceptRegistry
                {
                    Concepts = new List<Concept>
                    {
                        new Concept { Id = "greet.hello", Gloss = "hello", Kind = "phrase" },
                        new Concept { Id = "greet.bye", Gloss = "bye", Kind = "phrase" },
                        new Concept { Id = "word.name", Gloss = "name", Kind = "noun" },
                        new Concept { Id = "ans.yes", Gloss = "yes", Kind = "phrase" },
                        new Concept { Id = "ans.no", Gloss = "no", Kind = "phrase" },
                    },
                    Patterns = new List<Pattern>
                    {
                        new Pattern { Id = "pat.my_name_is", Meaning = "My name is ___", Slots = new Dictionary<string, string> { ["x"] = "player_name" } },
                    },
                },
            },
            Edition = new EditionContent
            {
                Edition = new EditionInfo { Names = new Dictionary<string, CharacterName> { ["ekqo"] = new CharacterName { Text = "Ekqo" } } },
                Lexicon = new Lexicon
                {
                    Entries = new List<LexiconEntry>
                    {
                        new LexiconEntry { Concept = "greet.hello", Text = "မင်္ဂလာပါ" },
                        new LexiconEntry { Concept = "greet.bye", Text = "တာ့တာ" },
                        new LexiconEntry { Concept = "word.name", Text = "နာမည်" },
                        new LexiconEntry { Concept = "ans.yes", Text = "ဟုတ်ကဲ့" },
                        new LexiconEntry { Concept = "ans.no", Text = "မဟုတ်ဘူး" },
                    },
                },
                Patterns = new EditionPatterns { Patterns = new List<PatternTemplate> { new PatternTemplate { Id = "pat.my_name_is", Template = "နာမည် {x} ပါ" } } },
                Quests = new List<EditionQuest>
                {
                    new EditionQuest
                    {
                        Quest = "q01",
                        Say = new List<string> { "greet.hello", "word.name", "ans.yes", "ans.no", "greet.bye" },
                        Lines = new Dictionary<string, Line>
                        {
                            ["who"] = new Line { Text = "ဘယ်သူလဲ" },
                            ["say_scared"] = new Line { Prompt = "ကြောက်လား", Support = "Are you scared?" },
                        },
                    },
                },
            },
        };

        [Test]
        public void LineStepUsesTheEditionLineAndCleansTheMeaning()
        {
            var t = new LineText(Q1Bundle());
            var line = t.ForStep("q01", new Step { Id = "who", Type = "line", Meaning = "(quietly) Who is it?" });
            Assert.AreEqual("ဘယ်သူလဲ", line.Text);
            Assert.AreEqual("Who is it?", line.Support);
        }

        [Test]
        public void PatternLineIsFilledWithACharacterName()
        {
            var t = new LineText(Q1Bundle());
            var step = new Step { Id = "ekqo_name", Type = "line", Pattern = "pat.my_name_is", Fill = new Dictionary<string, string> { ["x"] = "{ekqo}" } };
            Assert.AreEqual("နာမည် Ekqo ပါ", t.ForStep("q01", step).Text);
        }

        [Test]
        public void NameAnswerUsesThePlayerNameWithEkqoAsTheDistractor()
        {
            var t = new LineText(Q1Bundle()) { PlayerName = "Su" };
            var step = new Step
            {
                Id = "say_name", Type = "say",
                Expects = new Expects { Pattern = "pat.my_name_is", SlotValues = new Dictionary<string, string> { ["x"] = "{player_name}" } },
            };
            var options = t.Options("q01", step);
            Assert.AreEqual("နာမည် Su ပါ", options.Single(o => o.Correct).Text);
            Assert.AreEqual("နာမည် Ekqo ပါ", options.Single(o => !o.Correct).Text);
        }

        [Test]
        public void YesOrNoAreBothRightAndNeedNoDistractor()
        {
            var t = new LineText(Q1Bundle());
            var step = new Step { Id = "say_scared", Type = "say", Expects = new Expects { AnyOf = new List<string> { "ans.yes", "ans.no" } } };
            var options = t.Options("q01", step);
            Assert.AreEqual(2, options.Count);
            Assert.IsTrue(options.All(o => o.Correct));
            Assert.AreEqual("ကြောက်လား", t.ForStep("q01", step).Text);
        }

        [Test]
        public void OneRightAnswerGetsADistractorOfTheSameKind()
        {
            var t = new LineText(Q1Bundle());
            var step = new Step { Id = "say_hello", Type = "say", Expects = new Expects { Concepts = new List<string> { "greet.hello" } } };
            var options = t.Options("q01", step);
            Assert.AreEqual(2, options.Count);
            Assert.AreEqual("မင်္ဂလာပါ", options.Single(o => o.Correct).Text);
            var wrong = options.Single(o => !o.Correct);
            Assert.AreEqual("phrase", t.Kind(wrong.Concept));
            Assert.AreEqual("", t.ForStep("q01", step).Text, "Ekqo just waits for hello");
        }
    }
}

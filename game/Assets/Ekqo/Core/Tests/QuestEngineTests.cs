using System.Collections.Generic;
using System.Linq;
using Ekqo.Core.Content;
using Ekqo.Core.Engine;
using NUnit.Framework;

namespace Ekqo.Core.Tests
{
    public class QuestEngineTests
    {
        private static Bundle TinyBundle()
        {
            var q1 = new Quest
            {
                Id = "q01", Chapter = "ch1", Title = "A Light in the Garden", Location = "house.garden",
                Rewards = new List<string> { "glow_jar" },
                Ekqo = new QuestEkqo { StageOnStart = 1 },
                Steps = new List<Step>
                {
                    new Step { Id = "light_falls", Type = "scene" },
                    new Step { Id = "teach_greeting", Type = "teach", Concepts = new List<string> { "greet.hello" } },
                    new Step { Id = "say_hello", Type = "say", Speaker = "ekqo",
                        Expects = new Expects { Intent = "greets Ekqo", Concepts = new List<string> { "greet.hello" } } },
                    new Step { Id = "discover_name", Type = "discover", Discovery = "ekqo.name" },
                    new Step { Id = "review", Type = "review" },
                },
            };
            var q2 = new Quest
            {
                Id = "q02", Chapter = "ch1", Title = "Hungry Ekqo", Location = "house.kitchen",
                Care = new QuestCare { Unlocks = "feed" },
                Steps = new List<Step> { new Step { Id = "try_foods", Type = "care", Action = "feed", FirstTime = true } },
            };
            return new Bundle
            {
                Core = new CoreContent
                {
                    World = new World
                    {
                        Locations = new List<Location>
                        {
                            new Location { Id = "house", UnlockedBy = "start" },
                            new Location { Id = "street", UnlockedBy = "q01" },
                        },
                        Chapters = new List<Chapter> { new Chapter { Id = "ch1", Number = 1, Quests = new List<string> { "q01", "q02" } } },
                    },
                    Care = new Care
                    {
                        Needs = new List<Need> { new Need { Id = "hunger", MetBy = "feed", RisePerActiveMinute = 0.5 } },
                        Actions = new List<CareAction> { new CareAction { Id = "feed", Need = "hunger" } },
                    },
                    Quests = new List<Quest> { q1, q2 },
                },
                Edition = new EditionContent
                {
                    Quests = new List<EditionQuest>
                    {
                        new EditionQuest { Quest = "q01", Say = new List<string> { "greet.hello", "word.name" } },
                        new EditionQuest { Quest = "q02", Say = new List<string> { "food.rice" } },
                    },
                },
            };
        }

        [Test]
        public void PlaysQ1ToTheEnd()
        {
            var engine = new QuestEngine(TinyBundle());
            var events = new List<EngineEvent>();
            engine.Events += events.Add;

            engine.StartQuest("q01");
            Assert.AreEqual(1, engine.State.EkqoStage, "Ekqo wakes at stage 1 in Q1");
            Assert.IsInstanceOf<PresentEvent>(events.Last());

            engine.Continue();
            Assert.IsInstanceOf<TeachEvent>(events.Last());
            engine.TeachDone(new[] { new WordOutcome { Concept = "greet.hello", Heard = true, Tapped = true } });
            Assert.IsInstanceOf<AskEvent>(events.Last());

            engine.Answer(new AnswerResult { Verdict = Verdict.Correct, Mode = AnswerMode.Speech });
            Assert.Contains("ekqo.name", engine.State.Discoveries);

            engine.Continue();
            var review = (ReviewEvent)events.Last();
            CollectionAssert.Contains(review.Concepts, "greet.hello");
            engine.ReviewDone(new[] { new WordOutcome { Concept = "greet.hello", Said = true } });

            var done = (QuestCompleteEvent)events.Last();
            Assert.AreEqual("q01", done.QuestId);
            CollectionAssert.Contains(engine.State.Items, "glow_jar");
            CollectionAssert.Contains(engine.State.UnlockedLocations, "street");
            Assert.AreEqual("q02", engine.NextQuestId());
        }

        [Test]
        public void WordIsLearnedAfterThreeSituations()
        {
            var engine = new QuestEngine(TinyBundle());
            engine.StartQuest("q01");
            engine.Continue();
            engine.TeachDone(new[] { new WordOutcome { Concept = "greet.hello", Tapped = true } });
            engine.Answer(new AnswerResult { Verdict = Verdict.Correct, Mode = AnswerMode.Speech });
            Assert.IsFalse(engine.State.Words["greet.hello"].Learned);
            engine.Continue();
            engine.ReviewDone(new[] { new WordOutcome { Concept = "greet.hello", Said = true } });
            Assert.IsTrue(engine.State.Words["greet.hello"].Learned);
        }

        [Test]
        public void MissesRecastAndThenOfferTap_NeverBlock()
        {
            var engine = new QuestEngine(TinyBundle());
            var events = new List<EngineEvent>();
            engine.Events += events.Add;
            engine.StartQuest("q01");
            engine.Continue();
            engine.TeachDone(new WordOutcome[0]);

            engine.Answer(new AnswerResult { Verdict = Verdict.Unclear, Mode = AnswerMode.Speech });
            engine.Answer(new AnswerResult { Verdict = Verdict.OffTopic, Mode = AnswerMode.Speech });
            var ask = (AskEvent)events.Last();
            Assert.IsTrue(ask.OfferTap);
            Assert.IsTrue(events.OfType<FeedbackEvent>().All(f => f.Recast));

            engine.Answer(new AnswerResult { Verdict = Verdict.Unclear, Mode = AnswerMode.Tap });
            Assert.AreEqual("discover_name", engine.CurrentStep.Id, "a tap always moves the quest on");
        }

        [Test]
        public void NeedsRiseOnlyDuringPlayAndOnlyWhenUnlocked()
        {
            var bundle = TinyBundle();
            var engine = new QuestEngine(bundle);
            engine.Tick(600);
            Assert.IsFalse(engine.State.Care.Levels.ContainsKey("hunger"), "feeding is not unlocked yet");

            engine.StartQuest("q02");
            engine.Tick(120);
            Assert.AreEqual(1.0, engine.State.Care.Levels["hunger"], 1e-9);
            engine.Tick(6000);
            Assert.AreEqual(CareState.MaxLevel, engine.State.Care.Levels["hunger"], "capped: grumpy at worst");
            Assert.AreEqual("grumpy", engine.State.Care.Mood());

            engine.CareDone("feed");
            Assert.AreEqual(0, engine.State.Care.Levels["hunger"]);
            CollectionAssert.Contains(engine.State.CompletedQuests, "q02");
        }
    }
}

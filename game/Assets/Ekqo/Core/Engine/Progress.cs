using System;
using System.Collections.Generic;
using System.Linq;
using Ekqo.Core.Content;

namespace Ekqo.Core.Engine
{
    /// <summary>Per-concept learning progress. A word is learned after use in three different situations.</summary>
    public sealed class WordProgress
    {
        public const int SituationsToLearn = 3;

        public int Heard;
        public int Said;
        public int Tapped;
        public bool FirstTryCorrect;
        public HashSet<string> Situations = new HashSet<string>();
        public bool Learned => Said > 0 && Situations.Count >= SituationsToLearn;
    }

    /// <summary>Ekqo's needs. Levels run 0 (met) to 3 (strong). They rise only during active play.</summary>
    public sealed class CareState
    {
        public const double MaxLevel = 3;
        public Dictionary<string, double> Levels = new Dictionary<string, double>();
        public HashSet<string> UnlockedActions = new HashSet<string>();

        public void Tick(Care care, double activeSeconds)
        {
            if (activeSeconds <= 0) return;
            foreach (var need in care.Needs)
            {
                if (!UnlockedActions.Contains(need.MetBy)) continue;
                Levels.TryGetValue(need.Id, out var level);
                Levels[need.Id] = Math.Min(MaxLevel, level + need.RisePerActiveMinute * activeSeconds / 60.0);
            }
        }

        public void Meet(Care care, string action)
        {
            foreach (var need in care.Needs.Where(n => n.MetBy == action)) Levels[need.Id] = 0;
        }

        public void Raise(string needId, double amount)
        {
            Levels.TryGetValue(needId, out var level);
            Levels[needId] = Math.Min(MaxLevel, level + amount);
        }

        /// <summary>happy when every need is at most 1, grumpy when any need is full, okay otherwise.</summary>
        public string Mood()
        {
            if (Levels.Values.Any(l => l >= MaxLevel)) return "grumpy";
            if (Levels.Values.All(l => l <= 1)) return "happy";
            return "okay";
        }
    }

    /// <summary>Everything needed to resume play. Stored locally and synced to the API.</summary>
    public sealed class SaveSnapshot
    {
        public int Version = 1;
        public string CurrentQuest;
        public int StepIndex;
        public int EkqoStage;
        public List<string> CompletedQuests = new List<string>();
        public List<string> Abilities = new List<string>();
        public List<string> Items = new List<string>();
        public List<string> Discoveries = new List<string>();
        public List<string> UnlockedLocations = new List<string>();
        public List<string> Friends = new List<string>();
        public int Sparks;
        public Dictionary<string, WordProgress> Words = new Dictionary<string, WordProgress>();
        public CareState Care = new CareState();
    }
}

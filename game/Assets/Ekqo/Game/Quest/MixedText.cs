using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ekqo.Game
{
    /// <summary>
    /// Text that mixes the edition script with Latin letters, like "နာမည် Kyaw ပါ".
    /// Noto Sans Myanmar has no Latin letters, so the text is split into runs and each run
    /// gets its own font: the edition font for Myanmar script, the theme font for the rest.
    /// </summary>
    internal sealed class MixedText : VisualElement
    {
        private readonly Font _font;
        private readonly string _runClass;

        public MixedText(Font font, string runClass)
        {
            _font = font;
            _runClass = runClass;
            AddToClassList("mixed");
            pickingMode = PickingMode.Ignore;
        }

        public void SetText(string text)
        {
            Clear();
            if (string.IsNullOrEmpty(text)) return;
            var run = new StringBuilder();
            var runIsScript = IsScript(text[0]);
            foreach (var ch in text)
            {
                var isScript = IsScript(ch) || (ch == ' ' && runIsScript);
                if (isScript != runIsScript && run.Length > 0)
                {
                    AddRun(run.ToString(), runIsScript);
                    run.Clear();
                }
                runIsScript = isScript;
                run.Append(ch);
            }
            AddRun(run.ToString(), runIsScript);
        }

        private void AddRun(string text, bool script)
        {
            var trimmed = text.Trim();
            if (trimmed.Length == 0) return;
            var label = new Label(trimmed) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(_runClass);
            label.AddToClassList(script ? "run--script" : "run--latin");
            if (script && _font != null) label.style.unityFontDefinition = FontDefinition.FromFont(_font);
            Add(label);
        }

        /// <summary>Myanmar, Myanmar Extended-A and Extended-B.</summary>
        public static bool IsScript(char ch) =>
            (ch >= 'က' && ch <= '႟') || (ch >= 'ꩠ' && ch <= 'ꩿ') || (ch >= 'ꧠ' && ch <= '꧿');
    }
}

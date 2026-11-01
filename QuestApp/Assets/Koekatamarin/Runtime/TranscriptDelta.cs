namespace Koekatamarin
{
    // Port of KoeKatamarinBehaviourScript.Update: emit from the first changed
    // character onwards. Corrections do not remove words already in flight.
    public sealed class TranscriptDelta
    {
        string previous = "";

        public string Accept(string text)
        {
            text ??= "";
            int common = 0;
            while (common < text.Length && common < previous.Length && text[common] == previous[common]) common++;
            // Keep supplementary Unicode characters intact when a correction shares a high surrogate.
            if (common > 0 && common < text.Length && char.IsLowSurrogate(text[common])) common--;
            previous = text;
            return text.Substring(common);
        }

        public void Reset() { previous = ""; }
    }
}

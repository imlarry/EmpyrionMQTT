namespace EmpyrionMQTT.ShapeBaker;

// Extracts the union of shape names referenced by BlockShapesWindow.ecf.
// The file's "Shapes:" properties carry comma-separated shape KEYs that
// the in-game palette can place; that union is also the canonical set of
// building-block primitives we want to voxelize. Markup tokens like
// <newline>, <pagebreak>, and <empty> are stripped.
internal static class EcfShapeReader
{
    public static HashSet<string> ReadShapeNames(string ecfPath)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rawLine in File.ReadLines(ecfPath))
        {
            var line = StripComment(rawLine);
            int kvStart = line.IndexOf("Shapes:", StringComparison.Ordinal);
            if (kvStart < 0) continue;

            int firstQuote = line.IndexOf('"', kvStart);
            if (firstQuote < 0) continue;
            int lastQuote = line.LastIndexOf('"');
            if (lastQuote <= firstQuote) continue;

            var listText = line.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
            foreach (var raw in listText.Split(','))
            {
                var tok = raw.Trim();
                if (tok.Length == 0) continue;
                if (tok.StartsWith("<")) continue;     // <newline>, <pagebreak>, <empty>
                names.Add(tok);
            }
        }

        return names;
    }

    private static string StripComment(string line)
    {
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"') inQuotes = !inQuotes;
            else if (c == '#' && !inQuotes) return line.Substring(0, i);
        }
        return line;
    }
}

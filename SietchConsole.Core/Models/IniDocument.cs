namespace SietchConsole.Core.Models;

/// <summary>
/// Parsed representation of an INI file that preserves all original lines
/// (comments, blank lines, unknown keys) so a round-trip save produces
/// minimal diffs.
/// </summary>
public class IniDocument
{
    private readonly List<string> _lines = [];

    // section -> key -> line index
    private readonly Dictionary<string, Dictionary<string, int>> _index =
        new(StringComparer.OrdinalIgnoreCase);

    // section -> index of last line belonging to that section
    private readonly Dictionary<string, int> _sectionEnd =
        new(StringComparer.OrdinalIgnoreCase);

    public string FilePath { get; }

    private IniDocument(string filePath) => FilePath = filePath;

    public static IniDocument Parse(string filePath, string content)
    {
        var doc = new IniDocument(filePath);
        var lines = content.Split('\n');
        string? currentSection = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var raw = lines[i].TrimEnd('\r');
            doc._lines.Add(raw);

            var trimmed = raw.Trim();

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                currentSection = trimmed[1..^1].Trim();
                if (!doc._index.ContainsKey(currentSection))
                    doc._index[currentSection] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                doc._sectionEnd[currentSection] = i;
            }
            else if (currentSection is not null && trimmed.Length > 0 && !trimmed.StartsWith(';') && !trimmed.StartsWith('#'))
            {
                var eq = trimmed.IndexOf('=');
                if (eq > 0)
                {
                    var key = trimmed[..eq].Trim();
                    if (!doc._index[currentSection].ContainsKey(key))
                        doc._index[currentSection][key] = i;
                    doc._sectionEnd[currentSection] = i;
                }
                else
                {
                    doc._sectionEnd[currentSection] = i;
                }
            }
            else if (currentSection is not null)
            {
                doc._sectionEnd[currentSection] = i;
            }
        }

        return doc;
    }

    public string? GetValue(string section, string key)
    {
        if (!_index.TryGetValue(section, out var keys)) return null;
        if (!keys.TryGetValue(key, out var lineIdx)) return null;
        var line = _lines[lineIdx];
        var eq = line.IndexOf('=');
        return eq >= 0 ? line[(eq + 1)..].Trim() : null;
    }

    public void SetValue(string section, string key, string value)
    {
        if (_index.TryGetValue(section, out var keys) && keys.TryGetValue(key, out var lineIdx))
        {
            _lines[lineIdx] = $"{key}={value}";
            return;
        }

        if (_index.TryGetValue(section, out keys))
        {
            // Section exists — append key after last line of section
            int insertAt = _sectionEnd[section] + 1;
            _lines.Insert(insertAt, $"{key}={value}");
            keys[key] = insertAt;
            ShiftIndices(insertAt + 1);
            _sectionEnd[section] = insertAt;
        }
        else
        {
            // Section doesn't exist — append at end
            if (_lines.Count > 0 && !string.IsNullOrWhiteSpace(_lines[^1]))
                _lines.Add(string.Empty);

            int sectionLine = _lines.Count;
            _lines.Add($"[{section}]");
            int keyLine = _lines.Count;
            _lines.Add($"{key}={value}");

            _index[section] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [key] = keyLine
            };
            _sectionEnd[section] = keyLine;
        }
    }

    public string GetRawText() => string.Join("\n", _lines);

    private void ShiftIndices(int fromLine)
    {
        foreach (var section in _index.Values)
        {
            var keys = section.Keys.ToList();
            foreach (var k in keys)
                if (section[k] >= fromLine)
                    section[k]++;
        }
        foreach (var s in _sectionEnd.Keys.ToList())
            if (_sectionEnd[s] >= fromLine)
                _sectionEnd[s]++;
    }
}

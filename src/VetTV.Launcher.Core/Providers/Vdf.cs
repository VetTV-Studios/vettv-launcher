namespace VetTV.Launcher.Core.Providers;
/// Minimal Valve KeyValues (text VDF/ACF) parser.
public sealed class VdfNode
{
    public string? Value { get; init; }
    public Dictionary<string, VdfNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    public VdfNode? this[string key] => Children.TryGetValue(key, out var n) ? n : null;
    public string? Get(string key) => this[key]?.Value;
}
public static class Vdf
{
    public static VdfNode Parse(string text)
    {
        int i = 0; var root = new VdfNode();
        ParseObject(text, ref i, root, top: true);
        return root;
    }
    static void ParseObject(string s, ref int i, VdfNode node, bool top)
    {
        while (true)
        {
            var key = NextToken(s, ref i);
            if (key is null) { if (top) return; throw new FormatException("Unexpected end of VDF"); }
            if (key == "}") { if (top) throw new FormatException("Unbalanced }"); return; }
            var val = NextToken(s, ref i) ?? throw new FormatException($"Missing value for '{key}'");
            if (val == "{") { var child = new VdfNode(); ParseObject(s, ref i, child, false); node.Children[key] = child; }
            else node.Children[key] = new VdfNode { Value = val };
        }
    }
    static string? NextToken(string s, ref int i)
    {
        while (i < s.Length)
        {
            if (char.IsWhiteSpace(s[i])) { i++; continue; }
            if (s[i] == '/' && i + 1 < s.Length && s[i + 1] == '/') { while (i < s.Length && s[i] != '\n') i++; continue; }
            break;
        }
        if (i >= s.Length) return null;
        char c = s[i];
        if (c == '{' || c == '}') { i++; return c.ToString(); }
        var sb = new System.Text.StringBuilder();
        if (c == '"')
        {
            i++;
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length) { i++; sb.Append(s[i] switch { 'n' => '\n', 't' => '\t', _ => s[i] }); }
                else sb.Append(s[i]);
                i++;
            }
            i++; return sb.ToString();
        }
        while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != '{' && s[i] != '}') sb.Append(s[i++]);
        return sb.ToString();
    }
}

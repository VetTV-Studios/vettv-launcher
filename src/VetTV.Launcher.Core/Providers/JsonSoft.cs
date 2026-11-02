using System.Text.Json;
namespace VetTV.Launcher.Core.Providers;
/// Soft-failing JSON helpers: missing/corrupt files give null, never throw.
static class JsonSoft
{
    public static JsonDocument? Load(string path)
    {
        try { return File.Exists(path) ? JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) : null; }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }
    public static string? Str(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    public static bool Bool(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
    public static string FolderTitle(string? path, string fallback) =>
        string.IsNullOrWhiteSpace(path) ? fallback : Path.GetFileName(path.Replace('/', '\\').TrimEnd('\\').Split('\\').Last()) is { Length: > 0 } n ? n : fallback;
}

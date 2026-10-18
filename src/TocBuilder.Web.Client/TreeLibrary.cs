using System.Text.Json;
using Microsoft.JSInterop;
using TocBuilder.Domain;

namespace TocBuilder.Web.Client;

public sealed record SavedTree(string Id, string Name);

/// <summary>The user's list of trees, kept in the browser's localStorage. Each tree is stored as the same JSON that export produces.</summary>
public sealed class TreeLibrary(IJSRuntime js)
{
    private const string IndexKey = "toc-builder:index";
    private const string CurrentKey = "toc-builder:current";
    private readonly SemaphoreSlim writes = new(1, 1);   // the index is read, changed and written back across awaits, so writers take turns
    private static string TreeKey(string id) => $"toc-builder:tree:{id}";

    public async Task<List<SavedTree>> ListAsync()
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", IndexKey);
        if (json is null) return [];
        try { return JsonSerializer.Deserialize<List<SavedTree>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    public async Task<string?> CurrentIdAsync() => await js.InvokeAsync<string?>("localStorage.getItem", CurrentKey);

    public ValueTask SetCurrentAsync(string id) => js.InvokeVoidAsync("localStorage.setItem", CurrentKey, id);

    public async Task<ImportResult> LoadAsync(string id)
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", TreeKey(id));
        return json is null
            ? new ImportResult(ImportOutcome.Corrupt, Message: "This tree is missing from the browser's storage.")
            : TreeSerializer.Import(json);
    }

    /// <summary>Writes the tree and records its name in the list, adding it when new.</summary>
    public async Task SaveAsync(string id, string name, Tree tree)
    {
        await writes.WaitAsync();
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", TreeKey(id), TreeSerializer.Export(name, tree));
            var list = await ListAsync();
            var index = list.FindIndex(t => t.Id == id);
            if (index < 0) list.Add(new SavedTree(id, name));
            else list[index] = new SavedTree(id, name);
            await js.InvokeVoidAsync("localStorage.setItem", IndexKey, JsonSerializer.Serialize(list));
        }
        finally { writes.Release(); }
    }

    public async Task DeleteAsync(string id)
    {
        await writes.WaitAsync();
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", TreeKey(id));
            var list = await ListAsync();
            list.RemoveAll(t => t.Id == id);
            await js.InvokeVoidAsync("localStorage.setItem", IndexKey, JsonSerializer.Serialize(list));
        }
        finally { writes.Release(); }
    }

    public ValueTask DownloadAsync(string fileName, string content) => js.InvokeVoidAsync("tocBuilder.download", fileName, content);
}

using System.Net.Http.Json;
using TocBuilder.Domain;

namespace TocBuilder.Web.Client;

public interface ISuggestionProvider
{
    Task<IReadOnlyList<Suggestion>> SuggestAsync(SuggestionApiRequest request);
}

/// <summary>Asks the host's Suggestion API. Nothing is sent until the user requests Suggestions.</summary>
public sealed class HttpSuggestionProvider(HttpClient http) : ISuggestionProvider
{
    public async Task<IReadOnlyList<Suggestion>> SuggestAsync(SuggestionApiRequest request)
    {
        using var response = await http.PostAsJsonAsync("api/suggestions", request);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                ? "Suggestions are not set up on this server."
                : $"The server answered {(int)response.StatusCode}: {(await response.Content.ReadAsStringAsync()).Trim('"')}");
        return await response.Content.ReadFromJsonAsync<Suggestion[]>() ?? [];
    }
}

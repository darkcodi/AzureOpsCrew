using System.Net.Http.Json;
using System.Text.Json;
using Front.Models;
using Microsoft.JSInterop;
using Serilog;

namespace Front.Services;

public class SettingsService(HttpClient http, IJSRuntime js)
{
    private const string StorageKey = "azureopscrew-settings";

    public async Task<List<Provider>> GetProvidersAsync()
    {
        try
        {
            var response = await http.GetAsync("/api/providers");
            if (!response.IsSuccessStatusCode) return [];
            var dtos = await response.Content.ReadFromJsonAsync<List<BackendProviderDto>>();
            if (dtos == null) return [];

            return dtos.Select(dto => new Provider
            {
                Id = dto.Id,
                BackendId = dto.Id,
                Name = dto.Name,
                ProviderType = ProviderTypeMap.FromBackend(dto.ProviderType),
                Status = dto.IsEnabled ? "enabled" : "disabled",
                ApiKey = "",
                HasApiKey = dto.HasApiKey,
                BaseUrl = dto.ApiEndpoint ?? "",
                DefaultModel = dto.DefaultModel ?? "",
                SelectedModels = ProviderTypeMap.SafeParseSelectedModels(dto.SelectedModels),
                Timeout = 30,
                RateLimit = 60,
                AvailableModels = [],
                IsDefault = false,
                DateCreated = dto.DateCreated,
            }).ToList();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to fetch providers from API");
            return [];
        }
    }

    public async Task<SaveProvidersResponse> SaveProvidersAsync(List<Provider> providers)
    {
        var results = new List<SavedProviderRef>();

        foreach (var p in providers)
        {
            var isEnabled = p.Status != "disabled";
            var selectedModelsJson = p.SelectedModels is { Count: > 0 }
                ? JsonSerializer.Serialize(p.SelectedModels)
                : null;

            if (!string.IsNullOrEmpty(p.BackendId))
            {
                var body = new
                {
                    name = p.Name,
                    apiKey = p.ApiKey,
                    apiEndpoint = string.IsNullOrEmpty(p.BaseUrl) ? (string?)null : p.BaseUrl,
                    defaultModel = string.IsNullOrEmpty(p.DefaultModel) ? (string?)null : p.DefaultModel,
                    selectedModels = selectedModelsJson,
                    isEnabled,
                };
                var res = await http.PutAsJsonAsync($"/api/providers/{p.BackendId}", body);
                if (!res.IsSuccessStatusCode)
                {
                    var text = await res.Content.ReadAsStringAsync();
                    throw new HttpRequestException(text.Length > 0 ? text : $"Failed to update provider {p.Name}");
                }
                results.Add(new SavedProviderRef { Id = p.Id, BackendId = p.BackendId });
            }
            else
            {
                var body = new
                {
                    name = p.Name,
                    providerType = ProviderTypeMap.ToBackend(p.ProviderType ?? p.Name),
                    apiKey = p.ApiKey,
                    apiEndpoint = string.IsNullOrEmpty(p.BaseUrl) ? (string?)null : p.BaseUrl,
                    defaultModel = string.IsNullOrEmpty(p.DefaultModel) ? (string?)null : p.DefaultModel,
                    selectedModels = selectedModelsJson,
                    isEnabled,
                };
                var res = await http.PostAsJsonAsync("/api/providers/create", body);
                if (!res.IsSuccessStatusCode)
                {
                    var text = await res.Content.ReadAsStringAsync();
                    throw new HttpRequestException(text.Length > 0 ? text : $"Failed to create provider {p.Name}");
                }
                var created = await res.Content.ReadFromJsonAsync<JsonElement>();
                var backendId = created.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                results.Add(new SavedProviderRef { Id = p.Id, BackendId = backendId });
            }
        }

        return new SaveProvidersResponse { Providers = results };
    }

    public async Task<ProviderTestResult> TestProviderAsync(Provider provider)
    {
        var payload = new
        {
            providerType = ProviderTypeMap.ToBackend(provider.ProviderType ?? provider.Name),
            apiKey = provider.ApiKey,
            providerId = provider.BackendId,
            apiEndpoint = provider.BaseUrl,
            defaultModel = provider.DefaultModel,
            name = provider.Name,
        };
        var response = await http.PostAsJsonAsync("/api/providers/test", payload);
        var result = await response.Content.ReadFromJsonAsync<ProviderTestResult>();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(result?.Message ?? "Could not test connection");
        return result ?? new ProviderTestResult();
    }

    public async Task RemoveProviderAsync(string backendId)
    {
        var response = await http.DeleteAsync($"/api/providers/{backendId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<SettingsState> LoadFromLocalStorageAsync()
    {
        try
        {
            var raw = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(raw)) return DefaultSettings.Create();
            var loaded = JsonSerializer.Deserialize<SettingsState>(raw, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            return loaded ?? DefaultSettings.Create();
        }
        catch
        {
            return DefaultSettings.Create();
        }
    }

    public async Task SaveToLocalStorageAsync(SettingsState state)
    {
        try
        {
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to persist settings to localStorage");
        }
    }
}

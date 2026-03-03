using Serilog;

namespace AzureOpsCrew.Api.Settings;

/// <summary>
/// Loads and provides the service registry from YAML.
/// The service registry maps services to their Azure resources, monitoring, DevOps, and docs.
/// Agents use this for knowing WHERE to look when investigating issues.
/// </summary>
public class ServiceRegistryProvider
{
    private readonly List<ServiceEntry> _services = new();
    private bool _loaded;

    /// <summary>
    /// Static factory: loads the service registry from the YAML file at startup.
    /// Gracefully handles missing file or parse errors.
    /// </summary>
    public static ServiceRegistryProvider Load(string yamlFilePath)
    {
        var provider = new ServiceRegistryProvider();

        try
        {
            if (!File.Exists(yamlFilePath))
            {
                Log.Warning("Service registry file not found at {Path}, running without service map", yamlFilePath);
                provider._loaded = true;
                return provider;
            }

            var yaml = File.ReadAllText(yamlFilePath);
            provider.ParseSimpleYaml(yaml);
            provider._loaded = true;
            Log.Information("Loaded {Count} services from registry {Path}", provider._services.Count, yamlFilePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load service registry from {Path}, running without service map", yamlFilePath);
            provider._loaded = true;
        }

        return provider;
    }

    /// <summary>
    /// Returns all registered services.
    /// </summary>
    public IReadOnlyList<ServiceEntry> GetAllServices() => _services;

    /// <summary>
    /// Finds services matching a query (name, description, resource name).
    /// </summary>
    public IReadOnlyList<ServiceEntry> FindServices(string query)
    {
        var q = query.ToLowerInvariant();
        return _services.Where(s =>
            s.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            s.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            s.ResourceNames.Any(r => r.Contains(q, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }

    /// <summary>
    /// Returns a compact text summary of the service registry for injection into agent prompts.
    /// </summary>
    public string GetRegistrySummaryForPrompt()
    {
        if (_services.Count == 0)
            return "No service registry loaded. Ask the user to identify the specific service and resources.";

        var lines = new List<string> { "=== SERVICE REGISTRY ===" };
        foreach (var svc in _services)
        {
            lines.Add($"• {svc.Name} ({svc.Environment}): {svc.Description}");
            lines.Add($"  Resource Group: {svc.ResourceGroup}");
            foreach (var r in svc.Resources)
                lines.Add($"  - {r.Name} [{r.Type}] ({r.Role})");
            if (!string.IsNullOrEmpty(svc.LogsWorkspace))
                lines.Add($"  Logs: {svc.LogsWorkspace}");
            if (!string.IsNullOrEmpty(svc.AppInsights))
                lines.Add($"  AppInsights: {svc.AppInsights}");
            if (!string.IsNullOrEmpty(svc.Pipeline))
                lines.Add($"  Pipeline: {svc.Pipeline} (repo: {svc.Repo})");
            if (svc.Runbooks.Count > 0)
                lines.Add($"  Runbooks: {string.Join("; ", svc.Runbooks)}");
        }
        lines.Add("=== END SERVICE REGISTRY ===");
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Simple line-based YAML parser (avoids dependency on YamlDotNet).
    /// Handles the flat structure of service-registry.yaml.
    /// </summary>
    private void ParseSimpleYaml(string yaml)
    {
        ServiceEntry? current = null;
        ServiceResource? currentResource = null;
        var section = "";

        foreach (var rawLine in yaml.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                continue;

            var trimmed = line.TrimStart();
            var indent = line.Length - trimmed.Length;

            // New service entry
            if (trimmed.StartsWith("- name:") && indent <= 4)
            {
                current = new ServiceEntry { Name = ExtractValue(trimmed, "- name:") };
                _services.Add(current);
                currentResource = null;
                section = "";
                continue;
            }

            if (current is null) continue;

            if (trimmed.StartsWith("description:"))
            {
                current.Description = ExtractQuotedValue(trimmed, "description:");
                continue;
            }
            if (trimmed.StartsWith("environment:"))
            {
                current.Environment = ExtractValue(trimmed, "environment:");
                continue;
            }
            if (trimmed == "azure:")
            {
                section = "azure";
                continue;
            }
            if (trimmed == "observability:")
            {
                section = "observability";
                continue;
            }
            if (trimmed == "devops:")
            {
                section = "devops";
                continue;
            }
            if (trimmed == "docs:")
            {
                section = "docs";
                continue;
            }
            if (trimmed == "owners:")
            {
                section = "owners";
                continue;
            }
            if (trimmed == "resources:")
            {
                continue;
            }
            if (trimmed == "runbooks:")
            {
                continue;
            }
            if (trimmed == "key_metrics:")
            {
                continue;
            }

            // Azure section
            if (section == "azure")
            {
                if (trimmed.StartsWith("subscription:"))
                    current.Subscription = ExtractQuotedValue(trimmed, "subscription:");
                else if (trimmed.StartsWith("resource_group:"))
                    current.ResourceGroup = ExtractQuotedValue(trimmed, "resource_group:");
                else if (trimmed.StartsWith("- name:"))
                {
                    currentResource = new ServiceResource { Name = ExtractValue(trimmed, "- name:") };
                    current.Resources.Add(currentResource);
                }
                else if (currentResource is not null)
                {
                    if (trimmed.StartsWith("type:"))
                        currentResource.Type = ExtractValue(trimmed, "type:");
                    else if (trimmed.StartsWith("role:"))
                        currentResource.Role = ExtractValue(trimmed, "role:");
                }
            }
            // Observability section
            else if (section == "observability")
            {
                if (trimmed.StartsWith("logs_workspace:"))
                    current.LogsWorkspace = ExtractValue(trimmed, "logs_workspace:");
                else if (trimmed.StartsWith("app_insights:"))
                    current.AppInsights = ExtractValue(trimmed, "app_insights:");
                else if (trimmed.StartsWith("- "))
                    current.KeyMetrics.Add(ExtractQuotedValue(trimmed, "- "));
            }
            // DevOps section
            else if (section == "devops")
            {
                if (trimmed.StartsWith("project:"))
                    current.Project = ExtractValue(trimmed, "project:");
                else if (trimmed.StartsWith("repo:"))
                    current.Repo = ExtractValue(trimmed, "repo:");
                else if (trimmed.StartsWith("pipeline:"))
                    current.Pipeline = ExtractValue(trimmed, "pipeline:");
                else if (trimmed.StartsWith("branch_strategy:"))
                    current.BranchStrategy = ExtractValue(trimmed, "branch_strategy:");
            }
            // Docs section
            else if (section == "docs")
            {
                if (trimmed.StartsWith("- "))
                    current.Runbooks.Add(ExtractQuotedValue(trimmed, "- "));
                else if (trimmed.StartsWith("architecture:"))
                    current.Architecture = ExtractQuotedValue(trimmed, "architecture:");
            }
        }
    }

    private static string ExtractValue(string line, string prefix)
    {
        return line[prefix.Length..].Trim().Trim('"').Trim('\'');
    }

    private static string ExtractQuotedValue(string line, string prefix)
    {
        return line[prefix.Length..].Trim().Trim('"').Trim('\'');
    }
}

public class ServiceEntry
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Environment { get; set; } = "";
    public string Subscription { get; set; } = "";
    public string ResourceGroup { get; set; } = "";
    public List<ServiceResource> Resources { get; set; } = new();
    public string LogsWorkspace { get; set; } = "";
    public string AppInsights { get; set; } = "";
    public List<string> KeyMetrics { get; set; } = new();
    public string Project { get; set; } = "";
    public string Repo { get; set; } = "";
    public string Pipeline { get; set; } = "";
    public string BranchStrategy { get; set; } = "";
    public List<string> Runbooks { get; set; } = new();
    public string Architecture { get; set; } = "";

    public IEnumerable<string> ResourceNames => Resources.Select(r => r.Name);
}

public class ServiceResource
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Role { get; set; } = "";
}

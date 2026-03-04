namespace AzureOpsCrew.Api.Setup.Seeds
{
    public record SeederOptions
    {
        public bool IsEnabled { get; set; }

        public string? OpenAiApiKey { get; set; }
        
        public string? AnthropicApiKey { get; set; }
        
        /// <summary>
        /// Default model for agents. Defaults to claude-opus-4-6 for extended thinking capabilities.
        /// </summary>
        public string DefaultModel { get; set; } = "claude-opus-4-6";
    }
}

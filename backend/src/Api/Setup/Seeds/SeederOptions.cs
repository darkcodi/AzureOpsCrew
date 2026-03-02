namespace AzureOpsCrew.Api.Setup.Seeds
{
    public record SeederOptions
    {
        public bool IsEnabled { get; set; }

        public string? OpenAiApiKey { get; set; }
    }
}

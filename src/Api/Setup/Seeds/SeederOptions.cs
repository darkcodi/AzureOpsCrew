namespace AzureOpsCrew.Api.Setup.Seeds
{
    public record SeederOptions
    {
        public bool IsEnabled { get; set; }

        public ProviderSeedData AzureFoundrySeed { get; set;}
    }

    public record ProviderSeedData
    {
        public string ApiEndpoint { get; set;}

        public string Key { get; set; }
    }
}

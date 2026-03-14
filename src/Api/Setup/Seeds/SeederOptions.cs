namespace AzureOpsCrew.Api.Setup.Seeds
{
    public record SeederOptions
    {
        public bool IsEnabled { get; set; }

        public ProviderSeedData ProviderSeed { get; set;}

        public UserSeedData UserSeed { get; set; }
    }

    public record ProviderSeedData
    {
        public string ProviderType { get; set; } = "AzureFoundry";

        public string ApiEndpoint { get; set;}

        public string Key { get; set; }

        public string DefaultModel { get; set; }
    }

    public record UserSeedData
    {
        public string Email { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }
    }
}

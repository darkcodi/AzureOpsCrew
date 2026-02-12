namespace AzureOpsCrew.Domain.Dimmies
{
    public class Dummy(Guid Id, string Name)
    {
        public Guid Id { get; } = Id;

        public string Name { get; set; } = Name;

        public string? Description { get; set;}
    }
}

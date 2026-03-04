using System.ComponentModel.DataAnnotations;
using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Agents
{
    public sealed class UpdateAgentBodyDto
    {
        [Required]
        [StringLength(30, MinimumLength = 2)]
        [RegularExpression(@"^[a-z0-9]+$", ErrorMessage = "Username must contain only lowercase letters and numbers.")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(8000, MinimumLength = 1)]
        public string Prompt { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Model { get; set; } = string.Empty;

        public string? Description { get; set; }

        public AgentTool[] AvailableTools { get; set; } = [];

        public Guid ProviderId { get; set; }

        public string Color { get; set; } = "#43b581";

        public AgentInfo ToAgentInfo()
        {
            return new AgentInfo(Username, Prompt, Model)
            {
                Description = Description,
                AvailableTools = AvailableTools
            };
        }
    }
}

using Worker.Models.Content;

namespace Worker.Models;

public class FinalAnswer
{
    public string Text { get; set; } = string.Empty;
    public AocUsageContent Usage { get; set; } = new();
}

using AzureOpsCrew.Infrastructure.Ai.Models;
using AzureOpsCrew.Infrastructure.Ai.Models.Content;

namespace AzureOpsCrew.Api.Background;

public class ToolExecutor
{
    public async Task<AocFunctionResultContent> ExecuteTool(ToolDeclaration toolDeclaration, AocFunctionCallContent toolCall)
    {
        throw new NotImplementedException();
    }
}

using Worker.Models;

namespace Worker.Tools;

public static class FrontEndTools
{
    public static List<ToolDeclaration> GetDeclarations()
    {
        return new List<ToolDeclaration>() { IpTool() };
    }

    private static ToolDeclaration IpTool()
    {
        throw new NotImplementedException();
    }
}

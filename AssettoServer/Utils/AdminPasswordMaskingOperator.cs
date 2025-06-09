using System.Text.RegularExpressions;
using Serilog.Enrichers.Sensitive;

namespace AssettoServer.Utils;

public partial class AdminPasswordMaskingOperator : IMaskingOperator
{
    [GeneratedRegex(@"(\/admin )(.*)")]
    private static partial Regex AdminPasswordRegex();
    
    public MaskingResult Mask(string input, string mask)
    {
        var result = AdminPasswordRegex().Replace(input, "$1********");

        return new MaskingResult
        {
            Result = result,
            Match = result != input
        };
    }
}

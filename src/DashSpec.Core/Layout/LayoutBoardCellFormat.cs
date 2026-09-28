namespace DashSpec.Core.Layout;

/// <summary>Bracket cell token formatting (ref and optional :weight).</summary>
public static class LayoutBoardCellFormat
{
    public static string RefToken(string cellToken)
    {
        if (string.IsNullOrWhiteSpace(cellToken))
        {
            return cellToken;
        }

        var separator = cellToken.IndexOf(':');
        return separator > 0 ? cellToken[..separator] : cellToken;
    }
}

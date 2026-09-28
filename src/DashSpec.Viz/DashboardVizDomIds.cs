namespace DashSpec.Viz;

public static class DashboardVizDomIds
{
    public static string ChartDomId(string cardId, bool detailView = false) =>
        "chart-" + DomIdHash(detailView ? cardId + ":detail" : cardId);

    public static string MatrixHostDomId(string cardId, bool detailView = false) =>
        "matrix-host-" + DomIdHash(detailView ? cardId + ":detail" : cardId);

    public static string MatrixCanvasDomId(string cardId, bool detailView = false) =>
        "matrix-canvas-" + DomIdHash(detailView ? cardId + ":detail" : cardId);

    private static string DomIdHash(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value)))[..12];
}

using System.Text;

namespace DocNative.Core.Validation;

/// <summary>
/// Reglas puras para detectar operaciones intercaladas a partir del código por página.
/// </summary>
internal static class InterleavedPdfAnalyzer
{
    public static Dictionary<int, string> ForwardFill(IReadOnlyDictionary<int, string> detected, int totalPages)
    {
        var result = new Dictionary<int, string>(detected);
        string? lastCode = null;

        for (var page = 1; page <= totalPages; page++)
        {
            if (result.TryGetValue(page, out var code))
            {
                lastCode = code;
                continue;
            }

            if (lastCode != null)
            {
                result[page] = lastCode;
            }
        }

        return result;
    }

    public static InterleavedCheckResult Check(IReadOnlyDictionary<int, string> codigoPorPagina, int totalPages)
    {
        if (totalPages <= 0 || codigoPorPagina.Count < 2)
        {
            return InterleavedCheckResult.Ok();
        }

        var pagesByCode = codigoPorPagina
            .GroupBy(item => item.Value, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Key).OrderBy(page => page).ToArray(),
                StringComparer.Ordinal);

        if (pagesByCode.Count < 2)
        {
            return InterleavedCheckResult.Ok();
        }

        foreach (var pages in pagesByCode.Values)
        {
            if (HasNonContiguousBlock(pages))
            {
                return InterleavedCheckResult.Fail(BuildMessage(pagesByCode), pagesByCode);
            }
        }

        if (HasAlternatingPattern(codigoPorPagina, totalPages))
        {
            return InterleavedCheckResult.Fail(BuildMessage(pagesByCode), pagesByCode);
        }

        return InterleavedCheckResult.Ok();
    }

    internal static bool HasNonContiguousBlock(IReadOnlyList<int> pages)
    {
        if (pages.Count <= 1)
        {
            return false;
        }

        for (var i = 1; i < pages.Count; i++)
        {
            if (pages[i] != pages[i - 1] + 1)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool HasAlternatingPattern(IReadOnlyDictionary<int, string> codigoPorPagina, int totalPages)
    {
        string? previousCode = null;
        var codesSeenSinceLastChange = new HashSet<string>(StringComparer.Ordinal);

        for (var page = 1; page <= totalPages; page++)
        {
            if (!codigoPorPagina.TryGetValue(page, out var currentCode))
            {
                continue;
            }

            if (previousCode != null
                && !string.Equals(currentCode, previousCode, StringComparison.Ordinal)
                && codesSeenSinceLastChange.Contains(currentCode))
            {
                return true;
            }

            if (previousCode != null && !string.Equals(currentCode, previousCode, StringComparison.Ordinal))
            {
                codesSeenSinceLastChange.Add(previousCode);
            }

            previousCode = currentCode;
        }

        return false;
    }

    internal static string BuildMessage(IReadOnlyDictionary<string, int[]> pagesByCode)
    {
        var parts = pagesByCode
            .OrderBy(item => item.Value[0])
            .Select(item => $"{item.Key}: pág. {FormatPages(item.Value)}");

        return $"PDF mal ordenado: operaciones intercaladas ({string.Join("; ", parts)}). Re-escanee separando cada pagaré.";
    }

    private static string FormatPages(IReadOnlyList<int> pages)
    {
        if (pages.Count == 0)
        {
            return string.Empty;
        }

        var ranges = new List<string>();
        var start = pages[0];
        var end = pages[0];

        for (var i = 1; i < pages.Count; i++)
        {
            if (pages[i] == end + 1)
            {
                end = pages[i];
                continue;
            }

            ranges.Add(start == end ? start.ToString() : $"{start}-{end}");
            start = pages[i];
            end = pages[i];
        }

        ranges.Add(start == end ? start.ToString() : $"{start}-{end}");
        return string.Join(",", ranges);
    }
}

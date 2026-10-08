using Bookennis.Shared.Utils.Sorting;

namespace Bookennis.Client.Utils.Sorting;

public static class SortParametersExtension
{
    private const string ParameterName = "sort";

    public static IEnumerable<KeyValuePair<string, string>> GetQueryParameters(this SortParameters? sortParameters)
    {
        if (sortParameters == null)
            yield break;

        foreach (var sortParameter in sortParameters)
        {
            yield return new KeyValuePair<string, string>(ParameterName, $"{sortParameter.Key}_{sortParameter.Value}");
        }
    }

    public static void AddSortParameters(this Dictionary<string, string?> queryParameters, SortParameters? sortParameters)
    {
        foreach (var queryParameter in sortParameters.GetQueryParameters())
        {
            queryParameters.Add(queryParameter.Key, queryParameter.Value);
        }
    }
}
using Bookennis.Shared.Utils.Sorting;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bookennis.Api.Infrastructure.Utils.Sorting;

public class SortParametersModelBinder : IModelBinder
{
    private const string ParameterName = "sort";

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var modelName = bindingContext.OriginalModelName;
        var valueProviderResult = bindingContext.ValueProvider.GetValue(ParameterName);

        if (valueProviderResult == ValueProviderResult.None)
            return Task.CompletedTask;

        var result = new SortParameters();
        foreach (var item in valueProviderResult)
        {
            var args = item.Split('_');

            if (args.Length == 1)
            {
                result.TryAdd(args[0], SortDirection.Ascending);
            }
            else if (args.Length == 2)
            {
                var sortDirection = args[1].ToLowerInvariant() is "desc" or "descending"
                    ? SortDirection.Descending
                    : SortDirection.Ascending;

                result.TryAdd(args[0], sortDirection);
            }
            else
            {
                throw new ArgumentException("Sorting argument invalid");
            }
        }

        bindingContext.Result = ModelBindingResult.Success(result);

        return Task.CompletedTask;
    }
}
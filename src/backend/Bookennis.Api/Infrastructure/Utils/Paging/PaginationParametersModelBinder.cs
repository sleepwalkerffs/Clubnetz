using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bookennis.Api.Infrastructure.Utils.Paging;

public class PaginationParametersModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var pageValue = bindingContext.ValueProvider.GetValue(nameof(PaginationParameters.Page));
        var pageSizeValue = bindingContext.ValueProvider.GetValue(nameof(PaginationParameters.PageSize));
        var enablePagingValue = bindingContext.ValueProvider.GetValue(nameof(PaginationParameters.EnablePaging));

        if (pageValue.Length > 1)
            throw new ArgumentException("Can't contain more than one value for page");

        if (pageSizeValue.Length > 1)
            throw new ArgumentException("Can't contain more than one value for pageSize");

        if (enablePagingValue.Length > 1)
            throw new ArgumentException("Can't contain more than one value for enablePaging");

        var page = PagingDefaults.Page;
        var pageSize = PagingDefaults.PageSize;
        var enablePaging = true;

        if (pageValue != ValueProviderResult.None && !int.TryParse(pageValue.FirstValue, out page))
            throw new ArgumentException("");

        if (pageSizeValue != ValueProviderResult.None && !int.TryParse(pageSizeValue.FirstValue, out pageSize))
            throw new ArgumentException("");

        if (enablePagingValue != ValueProviderResult.None && !bool.TryParse(enablePagingValue.FirstValue, out enablePaging))
            throw new ArgumentException("");

        var result = new PaginationParameters
        {
            Page = page,
            PageSize = pageSize,
            EnablePaging = enablePaging
        };

        bindingContext.Result = ModelBindingResult.Success(result);

        return Task.CompletedTask;
    }
}
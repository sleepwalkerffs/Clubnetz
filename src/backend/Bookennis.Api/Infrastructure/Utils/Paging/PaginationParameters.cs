using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Infrastructure.Utils.Paging;

[ModelBinder(BinderType = typeof(PaginationParametersModelBinder))]
public class PaginationParameters : Bookennis.Shared.Utils.Paging.PaginationParameters;
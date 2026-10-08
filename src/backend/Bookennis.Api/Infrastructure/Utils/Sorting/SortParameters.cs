using Microsoft.AspNetCore.Mvc;

namespace Bookennis.Api.Infrastructure.Utils.Sorting;

[ModelBinder(BinderType = typeof(SortParametersModelBinder))]
public class SortParameters : Bookennis.Shared.Utils.Sorting.SortParameters
{ }
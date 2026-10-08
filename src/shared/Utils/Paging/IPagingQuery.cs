namespace Bookennis.Shared.Utils.Paging;

public interface IPagingQuery
{
    public int Page { get; }
    public int PageSize { get; }
    public bool EnablePaging { get; }
}
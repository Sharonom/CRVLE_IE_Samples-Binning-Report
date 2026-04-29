using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamplesBucketing.Web.Models;
using SamplesBucketing.Web.Services;

namespace SamplesBucketing.Web.Pages;

public class VpoListModel : PageModel
{
    private readonly IVpoListService _listService;

    public VpoListModel(IVpoListService listService) => _listService = listService;

    [BindProperty(SupportsGet = true)]
    public string Search { get; set; } = "";

    public IReadOnlyList<VpoListRow> Vpos    { get; private set; } = Array.Empty<VpoListRow>();
    public string? ErrorMessage               { get; private set; }
    public bool    HasSearched                { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Only query when the user has submitted a search term (or explicitly hit Search)
        if (Request.Query.ContainsKey(nameof(Search)))
        {
            HasSearched = true;
            try
            {
                Vpos = await _listService.SearchAsync(Search.Trim(), maxRows: 200, ct: ct);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Database error: {ex.Message}";
            }
        }
    }
}

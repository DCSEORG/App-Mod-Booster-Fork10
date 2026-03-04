using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ExpenseManagement.Pages;

public class NewExpenseModel : PageModel
{
    private readonly IExpenseService _svc;
    public NewExpenseModel(IExpenseService svc) => _svc = svc;

    [BindProperty]
    public ExpenseDto Input { get; set; } = new() { ExpenseDate = DateTime.UtcNow.Date };

    public List<SelectListItem> CategoryOptions { get; private set; } = new();

    public string? SuccessMessage { get; private set; }

    private async Task LoadCategoriesAsync()
    {
        var (cats, error) = await _svc.GetCategoriesAsync();
        CategoryOptions = cats
            .Select(c => new SelectListItem(c.CategoryName, c.CategoryId.ToString()))
            .ToList();
        if (error != null) ViewData["ErrorMessage"] = error;
    }

    public async Task OnGetAsync()
    {
        await LoadCategoriesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCategoriesAsync();

        if (!ModelState.IsValid) return Page();

        var (expense, error) = await _svc.CreateExpenseAsync(Input);

        if (error != null) ViewData["ErrorMessage"] = error;

        if (expense != null)
        {
            SuccessMessage = $"Expense £{(Input.Amount):F2} created successfully (ID: {expense.ExpenseId}).";
            // Reset form
            Input = new ExpenseDto { ExpenseDate = DateTime.UtcNow.Date };
            ModelState.Clear();
        }
        else if (error == null)
        {
            ModelState.AddModelError(string.Empty, "Failed to create expense. Please try again.");
        }

        return Page();
    }
}

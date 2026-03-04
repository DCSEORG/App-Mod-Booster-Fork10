using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ExpensesModel : PageModel
{
    private readonly IExpenseService _svc;
    public ExpensesModel(IExpenseService svc) => _svc = svc;

    [BindProperty(SupportsGet = true)]
    public string? Filter { get; set; }

    public List<Expense> Expenses { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var (expenses, error) = await _svc.GetExpensesByFilterAsync(Filter);
        Expenses = expenses;
        if (error != null) ViewData["ErrorMessage"] = error;
    }
}

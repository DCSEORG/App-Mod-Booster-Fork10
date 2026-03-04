using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly IExpenseService _svc;
    public IndexModel(IExpenseService svc) => _svc = svc;

    public DashboardStats Stats { get; private set; } = new();
    public List<Expense> RecentExpenses { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var (stats, statsError) = await _svc.GetDashboardStatsAsync();
        var (expenses, expError) = await _svc.GetAllExpensesAsync();

        Stats = stats;
        RecentExpenses = expenses.OrderByDescending(e => e.CreatedAt).Take(10).ToList();

        var error = statsError ?? expError;
        if (error != null) ViewData["ErrorMessage"] = error;
    }
}

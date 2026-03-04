using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ApprovalsModel : PageModel
{
    private readonly IExpenseService _svc;
    public ApprovalsModel(IExpenseService svc) => _svc = svc;

    public List<Expense> PendingExpenses { get; private set; } = new();
    public string? ActionMessage { get; private set; }

    public async Task OnGetAsync()
    {
        var (expenses, error) = await _svc.GetPendingExpensesAsync();
        PendingExpenses = expenses;
        if (error != null) ViewData["ErrorMessage"] = error;
    }

    public async Task<IActionResult> OnPostApproveAsync(int expenseId)
    {
        // Manager ID = 2 (Bob Manager) for this demo
        var (success, error) = await _svc.ApproveExpenseAsync(expenseId, reviewedById: 2);
        if (error != null) ViewData["ErrorMessage"] = error;
        ActionMessage = success
            ? $"Expense #{expenseId} has been approved."
            : $"Could not approve expense #{expenseId}.";

        var (expenses, listError) = await _svc.GetPendingExpensesAsync();
        PendingExpenses = expenses;
        if (listError != null) ViewData["ErrorMessage"] = listError;
        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(int expenseId)
    {
        var (success, error) = await _svc.RejectExpenseAsync(expenseId, reviewedById: 2);
        if (error != null) ViewData["ErrorMessage"] = error;
        ActionMessage = success
            ? $"Expense #{expenseId} has been rejected."
            : $"Could not reject expense #{expenseId}.";

        var (expenses, listError) = await _svc.GetPendingExpensesAsync();
        PendingExpenses = expenses;
        if (listError != null) ViewData["ErrorMessage"] = listError;
        return Page();
    }
}

using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<(List<Expense> Expenses, string? ErrorMessage)> GetAllExpensesAsync();
    Task<(Expense? Expense, string? ErrorMessage)> GetExpenseByIdAsync(int id);
    Task<(Expense? Expense, string? ErrorMessage)> CreateExpenseAsync(ExpenseDto dto);
    Task<(bool Success, string? ErrorMessage)> SubmitExpenseAsync(int expenseId);
    Task<(bool Success, string? ErrorMessage)> ApproveExpenseAsync(int expenseId, int reviewedById);
    Task<(bool Success, string? ErrorMessage)> RejectExpenseAsync(int expenseId, int reviewedById);
    Task<(List<Expense> Expenses, string? ErrorMessage)> GetPendingExpensesAsync();
    Task<(DashboardStats Stats, string? ErrorMessage)> GetDashboardStatsAsync();
    Task<(List<Expense> Expenses, string? ErrorMessage)> GetExpensesByFilterAsync(string? filterText);
    Task<(List<ExpenseCategory> Categories, string? ErrorMessage)> GetCategoriesAsync();
}

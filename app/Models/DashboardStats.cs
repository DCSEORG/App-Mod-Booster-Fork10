namespace ExpenseManagement.Models;

public class DashboardStats
{
    public int TotalExpenses { get; set; }
    public int PendingApprovals { get; set; }

    /// <summary>Total approved amount in pence.</summary>
    public int ApprovedAmountMinor { get; set; }

    public int ApprovedCount { get; set; }

    public decimal ApprovedAmountPounds => ApprovedAmountMinor / 100m;
    public string ApprovedAmountFormatted => $"£{ApprovedAmountPounds:F2}";
}

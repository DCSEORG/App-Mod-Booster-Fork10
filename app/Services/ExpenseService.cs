using System.Data;
using System.Runtime.CompilerServices;
using ExpenseManagement.Models;
using Microsoft.Data.SqlClient;

namespace ExpenseManagement.Services;

public class ExpenseService : IExpenseService
{
    private readonly IConfiguration _config;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(IConfiguration config, ILogger<ExpenseService> logger)
    {
        _config = config;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Connection helper
    // ─────────────────────────────────────────────────────────────────────────

    private string ConnectionString =>
        _config.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection is not configured.");

    private SqlConnection CreateConnection() => new SqlConnection(ConnectionString);

    // ─────────────────────────────────────────────────────────────────────────
    // Error helper – formats a user-friendly message with file + line info
    // ─────────────────────────────────────────────────────────────────────────

    private static string BuildErrorMessage(
        Exception ex,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int line = 0)
    {
        var fileName = Path.GetFileName(filePath);
        var baseMsg = $"Database error in {fileName} (line {line}): {ex.Message}";

        if (ex.Message.Contains("Managed Identity", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("AADSTS", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("DefaultAzureCredential", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("ManagedIdentityCredential", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("CredentialUnavailableException", StringComparison.OrdinalIgnoreCase))
        {
            baseMsg += " | Managed Identity authentication failed. Fix: " +
                       "1) Ensure the App Service has a user-assigned managed identity assigned. " +
                       "2) Set AZURE_CLIENT_ID app setting to the managed identity client ID. " +
                       "3) Run 'az login' for local development (appsettings.Development.json uses " +
                       "'Authentication=Active Directory Default' which triggers az login).";
        }

        return baseMsg;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Dummy-data fallbacks (used whenever DB is unavailable)
    // ─────────────────────────────────────────────────────────────────────────

    private static List<Expense> GetDummyExpenses() => new()
    {
        new Expense
        {
            ExpenseId = 1, UserId = 1, UserName = "Alice Example",
            CategoryId = 1, CategoryName = "Travel",
            StatusId = 2, StatusName = "Submitted",
            AmountMinor = 2540, Currency = "GBP",
            ExpenseDate = DateTime.UtcNow.AddDays(-14),
            Description = "Taxi from airport to client site",
            SubmittedAt = DateTime.UtcNow.AddDays(-13),
            CreatedAt = DateTime.UtcNow.AddDays(-14)
        },
        new Expense
        {
            ExpenseId = 2, UserId = 1, UserName = "Alice Example",
            CategoryId = 2, CategoryName = "Meals",
            StatusId = 3, StatusName = "Approved",
            AmountMinor = 1425, Currency = "GBP",
            ExpenseDate = DateTime.UtcNow.AddDays(-45),
            Description = "Client lunch meeting",
            SubmittedAt = DateTime.UtcNow.AddDays(-44),
            ReviewedBy = 2, ReviewedByName = "Bob Manager",
            ReviewedAt = DateTime.UtcNow.AddDays(-43),
            CreatedAt = DateTime.UtcNow.AddDays(-45)
        },
        new Expense
        {
            ExpenseId = 3, UserId = 1, UserName = "Alice Example",
            CategoryId = 3, CategoryName = "Supplies",
            StatusId = 1, StatusName = "Draft",
            AmountMinor = 799, Currency = "GBP",
            ExpenseDate = DateTime.UtcNow.AddDays(-3),
            Description = "Office stationery",
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        },
        new Expense
        {
            ExpenseId = 4, UserId = 1, UserName = "Alice Example",
            CategoryId = 4, CategoryName = "Accommodation",
            StatusId = 3, StatusName = "Approved",
            AmountMinor = 12300, Currency = "GBP",
            ExpenseDate = DateTime.UtcNow.AddDays(-90),
            Description = "Hotel during client visit",
            SubmittedAt = DateTime.UtcNow.AddDays(-89),
            ReviewedBy = 2, ReviewedByName = "Bob Manager",
            ReviewedAt = DateTime.UtcNow.AddDays(-88),
            CreatedAt = DateTime.UtcNow.AddDays(-90)
        },
        new Expense
        {
            ExpenseId = 5, UserId = 1, UserName = "Alice Example",
            CategoryId = 5, CategoryName = "Other",
            StatusId = 4, StatusName = "Rejected",
            AmountMinor = 5000, Currency = "GBP",
            ExpenseDate = DateTime.UtcNow.AddDays(-30),
            Description = "Personal equipment (not eligible)",
            SubmittedAt = DateTime.UtcNow.AddDays(-29),
            ReviewedBy = 2, ReviewedByName = "Bob Manager",
            ReviewedAt = DateTime.UtcNow.AddDays(-28),
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        }
    };

    private static DashboardStats GetDummyStats() => new()
    {
        TotalExpenses = 5,
        PendingApprovals = 1,
        ApprovedAmountMinor = 13725,
        ApprovedCount = 2
    };

    private static List<ExpenseCategory> GetDummyCategories() => new()
    {
        new ExpenseCategory { CategoryId = 1, CategoryName = "Travel", IsActive = true },
        new ExpenseCategory { CategoryId = 2, CategoryName = "Meals", IsActive = true },
        new ExpenseCategory { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
        new ExpenseCategory { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
        new ExpenseCategory { CategoryId = 5, CategoryName = "Other", IsActive = true }
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Column-name constants (avoids magic strings scattered through reader code)
    // ─────────────────────────────────────────────────────────────────────────

    private static class StatCol
    {
        public const string TotalExpenses       = "TotalExpenses";
        public const string PendingApprovals    = "PendingApprovals";
        public const string ApprovedAmountMinor = "ApprovedAmountMinor";
        public const string ApprovedCount       = "ApprovedCount";
    }

    private static class Col
    {
        public const string ExpenseId      = "ExpenseId";
        public const string UserId         = "UserId";
        public const string UserName       = "UserName";
        public const string UserEmail      = "UserEmail";
        public const string CategoryId     = "CategoryId";
        public const string CategoryName   = "CategoryName";
        public const string StatusId       = "StatusId";
        public const string StatusName     = "StatusName";
        public const string AmountMinor    = "AmountMinor";
        public const string Currency       = "Currency";
        public const string ExpenseDate    = "ExpenseDate";
        public const string Description    = "Description";
        public const string ReceiptFile    = "ReceiptFile";
        public const string SubmittedAt    = "SubmittedAt";
        public const string ReviewedBy     = "ReviewedBy";
        public const string ReviewedByName = "ReviewedByName";
        public const string ReviewedAt     = "ReviewedAt";
        public const string CreatedAt      = "CreatedAt";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Map SqlDataReader → Expense
    // ─────────────────────────────────────────────────────────────────────────

    private static Expense MapExpense(SqlDataReader r) => new()
    {
        ExpenseId     = r.GetInt32(Col.ExpenseId),
        UserId        = r.GetInt32(Col.UserId),
        UserName      = r.IsDBNull(Col.UserName)      ? string.Empty : r.GetString(Col.UserName),
        UserEmail     = r.IsDBNull(Col.UserEmail)     ? string.Empty : r.GetString(Col.UserEmail),
        CategoryId    = r.GetInt32(Col.CategoryId),
        CategoryName  = r.IsDBNull(Col.CategoryName)  ? string.Empty : r.GetString(Col.CategoryName),
        StatusId      = r.GetInt32(Col.StatusId),
        StatusName    = r.IsDBNull(Col.StatusName)    ? string.Empty : r.GetString(Col.StatusName),
        AmountMinor   = r.GetInt32(Col.AmountMinor),
        Currency      = r.IsDBNull(Col.Currency)      ? "GBP"        : r.GetString(Col.Currency),
        ExpenseDate   = r.GetDateTime(Col.ExpenseDate),
        Description   = r.IsDBNull(Col.Description)   ? null : r.GetString(Col.Description),
        ReceiptFile   = r.IsDBNull(Col.ReceiptFile)   ? null : r.GetString(Col.ReceiptFile),
        SubmittedAt   = r.IsDBNull(Col.SubmittedAt)   ? null : r.GetDateTime(Col.SubmittedAt),
        ReviewedBy    = r.IsDBNull(Col.ReviewedBy)    ? null : r.GetInt32(Col.ReviewedBy),
        ReviewedByName= r.IsDBNull(Col.ReviewedByName)? null : r.GetString(Col.ReviewedByName),
        ReviewedAt    = r.IsDBNull(Col.ReviewedAt)    ? null : r.GetDateTime(Col.ReviewedAt),
        CreatedAt     = r.GetDateTime(Col.CreatedAt)
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<(List<Expense> Expenses, string? ErrorMessage)> GetAllExpensesAsync()
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("usp_GetAllExpenses", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            var list = new List<Expense>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapExpense(reader));
            return (list, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAllExpenses failed");
            return (GetDummyExpenses(), BuildErrorMessage(ex));
        }
    }

    public async Task<(Expense? Expense, string? ErrorMessage)> GetExpenseByIdAsync(int id)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("usp_GetExpenseById", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId", id);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync()) return (MapExpense(reader), null);
            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetExpenseById failed");
            var dummy = GetDummyExpenses().FirstOrDefault(e => e.ExpenseId == id);
            return (dummy, BuildErrorMessage(ex));
        }
    }

    public async Task<(Expense? Expense, string? ErrorMessage)> CreateExpenseAsync(ExpenseDto dto)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("usp_CreateExpense", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@UserId",      dto.UserId);
            cmd.Parameters.AddWithValue("@CategoryId",  dto.CategoryId);
            cmd.Parameters.AddWithValue("@AmountMinor", (int)(dto.Amount * 100));
            cmd.Parameters.AddWithValue("@ExpenseDate", dto.ExpenseDate);
            cmd.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync()) return (MapExpense(reader), null);
            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateExpense failed");
            // Return a dummy created expense so UI still renders
            var dummy = new Expense
            {
                ExpenseId   = 99,
                UserId      = dto.UserId,
                UserName    = "Demo User",
                CategoryId  = dto.CategoryId,
                CategoryName = GetDummyCategories().FirstOrDefault(c => c.CategoryId == dto.CategoryId)?.CategoryName ?? "Unknown",
                StatusId    = 1,
                StatusName  = "Draft",
                AmountMinor = (int)(dto.Amount * 100),
                Currency    = "GBP",
                ExpenseDate = dto.ExpenseDate,
                Description = dto.Description,
                CreatedAt   = DateTime.UtcNow
            };
            return (dummy, BuildErrorMessage(ex));
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("usp_SubmitExpense", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            await cmd.ExecuteNonQueryAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubmitExpense failed");
            return (false, BuildErrorMessage(ex));
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> ApproveExpenseAsync(int expenseId, int reviewedById)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("usp_ApproveExpense", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId",    expenseId);
            cmd.Parameters.AddWithValue("@ReviewedBy",   reviewedById);
            await cmd.ExecuteNonQueryAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ApproveExpense failed");
            return (false, BuildErrorMessage(ex));
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> RejectExpenseAsync(int expenseId, int reviewedById)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("usp_RejectExpense", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId",  expenseId);
            cmd.Parameters.AddWithValue("@ReviewedBy", reviewedById);
            await cmd.ExecuteNonQueryAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RejectExpense failed");
            return (false, BuildErrorMessage(ex));
        }
    }

    public async Task<(List<Expense> Expenses, string? ErrorMessage)> GetPendingExpensesAsync()
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("usp_GetPendingExpenses", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            var list = new List<Expense>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapExpense(reader));
            return (list, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPendingExpenses failed");
            var pending = GetDummyExpenses().Where(e => e.StatusName == "Submitted").ToList();
            return (pending, BuildErrorMessage(ex));
        }
    }

    public async Task<(DashboardStats Stats, string? ErrorMessage)> GetDashboardStatsAsync()
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("usp_GetDashboardStats", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var stats = new DashboardStats
                {
                    TotalExpenses       = reader.IsDBNull(StatCol.TotalExpenses)       ? 0 : reader.GetInt32(StatCol.TotalExpenses),
                    PendingApprovals    = reader.IsDBNull(StatCol.PendingApprovals)    ? 0 : reader.GetInt32(StatCol.PendingApprovals),
                    ApprovedAmountMinor = reader.IsDBNull(StatCol.ApprovedAmountMinor) ? 0 : reader.GetInt32(StatCol.ApprovedAmountMinor),
                    ApprovedCount       = reader.IsDBNull(StatCol.ApprovedCount)       ? 0 : reader.GetInt32(StatCol.ApprovedCount)
                };
                return (stats, null);
            }
            return (GetDummyStats(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDashboardStats failed");
            return (GetDummyStats(), BuildErrorMessage(ex));
        }
    }

    public async Task<(List<Expense> Expenses, string? ErrorMessage)> GetExpensesByFilterAsync(string? filterText)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("usp_GetExpensesByFilter", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@FilterText", (object?)filterText ?? DBNull.Value);
            var list = new List<Expense>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(MapExpense(reader));
            return (list, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetExpensesByFilter failed");
            var filtered = GetDummyExpenses()
                .Where(e => string.IsNullOrWhiteSpace(filterText)
                    || (e.Description?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false)
                    || e.CategoryName.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                    || e.StatusName.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                    || e.UserName.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return (filtered, BuildErrorMessage(ex));
        }
    }

    public async Task<(List<ExpenseCategory> Categories, string? ErrorMessage)> GetCategoriesAsync()
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT CategoryId, CategoryName, IsActive FROM dbo.ExpenseCategories WHERE IsActive = 1 ORDER BY CategoryName",
                conn);
            var list = new List<ExpenseCategory>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ExpenseCategory
                {
                    CategoryId   = reader.GetInt32("CategoryId"),
                    CategoryName = reader.GetString("CategoryName"),
                    IsActive     = reader.GetBoolean("IsActive")
                });
            }
            return (list, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCategories failed");
            return (GetDummyCategories(), BuildErrorMessage(ex));
        }
    }
}

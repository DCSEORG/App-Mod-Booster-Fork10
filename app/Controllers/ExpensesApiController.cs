using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Controllers;

/// <summary>
/// REST API for Expense Management.
/// All endpoints return JSON and use the ExpenseService which calls stored procedures.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class ExpensesApiController : ControllerBase
{
    private readonly IExpenseService _svc;

    public ExpensesApiController(IExpenseService svc) => _svc = svc;

    // ── GET /api/expenses ────────────────────────────────────────────────────

    /// <summary>Get all expenses with employee, category and status details.</summary>
    [HttpGet("expenses")]
    [ProducesResponseType(typeof(IEnumerable<Expense>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllExpenses()
    {
        var (expenses, error) = await _svc.GetAllExpensesAsync();
        return Ok(new { data = expenses, error });
    }

    // ── GET /api/expenses/pending ────────────────────────────────────────────

    /// <summary>Get expenses with status 'Submitted' awaiting manager approval.</summary>
    [HttpGet("expenses/pending")]
    [ProducesResponseType(typeof(IEnumerable<Expense>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingExpenses()
    {
        var (expenses, error) = await _svc.GetPendingExpensesAsync();
        return Ok(new { data = expenses, error });
    }

    // ── GET /api/expenses/{id} ───────────────────────────────────────────────

    /// <summary>Get a single expense by ID.</summary>
    [HttpGet("expenses/{id:int}")]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExpenseById(int id)
    {
        var (expense, error) = await _svc.GetExpenseByIdAsync(id);
        if (expense == null && error == null) return NotFound(new { message = $"Expense {id} not found." });
        return Ok(new { data = expense, error });
    }

    // ── POST /api/expenses ───────────────────────────────────────────────────

    /// <summary>Create a new expense (status starts as Draft).</summary>
    [HttpPost("expenses")]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateExpense([FromBody] ExpenseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var (expense, error) = await _svc.CreateExpenseAsync(dto);
        if (expense == null) return StatusCode(500, new { message = "Failed to create expense.", error });
        return CreatedAtAction(nameof(GetExpenseById), new { id = expense.ExpenseId },
            new { data = expense, error });
    }

    // ── POST /api/expenses/{id}/submit ───────────────────────────────────────

    /// <summary>Submit an expense for manager approval (Draft → Submitted).</summary>
    [HttpPost("expenses/{id:int}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitExpense(int id)
    {
        var (success, error) = await _svc.SubmitExpenseAsync(id);
        if (!success) return BadRequest(new { message = "Failed to submit expense.", error });
        return Ok(new { message = $"Expense {id} submitted successfully.", error });
    }

    // ── POST /api/expenses/{id}/approve ─────────────────────────────────────

    /// <summary>Approve a submitted expense (Submitted → Approved). Body: { reviewedById: int }</summary>
    [HttpPost("expenses/{id:int}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveExpense(int id, [FromBody] ReviewActionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var (success, error) = await _svc.ApproveExpenseAsync(id, dto.ReviewedById);
        if (!success) return BadRequest(new { message = "Failed to approve expense.", error });
        return Ok(new { message = $"Expense {id} approved successfully.", error });
    }

    // ── POST /api/expenses/{id}/reject ───────────────────────────────────────

    /// <summary>Reject a submitted expense (Submitted → Rejected). Body: { reviewedById: int }</summary>
    [HttpPost("expenses/{id:int}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectExpense(int id, [FromBody] ReviewActionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var (success, error) = await _svc.RejectExpenseAsync(id, dto.ReviewedById);
        if (!success) return BadRequest(new { message = "Failed to reject expense.", error });
        return Ok(new { message = $"Expense {id} rejected successfully.", error });
    }

    // ── GET /api/dashboard ───────────────────────────────────────────────────

    /// <summary>Get aggregate dashboard statistics.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardStats), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard()
    {
        var (stats, error) = await _svc.GetDashboardStatsAsync();
        return Ok(new { data = stats, error });
    }
}

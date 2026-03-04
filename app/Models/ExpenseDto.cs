using System.ComponentModel.DataAnnotations;

namespace ExpenseManagement.Models;

/// <summary>DTO used when creating a new expense via the API / form.</summary>
public class ExpenseDto
{
    [Required]
    public int UserId { get; set; } = 1;

    [Required]
    public int CategoryId { get; set; }

    /// <summary>Amount in pounds (e.g. 12.34). Converted to pence before storage.</summary>
    [Required]
    [Range(0.01, 100000)]
    public decimal Amount { get; set; }

    [Required]
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow.Date;

    [MaxLength(1000)]
    public string? Description { get; set; }
}

/// <summary>DTO for approve/reject actions.</summary>
public class ReviewActionDto
{
    [Required]
    public int ReviewedById { get; set; } = 2;
}

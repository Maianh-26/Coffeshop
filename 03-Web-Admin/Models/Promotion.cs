using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Admin.Models;

public class Promotion : IValidatableObject
{
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Nhập tên chương trình.")]
    [StringLength(100, ErrorMessage = "Tên không được vượt quá 100 ký tự.")]
    public string Name { get; set; } = "";

    [Range(1, 100, ErrorMessage = "Giảm giá từ 1 đến 100%.")]
    public decimal DiscountPercent { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Ngày kết thúc không được trước ngày bắt đầu.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate.HasValue && EndDate.HasValue && EndDate.Value.Date < StartDate.Value.Date)
        {
            yield return new ValidationResult(
                "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.",
                [nameof(EndDate)]);
        }
    }
}

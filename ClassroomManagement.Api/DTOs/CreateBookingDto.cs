using System.ComponentModel.DataAnnotations;
using ClassroomManagement.Api.Validators;

namespace ClassroomManagement.Api.DTOs;

public class CreateBookingDto : IValidatableObject
{
    [Required(ErrorMessage = "ClassroomId є обов'язковим.")]
    [NotEmptyGuid(ErrorMessage = "Необхідно вказати коректний ідентифікатор аудиторії (не порожній GUID).")]
    public Guid ClassroomId { get; set; }

    [Required(ErrorMessage = "Поле 'Хто бронює' (ReservedBy) є обов'язковим.")]
    [MinLength(2, ErrorMessage = "Поле 'Хто бронює' (ReservedBy) має містити щонайменше 2 символи.")]
    public string ReservedBy { get; set; } = string.Empty;

    [Required(ErrorMessage = "Час початку є обов'язковим.")]
    public DateTime StartTime { get; set; }

    [Required(ErrorMessage = "Час закінчення є обов'язковим.")]
    public DateTime EndTime { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndTime <= StartTime)
        {
            yield return new ValidationResult(
                "Час закінчення бронювання повинен бути пізнішим за час початку.",
                new[] { nameof(EndTime) });
        }
    }
}

using System.ComponentModel.DataAnnotations;

namespace ClassroomManagement.Api.DTOs;

public class CreateClassroomDto
{
    [Required(ErrorMessage = "Номер аудиторії є обов'язковим.")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "Номер аудиторії має містити від 1 до 20 символів.")]
    public string Number { get; set; } = string.Empty;

    [Required(ErrorMessage = "Корпус є обов'язковим.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Назва корпусу має містити від 2 до 100 символів.")]
    public string Building { get; set; } = string.Empty;

    [Range(1, 1000, ErrorMessage = "Місткість аудиторії повинна бути від 1 до 1000 місць.")]
    public int Capacity { get; set; }

    public List<string> Equipment { get; set; } = new();
}

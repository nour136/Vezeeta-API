using Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.DTOs.DoctorDTOs
{
    public class AppointmentDTO : IValidatableObject
    {
        [Range(1, int.MaxValue, ErrorMessage = "Price must be greater than 0.")]
        public int Price { get; set; }

        [Required]
        public Days Days { get; set; }

        [Required(ErrorMessage = "At least one time slot is required.")]
        [MinLength(1, ErrorMessage = "At least one time slot is required.")]
        public ICollection<TimeOnly> TimeOnly { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (TimeOnly is not null && TimeOnly.Distinct().Count() != TimeOnly.Count)
            {
                yield return new ValidationResult(
                    "Duplicate time slots are not allowed.",
                    new[] { nameof(TimeOnly) });
            }
        }
    }
}

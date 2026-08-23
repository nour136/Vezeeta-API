using Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.DTOs.AdminDTOs
{
    public class DiscountCodeDTO : IValidatableObject
    {
        [Required]
        [MaxLength(50, ErrorMessage = "Name must not exceed 50 characters.")]
        public string Name { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "NumberOfRequests cannot be negative.")]
        public int NumberOfRequests { get; set; }

        public bool IsActive { get; set; }

        [Required]
        public DiscountType DiscountType { get; set; }

        public int Discount { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DiscountType == DiscountType.Percentage && (Discount < 1 || Discount > 100))
            {
                yield return new ValidationResult(
                    "Percentage discount must be between 1 and 100.",
                    new[] { nameof(Discount) });
            }
            else if (DiscountType == DiscountType.Value && Discount < 1)
            {
                yield return new ValidationResult(
                    "Discount value must be greater than 0.",
                    new[] { nameof(Discount) });
            }
        }
    }
}

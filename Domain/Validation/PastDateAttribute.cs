using System.ComponentModel.DataAnnotations;

namespace Domain.Validation
{
    public class PastDateAttribute : ValidationAttribute
    {
        public PastDateAttribute()
        {
            ErrorMessage = "{0} cannot be in the future.";
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is DateTime date)
            {
                if (date.Date > DateTime.Now.Date)
                {
                    return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
                }
            }

            return ValidationResult.Success;
        }
    }
}

using Domain.Enums;
using Domain.Validation;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Domain.DTOs.AuthDTOs
{
    public class EditDoctorDTO
    {
        [Required]
        public string Id { get; set; }

        [Required]
        [MaxLength(50, ErrorMessage = "First Name must not exceed 50 characters."),
            MinLength(3, ErrorMessage = "First Name must not be less than 3 characters.")]
        public string FirstName { get; set; }

        [Required]
        [MaxLength(50, ErrorMessage = "First name must not exceed 50 characters."),
            MinLength(3, ErrorMessage = "First name must not be less than 3 characters.")]
        public string LastName { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        [EmailAddress(ErrorMessage = "A valid email address is required.")]
        public string Email { get; set; }

        [Required]
        [Phone(ErrorMessage = "A valid phone number is required.")]
        public string Phone { get; set; }

        [Required]
        public Gender Gender { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "A valid specialization must be selected.")]
        public int SpecializeId { get; set; }

        public IFormFile? ImageFile { get; set; }

        [Required]
        [PastDate]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        public string Password { get; set; }
    }
}

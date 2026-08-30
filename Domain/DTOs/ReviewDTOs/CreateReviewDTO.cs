using System.ComponentModel.DataAnnotations;

namespace Domain.DTOs.ReviewDTOs
{
    public class CreateReviewDTO
    {
        [Required]
        public int BookingId { get; set; }

        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int Rating { get; set; }

        [MaxLength(1000, ErrorMessage = "Comment must not exceed 1000 characters.")]
        public string? Comment { get; set; }
    }
}

using System;

namespace Domain.DTOs.ReviewDTOs
{
    public class ReviewDTO
    {
        public int Id { get; set; }
        public string PatientName { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

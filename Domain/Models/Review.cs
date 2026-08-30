using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Domain.Models
{
    public class Review
    {
        public int Id { get; set; }

        [ForeignKey("BookingForeignKey")]
        public int BookingId { get; set; }

        [ForeignKey("PatientForeignKey")]
        public string PatientId { get; set; }

        [ForeignKey("DoctorForeignKey")]
        public string DoctorId { get; set; }

        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonIgnore]
        public virtual Booking Booking { get; set; }
        [JsonIgnore]
        public virtual ApplicationUser Patient { get; set; }
        [JsonIgnore]
        public virtual ApplicationUser Doctor { get; set; }
    }
}

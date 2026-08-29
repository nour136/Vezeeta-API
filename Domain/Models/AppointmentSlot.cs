using Domain.Enums;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Domain.Models
{
    public class AppointmentSlot
    {
        public int Id { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly Time { get; set; }
        public int Price { get; set; }
        public SlotStatus Status { get; set; } = SlotStatus.Available;

        [ForeignKey("DoctorForeignKey")]
        public string DoctorId { get; set; }

        public int? SourceAppointmentId { get; set; }

        [JsonIgnore]
        public virtual ApplicationUser Doctor { get; set; }
        [JsonIgnore]
        public virtual Appointment? SourceAppointment { get; set; }
        public virtual Booking? Booking { get; set; }
    }
}

using System;

namespace Domain.DTOs.PatientDTOs
{
    public class AppointmentSlotDTO
    {
        public int Id { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly Time { get; set; }
        public int Price { get; set; }
    }
}

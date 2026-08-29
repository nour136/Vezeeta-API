using Domain.Enums;
using System;

namespace Domain.DTOs.PatientDTOs
{
    public class BookingDTO
    {
        public int Id { get; set; }
        public int SlotId { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly Time { get; set; }
        public string DoctorName { get; set; }
        public RequestState RequestState { get; set; }
        public int Price { get; set; }
        public int FinalPrice { get; set; }
    }
}

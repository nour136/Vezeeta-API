namespace Domain.DTOs.PatientDTOs
{
    public class DoctorSearchResultDTO
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? Specialization { get; set; }
        public string? Image { get; set; }
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public int? MinPrice { get; set; }
        public int? MaxPrice { get; set; }
    }
}

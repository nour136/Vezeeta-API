using Domain.Models;

namespace Domain.Repositories
{
    public class DoctorSearchResult
    {
        public ApplicationUser Doctor { get; set; }
        public double? AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public int? MinPrice { get; set; }
        public int? MaxPrice { get; set; }
    }
}

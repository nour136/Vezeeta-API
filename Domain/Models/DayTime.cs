using System.Text.Json.Serialization;

namespace Domain.Models
{
    public class DayTime
    {
        public int Id { get; set; }
        public TimeOnly Time { get; set; }

        [JsonIgnore]
        public virtual Appointment Appointment { get; set; }
    }
}

using System.Text.Json.Serialization;

namespace Domain.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Days
    {
        Saturday,
        Sunday,
        Monday,
        Tuesday,
        Wednesday,
        Thursday,
        Friday
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Gender
    {
        Male,
        Female
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DiscountType
    {
        Percentage,
        Value
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RequestState
    {
        Pending = 0,
        Completed = 1,
        Cancelled = 2,
        Confirmed = 3
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SlotStatus
    {
        Available,
        Booked
    }
}

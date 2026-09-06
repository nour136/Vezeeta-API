namespace Domain.Notifications
{
    // Not a database entity - just the shape a notification takes on its way out.
    // Kept separate from Domain.Models so it's clear this never gets persisted.
    public class NotificationMessage
    {
        public string ToEmail { get; set; } = string.Empty;
        public string ToName { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }
}

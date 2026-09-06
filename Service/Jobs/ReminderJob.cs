using Domain.Enums;
using Domain.Repositories;
using Domain.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Service.Jobs
{
    // Runs on a schedule via Hangfire (registered as a recurring job in Program.cs).
    // Finds Confirmed bookings starting within the next 24 hours that haven't been reminded
    // yet, sends one reminder each, and marks them so the same booking is never reminded twice
    // even if this job runs again before the appointment happens.
    public class ReminderJob
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly INotificationService notificationService;
        private readonly ILogger<ReminderJob> logger;

        private static readonly TimeSpan ReminderWindow = TimeSpan.FromHours(24);

        public ReminderJob(IUnitOfWork unitOfWork, INotificationService notificationService, ILogger<ReminderJob> logger)
        {
            this.unitOfWork = unitOfWork;
            this.notificationService = notificationService;
            this.logger = logger;
        }

        public async Task SendDueRemindersAsync()
        {
            var candidates = await unitOfWork.Bookings.GetAllByPropertyAsync(
                b => b.Request.RequestState == RequestState.Confirmed && !b.Request.IsReminded);

            var now = DateTime.Now;

            var due = candidates
                .Where(b =>
                {
                    var startsAt = b.Slot.Date.ToDateTime(b.Slot.Time);
                    return startsAt > now && startsAt - now <= ReminderWindow;
                })
                .ToList();

            if (!due.Any())
                return;

            foreach (var booking in due)
            {
                try
                {
                    await notificationService.NotifyBookingReminderAsync(booking);
                    booking.Request.IsReminded = true;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Reminder failed to send for booking {BookingId}", booking.Id);
                }
            }

            unitOfWork.Complete();
            logger.LogInformation("Sent {Count} appointment reminder(s)", due.Count);
        }
    }
}

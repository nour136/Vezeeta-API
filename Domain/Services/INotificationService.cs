using Domain.Models;
using System.Threading.Tasks;

namespace Domain.Services
{
    public interface INotificationService
    {
        Task NotifyBookingCreatedAsync(Booking booking);
        Task NotifyBookingConfirmedAsync(Booking booking);
        Task NotifyBookingCancelledAsync(Booking booking, string cancelledByRole);
        Task NotifyBookingCompletedAsync(Booking booking);
    }
}

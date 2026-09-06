using Domain.Models;
using Domain.Repositories;
using Domain.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Service.Tests
{
    public class FakeRepository<T> : IBaseRepository<T> where T : class
    {
        private readonly Func<T, int> idSelector;

        public List<T> Items { get; } = new();

        public FakeRepository(Func<T, int> idSelector)
        {
            this.idSelector = idSelector;
        }

        public Task<IEnumerable<T>> GetAllPaginatedFilteredAsync(Expression<Func<T, bool>> filterCriteria, int page = 1, int count = 5)
            => Task.FromResult(Items.AsQueryable().Where(filterCriteria).Skip((page - 1) * count).Take(count).AsEnumerable());

        public Task<T> GetByIdAsync(int id)
            => Task.FromResult(Items.FirstOrDefault(i => idSelector(i) == id));

        public Task<IEnumerable<T>> GetAllAsync()
            => Task.FromResult(Items.AsEnumerable());

        public Task<IEnumerable<T>> GetAllByPropertyAsync(Expression<Func<T, bool>> criteria)
            => Task.FromResult(Items.AsQueryable().Where(criteria).AsEnumerable());

        public Task<T> CreateAsync(T entity)
        {
            Items.Add(entity);
            return Task.FromResult(entity);
        }

        public T Update(T entity) => entity;

        public T Delete(T entity)
        {
            Items.Remove(entity);
            return entity;
        }
    }

    // No-op by default; records what was "sent" so a test can assert on it if needed
    // (e.g. Assert.Contains("Created", fake.Sent)).
    public class FakeNotificationService : INotificationService
    {
        public List<string> Sent { get; } = new();

        public Task NotifyBookingCreatedAsync(Booking booking)
        {
            Sent.Add("Created");
            return Task.CompletedTask;
        }

        public Task NotifyBookingConfirmedAsync(Booking booking)
        {
            Sent.Add("Confirmed");
            return Task.CompletedTask;
        }

        public Task NotifyBookingCancelledAsync(Booking booking, string cancelledByRole)
        {
            Sent.Add($"Cancelled:{cancelledByRole}");
            return Task.CompletedTask;
        }

        public Task NotifyBookingCompletedAsync(Booking booking)
        {
            Sent.Add("Completed");
            return Task.CompletedTask;
        }

        public Task NotifyBookingReminderAsync(Booking booking)
        {
            Sent.Add("Reminder");
            return Task.CompletedTask;
        }
    }

    public class TestUnitOfWork : IUnitOfWork
    {
        public FakeRepository<Appointment> AppointmentsFake { get; } = new(a => a.Id);
        public FakeRepository<Booking> BookingsFake { get; } = new(b => b.Id);
        public FakeRepository<Specialization> SpecializationsFake { get; } = new(s => s.Id);
        public FakeRepository<Request> RequestsFake { get; } = new(r => r.Id);
        public FakeRepository<DayTime> TimeFake { get; } = new(t => t.Id);
        public FakeRepository<AppointmentSlot> SlotsFake { get; } = new(s => s.Id);
        public FakeRepository<DiscountCode> DiscountCodesFake { get; } = new(d => d.Id);
        public FakeRepository<ExpiredCode> ExpiredCodesFake { get; } = new(e => e.Id);
        public FakeRepository<Review> ReviewsFake { get; } = new(r => r.Id);

        public IUserRepository AuthRepository { get; set; } = null!;

        public IBaseRepository<Appointment> Appointments => AppointmentsFake;
        public IBaseRepository<Booking> Bookings => BookingsFake;
        public IBaseRepository<Specialization> Specializations => SpecializationsFake;
        public IBaseRepository<Request> Requests => RequestsFake;
        public IBaseRepository<DayTime> Time => TimeFake;
        public IBaseRepository<AppointmentSlot> Slots => SlotsFake;
        public IBaseRepository<DiscountCode> DiscountCodes => DiscountCodesFake;
        public IBaseRepository<ExpiredCode> ExpiredCodes => ExpiredCodesFake;
        public IBaseRepository<Review> Reviews => ReviewsFake;

        public int Complete() => 1;

        public void Dispose() { }
    }
}

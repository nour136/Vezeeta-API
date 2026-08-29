using Domain.Models;
using Domain.Repositories;
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

        public IUserRepository AuthRepository { get; set; } = null!;

        public IBaseRepository<Appointment> Appointments => AppointmentsFake;
        public IBaseRepository<Booking> Bookings => BookingsFake;
        public IBaseRepository<Specialization> Specializations => SpecializationsFake;
        public IBaseRepository<Request> Requests => RequestsFake;
        public IBaseRepository<DayTime> Time => TimeFake;
        public IBaseRepository<AppointmentSlot> Slots => SlotsFake;
        public IBaseRepository<DiscountCode> DiscountCodes => DiscountCodesFake;
        public IBaseRepository<ExpiredCode> ExpiredCodes => ExpiredCodesFake;

        public int Complete() => 1;

        public void Dispose() { }
    }
}

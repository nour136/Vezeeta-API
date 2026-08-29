using Domain.Enums;
using Domain.Utilities;
using Xunit;

namespace Service.Tests
{
    public class BookingTransitionsTests
    {
        [Theory]
        [InlineData(RequestState.Pending, RequestState.Confirmed, BookingTransitions.Actor.Doctor, true)]
        [InlineData(RequestState.Pending, RequestState.Confirmed, BookingTransitions.Actor.Patient, false)]
        [InlineData(RequestState.Pending, RequestState.Cancelled, BookingTransitions.Actor.Doctor, true)]
        [InlineData(RequestState.Pending, RequestState.Cancelled, BookingTransitions.Actor.Patient, true)]
        [InlineData(RequestState.Confirmed, RequestState.Cancelled, BookingTransitions.Actor.Doctor, true)]
        [InlineData(RequestState.Confirmed, RequestState.Cancelled, BookingTransitions.Actor.Patient, true)]
        [InlineData(RequestState.Confirmed, RequestState.Completed, BookingTransitions.Actor.Doctor, true)]
        [InlineData(RequestState.Confirmed, RequestState.Completed, BookingTransitions.Actor.Patient, false)]
        [InlineData(RequestState.Pending, RequestState.Completed, BookingTransitions.Actor.Doctor, false)]
        [InlineData(RequestState.Completed, RequestState.Cancelled, BookingTransitions.Actor.Doctor, false)]
        [InlineData(RequestState.Completed, RequestState.Cancelled, BookingTransitions.Actor.Patient, false)]
        [InlineData(RequestState.Cancelled, RequestState.Confirmed, BookingTransitions.Actor.Doctor, false)]
        [InlineData(RequestState.Cancelled, RequestState.Cancelled, BookingTransitions.Actor.Doctor, false)]
        public void IsAllowed_MatchesExpectedStateMachine(RequestState from, RequestState to, BookingTransitions.Actor actor, bool expected)
        {
            Assert.Equal(expected, BookingTransitions.IsAllowed(from, to, actor));
        }
    }
}

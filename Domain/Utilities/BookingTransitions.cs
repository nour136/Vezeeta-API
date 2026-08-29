using Domain.Enums;
using System.Collections.Generic;
using System.Linq;

namespace Domain.Utilities
{
    public static class BookingTransitions
    {
        public enum Actor
        {
            Patient,
            Doctor
        }

        private static readonly Dictionary<(RequestState From, RequestState To), Actor[]> Allowed = new()
        {
            [(RequestState.Pending, RequestState.Confirmed)] = new[] { Actor.Doctor },

            [(RequestState.Pending, RequestState.Cancelled)] = new[] { Actor.Doctor, Actor.Patient },
            [(RequestState.Confirmed, RequestState.Cancelled)] = new[] { Actor.Doctor, Actor.Patient },

            [(RequestState.Confirmed, RequestState.Completed)] = new[] { Actor.Doctor },
        };

        public static bool IsAllowed(RequestState from, RequestState to, Actor actor)
            => Allowed.TryGetValue((from, to), out var actors) && actors.Contains(actor);
    }
}

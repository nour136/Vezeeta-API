using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Models
{
    public class Booking
    {
        public int Id { get; set; }

        [ForeignKey("SlotForeignKey")]
        public int SlotId { get; set; }

        [ForeignKey("RequestForeignKey")]
        public int RequestId { get; set; }
        public int FinalPrice { get; set; }

        public virtual ApplicationUser Patient { get; set; }
        public virtual AppointmentSlot Slot { get; set; }
        public virtual Request Request { get; set; }
    }
}

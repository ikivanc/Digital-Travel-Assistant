using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TravelBot.Models
{
    public class HotelReservation
    {
        public string Region { get; set; }
        public string HotelName { get; set; }
        public int? GuestCount { get; set; }
        public string Room { get; set; }
        public DateTime? CheckInDate { get; set; }
        public DateTime? CheckOutDate { get; set; }
    }
}

// Path: QuickTechSystems.Domain/Entities/RestaurantTable.cs
using System;

namespace QuickTechSystems.Domain.Entities
{
    public class RestaurantTable
    {
        public int Id { get; set; }
        public int TableNumber { get; set; }
        public string Status { get; set; } = "Available"; // Available, Occupied, Reserved, Maintenance
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
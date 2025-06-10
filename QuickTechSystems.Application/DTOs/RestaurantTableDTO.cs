// Path: QuickTechSystems.Application/DTOs/RestaurantTableDTO.cs
using System;

namespace QuickTechSystems.Application.DTOs
{
    public class RestaurantTableDTO : BaseDTO
    {
        public int Id { get; set; }
        public int TableNumber { get; set; }
        public string Status { get; set; } = "Available";
        public string Description { get; set; } = string.Empty;
    }
}
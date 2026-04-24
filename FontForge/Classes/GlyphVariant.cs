using System;

namespace FontForge.Classes
{
    public class GlyphVariant
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ImagePath { get; set; } = "";   // полный путь к PNG
        public bool IsDefault { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}

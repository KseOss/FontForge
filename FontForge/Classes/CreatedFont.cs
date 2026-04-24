using System;
using System.Collections.Generic;

namespace FontForge.Classes
{
    public class CreatedFont
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // корзина
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; } = null;

        // символы
        public List<GlyphEntry> Glyphs { get; set; } = new List<GlyphEntry>();
    }
}

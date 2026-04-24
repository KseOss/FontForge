using System;
using System.Collections.Generic;

namespace FontForge.Classes
{
    public class GlyphEntry
    {
        public string Char { get; set; } = ""; // например "А"

        // Несколько вариантов одной буквы
        public List<GlyphVariant> Variants { get; set; } = new List<GlyphVariant>();

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}

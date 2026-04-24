using System;
using System.Collections.Generic;

namespace FontForge.Classes
{
    public class CreatedFont
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Корзина
        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; } = null;

        // Символы
        public List<GlyphEntry> Glyphs { get; set; } = new List<GlyphEntry>();

        // ВАЖНО:
        // Это нужно, чтобы ComboBox, ListBox и другие элементы
        // показывали название шрифта, а не FontForge.Classes.CreatedFont.
        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Name)
                ? "Без названия"
                : Name;
        }
    }
}
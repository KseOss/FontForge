using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FontForge.Classes
{
    public static class FontStorage
    {
        private static readonly string FontStoragePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fonts.json");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public static List<CreatedFont> LoadFonts()
        {
            try
            {
                if (!File.Exists(FontStoragePath))
                    return new List<CreatedFont>();

                var json = File.ReadAllText(FontStoragePath);
                return JsonSerializer.Deserialize<List<CreatedFont>>(json, JsonOptions) ?? new List<CreatedFont>();
            }
            catch
            {
                return new List<CreatedFont>();
            }
        }

        public static void SaveFonts(List<CreatedFont> fonts)
        {
            var json = JsonSerializer.Serialize(fonts, JsonOptions);
            File.WriteAllText(FontStoragePath, json);
        }

        public static CreatedFont? LoadFontById(Guid fontId)
        {
            var fonts = LoadFonts();
            return fonts.FirstOrDefault(f => f.Id == fontId);
        }

        public static string GetFontFolder(Guid fontId)
        {
            var root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserFonts");
            return Path.Combine(root, fontId.ToString("N"));
        }

        public static string GetGlyphsFolder(Guid fontId)
        {
            return Path.Combine(GetFontFolder(fontId), "glyphs");
        }

        public static void EnsureFolders(Guid fontId)
        {
            Directory.CreateDirectory(GetGlyphsFolder(fontId));
        }

        public static string BuildVariantFilePath(Guid fontId, string ch, Guid variantId)
        {
            EnsureFolders(fontId);

            // имя символа -> код
            int code = ch.Length > 0 ? char.ConvertToUtf32(ch, 0) : 0;
            string safeChar = "U" + code.ToString("X4");

            return Path.Combine(GetGlyphsFolder(fontId), $"{safeChar}_{variantId:N}.png");
        }

        public static void NormalizeDefaults(CreatedFont font, string ch)
        {
            var g = font.Glyphs.FirstOrDefault(x => x.Char == ch);
            if (g == null) return;

            // если нет вариантов — ок
            if (g.Variants.Count == 0) return;

            // если нет дефолтного — сделать первым
            if (!g.Variants.Any(v => v.IsDefault))
            {
                g.Variants[0].IsDefault = true;
                return;
            }

            // если дефолтных несколько — оставить один
            bool first = true;
            foreach (var v in g.Variants)
            {
                if (!v.IsDefault) continue;
                if (first) { first = false; continue; }
                v.IsDefault = false;
            }
        }
    }
}

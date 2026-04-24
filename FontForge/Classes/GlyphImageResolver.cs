using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FontForge.Classes
{
    public static class GlyphImageResolver
    {
        private static readonly Dictionary<string, string[]> LookAlikeMap = new(StringComparer.Ordinal)
        {
            // Latin -> Cyrillic
            ["A"] = new[] { "А" },
            ["a"] = new[] { "а" },
            ["B"] = new[] { "В" },
            ["C"] = new[] { "С" },
            ["c"] = new[] { "с" },
            ["E"] = new[] { "Е" },
            ["e"] = new[] { "е" },
            ["H"] = new[] { "Н" },
            ["K"] = new[] { "К" },
            ["M"] = new[] { "М" },
            ["O"] = new[] { "О" },
            ["o"] = new[] { "о" },
            ["P"] = new[] { "Р" },
            ["p"] = new[] { "р" },
            ["T"] = new[] { "Т" },
            ["X"] = new[] { "Х" },
            ["x"] = new[] { "х" },
            ["Y"] = new[] { "У" },
            ["y"] = new[] { "у" },

            // Cyrillic -> Latin
            ["А"] = new[] { "A" },
            ["а"] = new[] { "a" },
            ["В"] = new[] { "B" },
            ["С"] = new[] { "C" },
            ["с"] = new[] { "c" },
            ["Е"] = new[] { "E" },
            ["е"] = new[] { "e" },
            ["Н"] = new[] { "H" },
            ["К"] = new[] { "K" },
            ["М"] = new[] { "M" },
            ["О"] = new[] { "O" },
            ["о"] = new[] { "o" },
            ["Р"] = new[] { "P" },
            ["р"] = new[] { "p" },
            ["Т"] = new[] { "T" },
            ["Х"] = new[] { "X" },
            ["х"] = new[] { "x" },
            ["У"] = new[] { "Y" },
            ["у"] = new[] { "y" }
        };

        public static string? FindGlyphImagePath(CreatedFont? font, string? symbol, bool allowLookAlikeFallback = true)
        {
            if (font == null || font.Glyphs == null || string.IsNullOrWhiteSpace(symbol))
                return null;

            foreach (string candidate in BuildCandidateSymbols(symbol, allowLookAlikeFallback))
            {
                GlyphEntry? glyph = font.Glyphs.FirstOrDefault(g =>
                    string.Equals(g.Char, candidate, StringComparison.Ordinal));

                string? fromVariantsWindow = FindImagePathFromGlyphVariants(font.Id, glyph);

                if (!string.IsNullOrWhiteSpace(fromVariantsWindow))
                    return fromVariantsWindow;
            }

            return null;
        }

        public static bool HasDrawableGlyph(CreatedFont font, GlyphEntry glyph)
        {
            if (font == null || glyph == null || string.IsNullOrWhiteSpace(glyph.Char))
                return false;

            return !string.IsNullOrWhiteSpace(FindGlyphImagePath(font, glyph.Char, allowLookAlikeFallback: false));
        }

        public static List<string> GetDrawableChars(CreatedFont font)
        {
            if (font == null || font.Glyphs == null)
                return new List<string>();

            return font.Glyphs
                .Where(g => !string.IsNullOrWhiteSpace(g.Char))
                .Where(g => HasDrawableGlyph(font, g))
                .Select(g => g.Char)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
        }

        private static IEnumerable<string> BuildCandidateSymbols(string symbol, bool allowLookAlikeFallback)
        {
            yield return symbol;

            if (!allowLookAlikeFallback)
                yield break;

            if (LookAlikeMap.TryGetValue(symbol, out string[]? alternatives))
            {
                foreach (string alternative in alternatives)
                    yield return alternative;
            }
        }

        private static string? FindImagePathFromGlyphVariants(Guid fontId, GlyphEntry? glyph)
        {
            if (glyph == null || glyph.Variants == null || glyph.Variants.Count == 0)
                return null;

            // Берём именно варианты из окна "Варианты буквы".
            // Сначала тот, который отмечен красной точкой IsDefault.
            // Потом остальные, самые новые.
            var orderedVariants = glyph.Variants
                .OrderByDescending(v => v.IsDefault)
                .ThenByDescending(v => v.UpdatedAt)
                .ThenByDescending(v => v.CreatedAt)
                .ToList();

            foreach (GlyphVariant variant in orderedVariants)
            {
                string? path = ResolveVariantPath(fontId, glyph.Char, variant);

                if (!string.IsNullOrWhiteSpace(path))
                    return path;
            }

            return null;
        }

        private static string? ResolveVariantPath(Guid fontId, string ch, GlyphVariant variant)
        {
            // 1. Главный путь, который хранится в варианте буквы.
            if (IsGoodPngFile(variant.ImagePath))
                return variant.ImagePath;

            // 2. Если ImagePath почему-то пустой или устарел,
            // строим путь так же, как GlyphEditorWindow сохраняет PNG.
            try
            {
                string expectedPath = FontStorage.BuildVariantFilePath(fontId, ch, variant.Id);

                if (IsGoodPngFile(expectedPath))
                    return expectedPath;
            }
            catch
            {
                // не падаем
            }

            // 3. Если имя файла чуть изменилось, ищем по id варианта.
            try
            {
                string glyphsFolder = FontStorage.GetGlyphsFolder(fontId);

                if (!Directory.Exists(glyphsFolder))
                    return null;

                string variantId = variant.Id.ToString("N");

                string? byVariantId = Directory
                    .GetFiles(glyphsFolder, $"*{variantId}*.png", SearchOption.TopDirectoryOnly)
                    .Where(IsGoodPngFile)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(byVariantId))
                    return byVariantId;
            }
            catch
            {
                // не падаем
            }

            return null;
        }

        private static bool IsGoodPngFile(string? path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return false;

                if (!File.Exists(path))
                    return false;

                if (!string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
                    return false;

                var info = new FileInfo(path);

                // PNG не может быть совсем маленьким.
                return info.Length > 20;
            }
            catch
            {
                return false;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;

namespace FontForge.Classes
{
    public sealed class FontExportResult
    {
        public int ExportedGlyphCount { get; set; }
        public int SkippedGlyphCount { get; set; }
        public List<string> SkippedChars { get; set; } = new List<string>();
    }

    public static class TrueTypeFontExporter
    {
        private const int UnitsPerEm = 1000;
        private const int AdvanceWidth = 1000;
        private const int Ascender = 850;
        private const int Descender = -200;
        private const int LineGap = 200;

        public static FontExportResult Export(CreatedFont font, string outputPath)
        {
            if (font == null)
                throw new ArgumentNullException(nameof(font));

            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Путь сохранения пустой.", nameof(outputPath));

            var result = new FontExportResult();

            var glyphs = new List<ExportGlyph>();

            // glyph 0: .notdef
            glyphs.Add(new ExportGlyph
            {
                Name = ".notdef",
                Unicode = null,
                Glyph = BuildNotDefGlyph()
            });

            // glyph 1: space
            glyphs.Add(new ExportGlyph
            {
                Name = "space",
                Unicode = 32,
                Glyph = GlyphData.Empty()
            });

            if (font.Glyphs != null)
            {
                foreach (GlyphEntry glyphEntry in font.Glyphs
                             .Where(g => !string.IsNullOrWhiteSpace(g.Char))
                             .OrderBy(g => g.Char, StringComparer.Ordinal))
                {
                    int unicode;

                    try
                    {
                        unicode = char.ConvertToUtf32(glyphEntry.Char, 0);
                    }
                    catch
                    {
                        result.SkippedGlyphCount++;
                        result.SkippedChars.Add(glyphEntry.Char);
                        continue;
                    }

                    // Для простого cmap format 4 берём BMP-символы.
                    // Русские буквы, латиница, цифры, знаки — подходят.
                    if (unicode > 0xFFFF)
                    {
                        result.SkippedGlyphCount++;
                        result.SkippedChars.Add(glyphEntry.Char);
                        continue;
                    }

                    string? pngPath = GlyphImageResolver.FindGlyphImagePath(font, glyphEntry.Char, allowLookAlikeFallback: false);

                    if (string.IsNullOrWhiteSpace(pngPath))
                    {
                        result.SkippedGlyphCount++;
                        result.SkippedChars.Add(glyphEntry.Char);
                        continue;
                    }

                    string isfPath = Path.ChangeExtension(pngPath, ".isf");

                    if (!File.Exists(isfPath))
                    {
                        result.SkippedGlyphCount++;
                        result.SkippedChars.Add(glyphEntry.Char);
                        continue;
                    }

                    GlyphData glyphData = BuildGlyphFromIsf(isfPath);

                    if (glyphData.IsEmpty)
                    {
                        result.SkippedGlyphCount++;
                        result.SkippedChars.Add(glyphEntry.Char);
                        continue;
                    }

                    glyphs.Add(new ExportGlyph
                    {
                        Name = "uni" + unicode.ToString("X4"),
                        Unicode = unicode,
                        Glyph = glyphData
                    });

                    result.ExportedGlyphCount++;
                }
            }

            if (result.ExportedGlyphCount == 0)
                throw new InvalidOperationException("Нет символов для экспорта. Нарисуйте хотя бы один символ и сохраните его.");

            byte[] fontBytes = BuildTrueTypeFont(font.Name, glyphs);
            File.WriteAllBytes(outputPath, fontBytes);

            return result;
        }

        private static GlyphData BuildNotDefGlyph()
        {
            var glyph = new GlyphData();

            glyph.Contours.Add(new List<IntPoint>
            {
                new IntPoint(120, 0),
                new IntPoint(880, 0),
                new IntPoint(880, 760),
                new IntPoint(120, 760)
            });

            glyph.RecalculateBounds();
            return glyph;
        }

        private static GlyphData BuildGlyphFromIsf(string isfPath)
        {
            StrokeCollection strokes;

            using (var fs = new FileStream(isfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                strokes = new StrokeCollection(fs);
            }

            if (strokes.Count == 0)
                return GlyphData.Empty();

            var allPoints = new List<Point>();

            foreach (Stroke stroke in strokes)
            {
                foreach (StylusPoint p in stroke.StylusPoints)
                    allPoints.Add(new Point(p.X, p.Y));
            }

            if (allPoints.Count == 0)
                return GlyphData.Empty();

            double minX = allPoints.Min(p => p.X);
            double maxX = allPoints.Max(p => p.X);
            double minY = allPoints.Min(p => p.Y);
            double maxY = allPoints.Max(p => p.Y);

            double sourceWidth = Math.Max(1, maxX - minX);
            double sourceHeight = Math.Max(1, maxY - minY);

            double targetHeight = 760;
            double targetWidth = 760;

            double scale = Math.Min(targetWidth / sourceWidth, targetHeight / sourceHeight);

            double scaledWidth = sourceWidth * scale;
            double scaledHeight = sourceHeight * scale;

            double offsetX = (UnitsPerEm - scaledWidth) / 2.0 - minX * scale;
            double offsetY = 80 + scaledHeight + minY * scale;

            var glyph = new GlyphData();

            foreach (Stroke stroke in strokes)
            {
                List<Point> centerPoints = SimplifyStrokePoints(stroke);

                if (centerPoints.Count == 0)
                    continue;

                double strokeWidth = Math.Max(2, stroke.DrawingAttributes.Width);
                double halfWidth = Math.Max(14, strokeWidth * scale * 0.50);

                if (centerPoints.Count == 1)
                {
                    Point p = TransformPoint(centerPoints[0], scale, offsetX, offsetY);
                    glyph.Contours.Add(BuildCircleContour(p, halfWidth));
                    continue;
                }

                var transformed = centerPoints
                    .Select(p => TransformPoint(p, scale, offsetX, offsetY))
                    .ToList();

                List<IntPoint> left = new List<IntPoint>();
                List<IntPoint> right = new List<IntPoint>();

                for (int i = 0; i < transformed.Count; i++)
                {
                    Point prev = transformed[Math.Max(0, i - 1)];
                    Point next = transformed[Math.Min(transformed.Count - 1, i + 1)];

                    double dx = next.X - prev.X;
                    double dy = next.Y - prev.Y;

                    double len = Math.Sqrt(dx * dx + dy * dy);

                    if (len < 0.001)
                    {
                        dx = 1;
                        dy = 0;
                        len = 1;
                    }

                    double nx = -dy / len;
                    double ny = dx / len;

                    Point c = transformed[i];

                    left.Add(new IntPoint(
                        ToFontUnit(c.X + nx * halfWidth),
                        ToFontUnit(c.Y + ny * halfWidth)));

                    right.Add(new IntPoint(
                        ToFontUnit(c.X - nx * halfWidth),
                        ToFontUnit(c.Y - ny * halfWidth)));
                }

                right.Reverse();

                var contour = new List<IntPoint>();
                contour.AddRange(left);
                contour.AddRange(right);

                contour = RemoveDuplicatePoints(contour);

                if (contour.Count >= 3)
                    glyph.Contours.Add(contour);
            }

            glyph.RecalculateBounds();
            return glyph;
        }

        private static List<Point> SimplifyStrokePoints(Stroke stroke)
        {
            var result = new List<Point>();

            Point? last = null;

            foreach (StylusPoint sp in stroke.StylusPoints)
            {
                var current = new Point(sp.X, sp.Y);

                if (last == null)
                {
                    result.Add(current);
                    last = current;
                    continue;
                }

                double dx = current.X - last.Value.X;
                double dy = current.Y - last.Value.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance >= 2.0)
                {
                    result.Add(current);
                    last = current;
                }
            }

            if (result.Count == 1 && stroke.StylusPoints.Count > 1)
            {
                StylusPoint lastPoint = stroke.StylusPoints[stroke.StylusPoints.Count - 1];
                result.Add(new Point(lastPoint.X, lastPoint.Y));
            }

            // Ограничиваем количество точек, чтобы шрифт не был слишком тяжёлым.
            const int maxPoints = 180;

            if (result.Count > maxPoints)
            {
                var reduced = new List<Point>();

                double step = (double)(result.Count - 1) / (maxPoints - 1);

                for (int i = 0; i < maxPoints; i++)
                {
                    int index = (int)Math.Round(i * step);
                    index = Math.Clamp(index, 0, result.Count - 1);
                    reduced.Add(result[index]);
                }

                return reduced;
            }

            return result;
        }

        private static Point TransformPoint(Point p, double scale, double offsetX, double offsetY)
        {
            double x = p.X * scale + offsetX;
            double y = offsetY - p.Y * scale;

            return new Point(x, y);
        }

        private static List<IntPoint> BuildCircleContour(Point center, double radius)
        {
            var contour = new List<IntPoint>();

            const int count = 16;

            for (int i = 0; i < count; i++)
            {
                double angle = Math.PI * 2.0 * i / count;

                contour.Add(new IntPoint(
                    ToFontUnit(center.X + Math.Cos(angle) * radius),
                    ToFontUnit(center.Y + Math.Sin(angle) * radius)));
            }

            return contour;
        }

        private static List<IntPoint> RemoveDuplicatePoints(List<IntPoint> points)
        {
            var result = new List<IntPoint>();

            foreach (IntPoint p in points)
            {
                if (result.Count == 0 || result[result.Count - 1].X != p.X || result[result.Count - 1].Y != p.Y)
                    result.Add(p);
            }

            if (result.Count > 1 &&
                result[0].X == result[result.Count - 1].X &&
                result[0].Y == result[result.Count - 1].Y)
            {
                result.RemoveAt(result.Count - 1);
            }

            return result;
        }

        private static short ToFontUnit(double value)
        {
            int rounded = (int)Math.Round(value);
            rounded = Math.Clamp(rounded, short.MinValue, short.MaxValue);
            return (short)rounded;
        }

        private static byte[] BuildTrueTypeFont(string fontName, List<ExportGlyph> glyphs)
        {
            var cmapMap = new Dictionary<int, ushort>();

            // space
            cmapMap[32] = 1;

            for (ushort i = 0; i < glyphs.Count; i++)
            {
                if (glyphs[i].Unicode.HasValue)
                    cmapMap[glyphs[i].Unicode!.Value] = i;
            }

            List<byte[]> glyphBytes = new List<byte[]>();
            List<uint> loca = new List<uint>();

            uint offset = 0;

            int globalXMin = 0;
            int globalYMin = Descender;
            int globalXMax = AdvanceWidth;
            int globalYMax = Ascender;

            int maxPoints = 0;
            int maxContours = 0;

            foreach (ExportGlyph exportGlyph in glyphs)
            {
                loca.Add(offset);

                byte[] bytes = BuildGlyfRecord(exportGlyph.Glyph);
                glyphBytes.Add(bytes);

                offset += (uint)Pad4Length(bytes.Length);

                if (!exportGlyph.Glyph.IsEmpty)
                {
                    globalXMin = Math.Min(globalXMin, exportGlyph.Glyph.XMin);
                    globalYMin = Math.Min(globalYMin, exportGlyph.Glyph.YMin);
                    globalXMax = Math.Max(globalXMax, exportGlyph.Glyph.XMax);
                    globalYMax = Math.Max(globalYMax, exportGlyph.Glyph.YMax);

                    maxPoints = Math.Max(maxPoints, exportGlyph.Glyph.PointCount);
                    maxContours = Math.Max(maxContours, exportGlyph.Glyph.Contours.Count);
                }
            }

            loca.Add(offset);

            byte[] glyfTable = CombineGlyphs(glyphBytes);
            byte[] locaTable = BuildLocaTable(loca);
            byte[] headTable = BuildHeadTable(globalXMin, globalYMin, globalXMax, globalYMax);
            byte[] hheaTable = BuildHheaTable(glyphs.Count);
            byte[] maxpTable = BuildMaxpTable(glyphs.Count, maxPoints, maxContours);
            byte[] hmtxTable = BuildHmtxTable(glyphs);
            byte[] cmapTable = BuildCmapTable(cmapMap);
            byte[] nameTable = BuildNameTable(fontName);
            byte[] os2Table = BuildOS2Table(cmapMap);
            byte[] postTable = BuildPostTable();

            var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["cmap"] = cmapTable,
                ["glyf"] = glyfTable,
                ["head"] = headTable,
                ["hhea"] = hheaTable,
                ["hmtx"] = hmtxTable,
                ["loca"] = locaTable,
                ["maxp"] = maxpTable,
                ["name"] = nameTable,
                ["OS/2"] = os2Table,
                ["post"] = postTable
            };

            return BuildSfnt(tables);
        }

        private static byte[] BuildGlyfRecord(GlyphData glyph)
        {
            if (glyph.IsEmpty)
                return Array.Empty<byte>();

            using var ms = new MemoryStream();
            var w = new BeWriter(ms);

            w.WriteInt16((short)glyph.Contours.Count);
            w.WriteInt16((short)glyph.XMin);
            w.WriteInt16((short)glyph.YMin);
            w.WriteInt16((short)glyph.XMax);
            w.WriteInt16((short)glyph.YMax);

            int pointIndex = 0;

            foreach (List<IntPoint> contour in glyph.Contours)
            {
                pointIndex += contour.Count;
                w.WriteUInt16((ushort)(pointIndex - 1));
            }

            // instructionLength = 0
            w.WriteUInt16(0);

            List<IntPoint> points = glyph.Contours.SelectMany(c => c).ToList();

            // flags: все точки on-curve, координаты пишем 16-bit deltas
            foreach (IntPoint _ in points)
                w.WriteByte(0x01);

            short prevX = 0;
            foreach (IntPoint point in points)
            {
                short dx = (short)(point.X - prevX);
                w.WriteInt16(dx);
                prevX = point.X;
            }

            short prevY = 0;
            foreach (IntPoint point in points)
            {
                short dy = (short)(point.Y - prevY);
                w.WriteInt16(dy);
                prevY = point.Y;
            }

            return ms.ToArray();
        }

        private static byte[] CombineGlyphs(List<byte[]> glyphBytes)
        {
            using var ms = new MemoryStream();

            foreach (byte[] glyph in glyphBytes)
            {
                ms.Write(glyph, 0, glyph.Length);

                int pad = Pad4Length(glyph.Length) - glyph.Length;

                for (int i = 0; i < pad; i++)
                    ms.WriteByte(0);
            }

            return ms.ToArray();
        }

        private static byte[] BuildLocaTable(List<uint> offsets)
        {
            using var ms = new MemoryStream();
            var w = new BeWriter(ms);

            foreach (uint offset in offsets)
                w.WriteUInt32(offset);

            return ms.ToArray();
        }

        private static byte[] BuildHeadTable(int xMin, int yMin, int xMax, int yMax)
        {
            using var ms = new MemoryStream();
            var w = new BeWriter(ms);

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 2082844800L;

            w.WriteUInt32(0x00010000);
            w.WriteUInt32(0x00010000);
            w.WriteUInt32(0); // checkSumAdjustment, заполняется позже
            w.WriteUInt32(0x5F0F3CF5);
            w.WriteUInt16(0x000B);
            w.WriteUInt16(UnitsPerEm);
            w.WriteInt64(now);
            w.WriteInt64(now);
            w.WriteInt16((short)xMin);
            w.WriteInt16((short)yMin);
            w.WriteInt16((short)xMax);
            w.WriteInt16((short)yMax);
            w.WriteUInt16(0);
            w.WriteUInt16(8);
            w.WriteInt16(2);
            w.WriteInt16(1); // indexToLocFormat = long
            w.WriteInt16(0);

            return ms.ToArray();
        }

        private static byte[] BuildHheaTable(int glyphCount)
        {
            using var ms = new MemoryStream();
            var w = new BeWriter(ms);

            w.WriteUInt32(0x00010000);
            w.WriteInt16(Ascender);
            w.WriteInt16(Descender);
            w.WriteInt16(LineGap);
            w.WriteUInt16(AdvanceWidth);
            w.WriteInt16(0);
            w.WriteInt16(0);
            w.WriteInt16(AdvanceWidth);
            w.WriteInt16(1);
            w.WriteInt16(0);
            w.WriteInt16(0);

            for (int i = 0; i < 4; i++)
                w.WriteInt16(0);

            w.WriteInt16(0);
            w.WriteUInt16((ushort)glyphCount);

            return ms.ToArray();
        }

        private static byte[] BuildMaxpTable(int glyphCount, int maxPoints, int maxContours)
        {
            using var ms = new MemoryStream();
            var w = new BeWriter(ms);

            w.WriteUInt32(0x00010000);
            w.WriteUInt16((ushort)glyphCount);
            w.WriteUInt16((ushort)Math.Clamp(maxPoints, 0, ushort.MaxValue));
            w.WriteUInt16((ushort)Math.Clamp(maxContours, 0, ushort.MaxValue));
            w.WriteUInt16(0); // maxCompositePoints
            w.WriteUInt16(0); // maxCompositeContours
            w.WriteUInt16(2); // maxZones
            w.WriteUInt16(0); // maxTwilightPoints
            w.WriteUInt16(0); // maxStorage
            w.WriteUInt16(0); // maxFunctionDefs
            w.WriteUInt16(0); // maxInstructionDefs
            w.WriteUInt16(0); // maxStackElements
            w.WriteUInt16(0); // maxSizeOfInstructions
            w.WriteUInt16(0); // maxComponentElements
            w.WriteUInt16(0); // maxComponentDepth

            return ms.ToArray();
        }

        private static byte[] BuildHmtxTable(List<ExportGlyph> glyphs)
        {
            using var ms = new MemoryStream();
            var w = new BeWriter(ms);

            foreach (ExportGlyph glyph in glyphs)
            {
                w.WriteUInt16(AdvanceWidth);
                w.WriteInt16((short)Math.Clamp(glyph.Glyph.XMin, short.MinValue, short.MaxValue));
            }

            return ms.ToArray();
        }

        private static byte[] BuildCmapTable(Dictionary<int, ushort> cmap)
        {
            var codes = cmap.Keys
                .Where(c => c >= 0 && c <= 0xFFFF)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            if (!codes.Contains(32))
                codes.Insert(0, 32);

            int segCount = codes.Count + 1;
            int segCountX2 = segCount * 2;

            int power = 1;
            int entrySelector = 0;

            while (power * 2 <= segCount)
            {
                power *= 2;
                entrySelector++;
            }

            int searchRange = power * 2;
            int rangeShift = segCountX2 - searchRange;

            using var sub = new MemoryStream();
            var sw = new BeWriter(sub);

            sw.WriteUInt16(4);
            sw.WriteUInt16((ushort)(16 + segCount * 8));
            sw.WriteUInt16(0);
            sw.WriteUInt16((ushort)segCountX2);
            sw.WriteUInt16((ushort)searchRange);
            sw.WriteUInt16((ushort)entrySelector);
            sw.WriteUInt16((ushort)rangeShift);

            foreach (int code in codes)
                sw.WriteUInt16((ushort)code);

            sw.WriteUInt16(0xFFFF);
            sw.WriteUInt16(0);

            foreach (int code in codes)
                sw.WriteUInt16((ushort)code);

            sw.WriteUInt16(0xFFFF);

            foreach (int code in codes)
            {
                ushort glyphIndex = cmap.TryGetValue(code, out ushort gid) ? gid : (ushort)0;
                short delta = (short)((glyphIndex - code) & 0xFFFF);
                sw.WriteInt16(delta);
            }

            sw.WriteInt16(1); // 0xFFFF -> glyph 0

            for (int i = 0; i < segCount; i++)
                sw.WriteUInt16(0);

            byte[] subtable = sub.ToArray();

            using var ms = new MemoryStream();
            var w = new BeWriter(ms);

            w.WriteUInt16(0);
            w.WriteUInt16(1);
            w.WriteUInt16(3);
            w.WriteUInt16(1);
            w.WriteUInt32(12);
            ms.Write(subtable, 0, subtable.Length);

            return ms.ToArray();
        }

        private static byte[] BuildNameTable(string fontName)
        {
            string family = string.IsNullOrWhiteSpace(fontName) ? "My Handwriting Font" : fontName.Trim();
            string subfamily = "Regular";
            string fullName = family + " Regular";
            string version = "Version 1.0";
            string postScript = SanitizePostScriptName(family + "-Regular");

            var records = new List<NameRecordData>
            {
                new NameRecordData(1, family),
                new NameRecordData(2, subfamily),
                new NameRecordData(3, family + " Regular " + DateTime.Now.ToString("yyyyMMddHHmmss")),
                new NameRecordData(4, fullName),
                new NameRecordData(5, version),
                new NameRecordData(6, postScript)
            };

            using var stringData = new MemoryStream();

            foreach (NameRecordData record in records)
            {
                record.Offset = (ushort)stringData.Length;
                byte[] bytes = Encoding.BigEndianUnicode.GetBytes(record.Value);
                record.Length = (ushort)bytes.Length;
                stringData.Write(bytes, 0, bytes.Length);
            }

            using var ms = new MemoryStream();
            var w = new BeWriter(ms);

            w.WriteUInt16(0);
            w.WriteUInt16((ushort)records.Count);
            w.WriteUInt16((ushort)(6 + records.Count * 12));

            foreach (NameRecordData record in records)
            {
                w.WriteUInt16(3);
                w.WriteUInt16(1);
                w.WriteUInt16(0x0409);
                w.WriteUInt16(record.NameId);
                w.WriteUInt16(record.Length);
                w.WriteUInt16(record.Offset);
            }

            byte[] str = stringData.ToArray();
            ms.Write(str, 0, str.Length);

            return ms.ToArray();
        }

        private static string SanitizePostScriptName(string value)
        {
            var sb = new StringBuilder();

            foreach (char ch in value)
            {
                if (char.IsLetterOrDigit(ch))
                    sb.Append(ch);
                else if (ch == '-' || ch == '_')
                    sb.Append(ch);
            }

            if (sb.Length == 0)
                return "MyHandwritingFont-Regular";

            return sb.ToString();
        }

        private static byte[] BuildOS2Table(Dictionary<int, ushort> cmap)
        {
            int first = cmap.Keys.Where(k => k >= 0 && k <= 0xFFFF).DefaultIfEmpty(32).Min();
            int last = cmap.Keys.Where(k => k >= 0 && k <= 0xFFFF).DefaultIfEmpty(32).Max();

            using var ms = new MemoryStream();
            var w = new BeWriter(ms);

            w.WriteUInt16(0); // version
            w.WriteInt16(500); // xAvgCharWidth
            w.WriteUInt16(400); // usWeightClass
            w.WriteUInt16(5); // usWidthClass
            w.WriteUInt16(0); // fsType

            w.WriteInt16(650);
            w.WriteInt16(699);
            w.WriteInt16(0);
            w.WriteInt16(140);

            w.WriteInt16(650);
            w.WriteInt16(699);
            w.WriteInt16(0);
            w.WriteInt16(479);

            w.WriteInt16(49);
            w.WriteInt16(258);
            w.WriteInt16(0);

            for (int i = 0; i < 10; i++)
                w.WriteByte(0);

            // Basic Latin + Cyrillic
            w.WriteUInt32((uint)((1 << 0) | (1 << 9)));
            w.WriteUInt32(0);
            w.WriteUInt32(0);
            w.WriteUInt32(0);

            w.WriteAscii("FFRG");
            w.WriteUInt16(0x0040); // regular
            w.WriteUInt16((ushort)first);
            w.WriteUInt16((ushort)last);
            w.WriteInt16(Ascender);
            w.WriteInt16(Descender);
            w.WriteInt16(LineGap);
            w.WriteUInt16(1000);
            w.WriteUInt16(300);

            return ms.ToArray();
        }

        private static byte[] BuildPostTable()
        {
            using var ms = new MemoryStream();
            var w = new BeWriter(ms);

            w.WriteUInt32(0x00030000);
            w.WriteUInt32(0);
            w.WriteInt16(-75);
            w.WriteInt16(50);
            w.WriteUInt32(0);
            w.WriteUInt32(0);
            w.WriteUInt32(0);
            w.WriteUInt32(0);
            w.WriteUInt32(0);

            return ms.ToArray();
        }

        private static byte[] BuildSfnt(SortedDictionary<string, byte[]> tables)
        {
            int numTables = tables.Count;

            int maxPower = 1;
            int entrySelector = 0;

            while (maxPower * 2 <= numTables)
            {
                maxPower *= 2;
                entrySelector++;
            }

            int searchRange = maxPower * 16;
            int rangeShift = numTables * 16 - searchRange;

            int tableOffset = 12 + numTables * 16;

            var tableRecords = new List<TableRecord>();

            foreach (var table in tables)
            {
                int alignedOffset = Pad4Length(tableOffset);

                tableRecords.Add(new TableRecord
                {
                    Tag = table.Key,
                    Data = table.Value,
                    Offset = alignedOffset,
                    Length = table.Value.Length,
                    Checksum = ComputeChecksum(table.Value)
                });

                tableOffset = alignedOffset + Pad4Length(table.Value.Length);
            }

            using var ms = new MemoryStream();
            var w = new BeWriter(ms);

            w.WriteUInt32(0x00010000);
            w.WriteUInt16((ushort)numTables);
            w.WriteUInt16((ushort)searchRange);
            w.WriteUInt16((ushort)entrySelector);
            w.WriteUInt16((ushort)rangeShift);

            foreach (TableRecord record in tableRecords)
            {
                w.WriteTag(record.Tag);
                w.WriteUInt32(record.Checksum);
                w.WriteUInt32((uint)record.Offset);
                w.WriteUInt32((uint)record.Length);
            }

            foreach (TableRecord record in tableRecords)
            {
                while (ms.Length < record.Offset)
                    ms.WriteByte(0);

                ms.Write(record.Data, 0, record.Data.Length);

                while (ms.Length % 4 != 0)
                    ms.WriteByte(0);
            }

            byte[] file = ms.ToArray();

            TableRecord head = tableRecords.First(t => t.Tag == "head");
            WriteUInt32ToArray(file, head.Offset + 8, 0);

            uint checksum = ComputeChecksum(file);
            uint adjustment = unchecked(0xB1B0AFBA - checksum);

            WriteUInt32ToArray(file, head.Offset + 8, adjustment);

            return file;
        }

        private static uint ComputeChecksum(byte[] data)
        {
            uint sum = 0;
            int length = Pad4Length(data.Length);

            for (int i = 0; i < length; i += 4)
            {
                uint b0 = i < data.Length ? (uint)data[i] : 0u;
                uint b1 = i + 1 < data.Length ? (uint)data[i + 1] : 0u;
                uint b2 = i + 2 < data.Length ? (uint)data[i + 2] : 0u;
                uint b3 = i + 3 < data.Length ? (uint)data[i + 3] : 0u;

                uint value = (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
                sum = unchecked(sum + value);
            }

            return sum;
        }

        private static void WriteUInt32ToArray(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)((value >> 24) & 0xFF);
            data[offset + 1] = (byte)((value >> 16) & 0xFF);
            data[offset + 2] = (byte)((value >> 8) & 0xFF);
            data[offset + 3] = (byte)(value & 0xFF);
        }

        private static int Pad4Length(int length)
        {
            return (length + 3) & ~3;
        }

        private sealed class ExportGlyph
        {
            public string Name { get; set; } = "";
            public int? Unicode { get; set; }
            public GlyphData Glyph { get; set; } = GlyphData.Empty();
        }

        private sealed class GlyphData
        {
            public List<List<IntPoint>> Contours { get; } = new List<List<IntPoint>>();

            public bool IsEmpty => Contours.Count == 0;

            public int XMin { get; private set; }
            public int YMin { get; private set; }
            public int XMax { get; private set; }
            public int YMax { get; private set; }

            public int PointCount => Contours.Sum(c => c.Count);

            public static GlyphData Empty()
            {
                return new GlyphData();
            }

            public void RecalculateBounds()
            {
                if (IsEmpty)
                {
                    XMin = 0;
                    YMin = 0;
                    XMax = 0;
                    YMax = 0;
                    return;
                }

                var points = Contours.SelectMany(c => c).ToList();

                XMin = points.Min(p => p.X);
                YMin = points.Min(p => p.Y);
                XMax = points.Max(p => p.X);
                YMax = points.Max(p => p.Y);
            }
        }

        private readonly struct IntPoint
        {
            public short X { get; }
            public short Y { get; }

            public IntPoint(short x, short y)
            {
                X = x;
                Y = y;
            }
        }

        private sealed class NameRecordData
        {
            public ushort NameId { get; }
            public string Value { get; }
            public ushort Length { get; set; }
            public ushort Offset { get; set; }

            public NameRecordData(ushort nameId, string value)
            {
                NameId = nameId;
                Value = value;
            }
        }

        private sealed class TableRecord
        {
            public string Tag { get; set; } = "";
            public byte[] Data { get; set; } = Array.Empty<byte>();
            public int Offset { get; set; }
            public int Length { get; set; }
            public uint Checksum { get; set; }
        }

        private sealed class BeWriter
        {
            private readonly Stream _stream;

            public BeWriter(Stream stream)
            {
                _stream = stream;
            }

            public void WriteByte(byte value)
            {
                _stream.WriteByte(value);
            }

            public void WriteAscii(string value)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(value);
                _stream.Write(bytes, 0, bytes.Length);
            }

            public void WriteTag(string tag)
            {
                string fixedTag = tag.PadRight(4).Substring(0, 4);
                WriteAscii(fixedTag);
            }

            public void WriteUInt16(int value)
            {
                ushort v = (ushort)value;

                _stream.WriteByte((byte)((v >> 8) & 0xFF));
                _stream.WriteByte((byte)(v & 0xFF));
            }

            public void WriteInt16(int value)
            {
                short v = (short)value;
                WriteUInt16(unchecked((ushort)v));
            }

            public void WriteUInt32(uint value)
            {
                _stream.WriteByte((byte)((value >> 24) & 0xFF));
                _stream.WriteByte((byte)((value >> 16) & 0xFF));
                _stream.WriteByte((byte)((value >> 8) & 0xFF));
                _stream.WriteByte((byte)(value & 0xFF));
            }

            public void WriteInt64(long value)
            {
                unchecked
                {
                    ulong v = (ulong)value;

                    _stream.WriteByte((byte)((v >> 56) & 0xFF));
                    _stream.WriteByte((byte)((v >> 48) & 0xFF));
                    _stream.WriteByte((byte)((v >> 40) & 0xFF));
                    _stream.WriteByte((byte)((v >> 32) & 0xFF));
                    _stream.WriteByte((byte)((v >> 24) & 0xFF));
                    _stream.WriteByte((byte)((v >> 16) & 0xFF));
                    _stream.WriteByte((byte)((v >> 8) & 0xFF));
                    _stream.WriteByte((byte)(v & 0xFF));
                }
            }
        }
    }
}
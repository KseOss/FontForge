using FontForge.Classes;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FontForge
{
    public partial class DocumentEditorWindow : Window
    {
        private readonly Guid? _initialFontId;
        private List<CreatedFont> _fonts = new();
        private CreatedFont? _selectedFont;

        public DocumentEditorWindow() : this(null)
        {
        }

        public DocumentEditorWindow(Guid fontId) : this((Guid?)fontId)
        {
        }

        private DocumentEditorWindow(Guid? fontId)
        {
            InitializeComponent();
            _initialFontId = fontId;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            _fonts = FontStorage.LoadFonts() ?? new List<CreatedFont>();

            FontsCombo.ItemsSource = _fonts;
            FontsCombo.DisplayMemberPath = "Name";

            if (_fonts.Count > 0)
            {
                if (_initialFontId.HasValue)
                {
                    var match = _fonts.FirstOrDefault(f => f.Id == _initialFontId.Value);
                    FontsCombo.SelectedItem = match ?? _fonts[0];
                }
                else
                {
                    FontsCombo.SelectedItem = _fonts[0];
                }
            }

            RebuildPreview();
        }

        private void FontsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedFont = FontsCombo.SelectedItem as CreatedFont;
            RebuildPreview();
        }

        private void PreviewSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            RebuildPreview();
        }

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RebuildPreview();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SavePdf_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = "document.pdf"
            };

            if (sfd.ShowDialog() != true)
                return;

            string text = InputBox.Text ?? "";
            float fontSize = 12f;

            try
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.Content().Text(text).FontSize(fontSize);
                    });
                });

                doc.GeneratePdf(sfd.FileName);

                MessageBox.Show("PDF успешно сохранён.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сохранении PDF:\n" + ex.Message);
            }
        }

        private void RebuildPreview()
        {
            if (PreviewRoot == null || InputBox == null || PreviewSizeSlider == null)
                return;

            PreviewRoot.Children.Clear();

            string text = InputBox.Text ?? "";
            double size = PreviewSizeSlider.Value;

            if (_selectedFont == null)
            {
                var tb = new TextBlock
                {
                    Text = text,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = size * 0.35,
                    Foreground = (Brush)Application.Current.Resources["TextBrush"]
                };
                PreviewRoot.Children.Add(tb);
                return;
            }

            var lines = text.Replace("\r\n", "\n").Split('\n');

            foreach (var line in lines)
            {
                var wrap = new WrapPanel
                {
                    Margin = new Thickness(0, 0, 0, size * 0.25),
                    //VerticalAlignment = VerticalAlignment.Top
                };

                foreach (char ch in line)
                {
                    if (ch == ' ')
                    {
                        wrap.Children.Add(new Border { Width = size * 0.35, Height = 1 });
                        continue;
                    }

                    string s = ch.ToString();

                    var glyph = _selectedFont.Glyphs?.FirstOrDefault(g => g.Char == s);
                    var variant = glyph?.Variants?.FirstOrDefault(v => v.IsDefault) ?? glyph?.Variants?.FirstOrDefault();
                    string path = variant?.ImagePath ?? "";

                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        var img = new System.Windows.Controls.Image
                        {
                            Width = size,
                            Height = size,
                            Stretch = Stretch.Uniform,
                            Margin = new Thickness(2, 0, 2, 0)
                        };

                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.UriSource = new Uri(path, UriKind.Absolute);
                            bmp.EndInit();
                            img.Source = bmp;

                            wrap.Children.Add(img);
                        }
                        catch
                        {
                            wrap.Children.Add(new TextBlock
                            {
                                Text = s,
                                FontSize = size,
                                Foreground = (Brush)Application.Current.Resources["TextBrush"],
                                Margin = new Thickness(2, 0, 2, 0)
                            });
                        }
                    }
                    else
                    {
                        wrap.Children.Add(new TextBlock
                        {
                            Text = s,
                            FontSize = size,
                            Foreground = (Brush)Application.Current.Resources["TextBrush"],
                            Margin = new Thickness(2, 0, 2, 0)
                        });
                    }
                }

                PreviewRoot.Children.Add(wrap);
            }
        }
    }
}

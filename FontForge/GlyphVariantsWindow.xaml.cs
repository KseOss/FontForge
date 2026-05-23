using FontForge.Classes;
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
    public partial class GlyphVariantsWindow : Window
    {
        private readonly Guid _fontId;
        private readonly string _ch;

        private List<CreatedFont> _allFonts = new();
        private CreatedFont? _font;
        private GlyphEntry? _glyph;

        public GlyphVariantsWindow(Guid fontId, string ch)
        {
            InitializeComponent();

            _fontId = fontId;
            _ch = ch;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
            Build();
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LoadData()
        {
            _allFonts = FontStorage.LoadFonts();
            _font = _allFonts.FirstOrDefault(f => f.Id == _fontId);

            if (_font == null)
            {
                AppDialog.Warning(this, "Шрифт не найден.");
                Close();
                return;
            }

            if (_font.Glyphs == null)
                _font.Glyphs = new List<GlyphEntry>();

            _glyph = _font.Glyphs.FirstOrDefault(g => g.Char == _ch);

            if (_glyph == null)
            {
                _glyph = new GlyphEntry
                {
                    Char = _ch,
                    UpdatedAt = DateTime.Now,
                    Variants = new List<GlyphVariant>()
                };

                _font.Glyphs.Add(_glyph);
                SaveAll();
            }

            if (_glyph.Variants == null)
                _glyph.Variants = new List<GlyphVariant>();

            FontStorage.NormalizeDefaults(_font, _ch);

            TitleText.Text = $"Варианты буквы «{_ch}»";
        }

        private void SaveAll()
        {
            if (_font == null)
                return;

            int idx = _allFonts.FindIndex(x => x.Id == _font.Id);

            if (idx >= 0)
                _allFonts[idx] = _font;
            else
                _allFonts.Add(_font);

            FontStorage.SaveFonts(_allFonts);
        }

        private void ReloadData()
        {
            _allFonts = FontStorage.LoadFonts();
            _font = _allFonts.FirstOrDefault(f => f.Id == _fontId);

            if (_font == null)
                return;

            if (_font.Glyphs == null)
                _font.Glyphs = new List<GlyphEntry>();

            _glyph = _font.Glyphs.FirstOrDefault(g => g.Char == _ch);

            if (_glyph == null)
            {
                _glyph = new GlyphEntry
                {
                    Char = _ch,
                    UpdatedAt = DateTime.Now,
                    Variants = new List<GlyphVariant>()
                };

                _font.Glyphs.Add(_glyph);
            }

            if (_glyph.Variants == null)
                _glyph.Variants = new List<GlyphVariant>();

            FontStorage.NormalizeDefaults(_font, _ch);
        }

        private void Build()
        {
            if (_font == null || _glyph == null)
                return;

            CountText.Text = $"Количество вариантов: {_glyph.Variants.Count}";

            VariantsPanel.Children.Clear();

            foreach (GlyphVariant variant in _glyph.Variants.OrderByDescending(x => x.CreatedAt))
            {
                VariantsPanel.Children.Add(BuildVariantCard(variant));
            }

            VariantsPanel.Children.Add(BuildAddCard());
        }

        private UIElement BuildVariantCard(GlyphVariant variant)
        {
            var card = new Border
            {
                Style = (Style)FindResource("VariantCard"),
                Width = 265
            };

            var grid = new Grid();

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(175) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var top = new Grid();

            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var chText = new TextBlock
            {
                Text = _ch,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetTextBrush(),
                VerticalAlignment = VerticalAlignment.Center
            };

            var deleteButton = new Button
            {
                Style = (Style)FindResource("IconButton"),
                Content = "🗑️",
                ToolTip = "Удалить вариант",
                Tag = variant.Id
            };

            deleteButton.Click += DeleteVariant_Click;

            top.Children.Add(chText);

            Grid.SetColumn(deleteButton, 1);
            top.Children.Add(deleteButton);

            grid.Children.Add(top);

            var imageBorder = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromRgb(243, 244, 239)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(201, 208, 199)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 10, 0, 0)
            };

            Grid.SetRow(imageBorder, 1);

            var image = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(10)
            };

            if (!string.IsNullOrWhiteSpace(variant.ImagePath) && File.Exists(variant.ImagePath))
            {
                try
                {
                    var bmp = new BitmapImage();

                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bmp.UriSource = new Uri(variant.ImagePath, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();

                    image.Source = bmp;
                }
                catch
                {
                }
            }

            imageBorder.Child = image;

            var overlay = new Grid();
            overlay.Children.Add(imageBorder);

            if (variant.IsDefault)
            {
                var dot = new TextBlock
                {
                    Text = "•",
                    FontSize = 26,
                    FontWeight = FontWeights.Black,
                    Foreground = Brushes.Red,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 4, 10, 0)
                };

                overlay.Children.Add(dot);
            }

            var imageWrap = new Border
            {
                Background = Brushes.Transparent,
                Child = overlay
            };

            Grid.SetRow(imageWrap, 1);
            grid.Children.Add(imageWrap);

            var buttons = new StackPanel
            {
                Margin = new Thickness(0, 8, 0, 0)
            };

            Grid.SetRow(buttons, 2);

            var editButton = new Button
            {
                Style = (Style)FindResource("ActionButton"),
                Content = "РЕДАКТИРОВАТЬ",
                Tag = variant.Id
            };

            editButton.Click += Edit_Click;

            var makeDefaultButton = new Button
            {
                Style = (Style)FindResource("ActionButton"),
                Content = "ИСПОЛЬЗОВАТЬ В ШРИФТЕ",
                Tag = variant.Id,
                IsEnabled = !variant.IsDefault
            };

            makeDefaultButton.Click += MakeDefault_Click;

            var copyButton = new Button
            {
                Style = (Style)FindResource("ActionButton"),
                Content = "СКОПИРОВАТЬ",
                Tag = variant.Id
            };

            copyButton.Click += Copy_Click;

            buttons.Children.Add(editButton);
            buttons.Children.Add(makeDefaultButton);
            buttons.Children.Add(copyButton);

            grid.Children.Add(buttons);

            card.Child = grid;

            return card;
        }

        private UIElement BuildAddCard()
        {
            var wrap = new Border
            {
                Width = 120,
                Height = 120,
                CornerRadius = new CornerRadius(18),
                Background = GetSecondaryButtonBrush(),
                BorderBrush = GetBorderBrush(),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 16, 16)
            };

            var button = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Content = new TextBlock
                {
                    Text = "+",
                    FontSize = 42,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = GetTextBrush()
                }
            };

            button.Click += AddVariant_Click;

            wrap.Child = button;

            return wrap;
        }

        private void MakeVariantDefault(Guid variantId)
        {
            if (_glyph == null)
                return;

            foreach (GlyphVariant variant in _glyph.Variants)
            {
                variant.IsDefault = variant.Id == variantId;
            }
        }

        private void AddVariant_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null || _glyph == null)
                return;

            var variant = new GlyphVariant
            {
                Id = Guid.NewGuid(),
                IsDefault = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            var editor = new GlyphEditorWindow(_font.Id, _ch, variant.Id)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false
            };

            bool? result = false;

            try
            {
                Hide();
                result = editor.ShowDialog();
            }
            finally
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            }

            ReloadData();

            if (_font == null || _glyph == null)
                return;

            if (result == true)
            {
                variant.ImagePath = editor.SavedImagePath ?? "";
                variant.UpdatedAt = DateTime.Now;

                if (!string.IsNullOrWhiteSpace(variant.ImagePath))
                {
                    _glyph.Variants.Add(variant);

                    MakeVariantDefault(variant.Id);

                    _glyph.UpdatedAt = DateTime.Now;

                    FontStorage.NormalizeDefaults(_font, _ch);
                    SaveAll();
                }
            }

            ReloadData();
            Build();
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null || _glyph == null)
                return;

            if (sender is not Button button)
                return;

            if (button.Tag is not Guid id)
                return;

            GlyphVariant? variant = _glyph.Variants.FirstOrDefault(x => x.Id == id);

            if (variant == null)
                return;

            var editor = new GlyphEditorWindow(_font.Id, _ch, variant.Id)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false
            };

            bool? result = false;

            try
            {
                Hide();
                result = editor.ShowDialog();
            }
            finally
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            }

            ReloadData();

            if (_font == null || _glyph == null)
                return;

            if (result == true)
            {
                GlyphVariant? updatedVariant = _glyph.Variants.FirstOrDefault(x => x.Id == id);

                if (updatedVariant != null)
                {
                    updatedVariant.ImagePath = editor.SavedImagePath ?? updatedVariant.ImagePath;
                    updatedVariant.UpdatedAt = DateTime.Now;

                    MakeVariantDefault(updatedVariant.Id);

                    _glyph.UpdatedAt = DateTime.Now;

                    FontStorage.NormalizeDefaults(_font, _ch);
                    SaveAll();
                }
            }

            ReloadData();
            Build();
        }

        private void MakeDefault_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null || _glyph == null)
                return;

            if (sender is not Button button)
                return;

            if (button.Tag is not Guid id)
                return;

            foreach (GlyphVariant variant in _glyph.Variants)
            {
                variant.IsDefault = variant.Id == id;
            }

            FontStorage.NormalizeDefaults(_font, _ch);
            SaveAll();

            ReloadData();
            Build();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null || _glyph == null)
                return;

            if (sender is not Button button)
                return;

            if (button.Tag is not Guid id)
                return;

            GlyphVariant? source = _glyph.Variants.FirstOrDefault(x => x.Id == id);

            if (source == null)
                return;

            var newVariant = new GlyphVariant
            {
                Id = Guid.NewGuid(),
                IsDefault = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            if (!string.IsNullOrWhiteSpace(source.ImagePath) && File.Exists(source.ImagePath))
            {
                string newPath = FontStorage.BuildVariantFilePath(_font.Id, _ch, newVariant.Id);

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(newPath) ?? "");
                    File.Copy(source.ImagePath, newPath, true);

                    string sourceIsf = Path.ChangeExtension(source.ImagePath, ".isf");
                    string newIsf = Path.ChangeExtension(newPath, ".isf");

                    if (File.Exists(sourceIsf))
                        File.Copy(sourceIsf, newIsf, true);

                    newVariant.ImagePath = newPath;
                }
                catch
                {
                    newVariant.ImagePath = "";
                }
            }

            _glyph.Variants.Add(newVariant);
            _glyph.UpdatedAt = DateTime.Now;

            SaveAll();

            ReloadData();
            Build();
        }

        private void DeleteVariant_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null || _glyph == null)
                return;

            if (sender is not Button button)
                return;

            if (button.Tag is not Guid id)
                return;

            GlyphVariant? variant = _glyph.Variants.FirstOrDefault(x => x.Id == id);

            if (variant == null)
                return;

            MessageBoxResult result = AppDialog.Show(
                this,
                "Подтверждение",
                "Удалить этот вариант буквы?",
                MessageBoxButton.YesNo,
                AppDialogKind.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                if (!string.IsNullOrWhiteSpace(variant.ImagePath) && File.Exists(variant.ImagePath))
                    File.Delete(variant.ImagePath);

                string isfPath = Path.ChangeExtension(variant.ImagePath, ".isf");

                if (!string.IsNullOrWhiteSpace(isfPath) && File.Exists(isfPath))
                    File.Delete(isfPath);
            }
            catch
            {
            }

            _glyph.Variants.Remove(variant);

            FontStorage.NormalizeDefaults(_font, _ch);

            SaveAll();

            ReloadData();
            Build();
        }

        private static Brush GetTextBrush()
        {
            return Application.Current.Resources["TextBrush"] as Brush ?? Brushes.Black;
        }

        private static Brush GetBorderBrush()
        {
            return Application.Current.Resources["BorderBrush"] as Brush ?? Brushes.LightGray;
        }

        private static Brush GetSecondaryButtonBrush()
        {
            return Application.Current.Resources["SecondaryButtonBackgroundBrush"] as Brush ?? Brushes.LightGray;
        }
    }
}
using FontForge.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

        private bool _isThemeAnimating = false;

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


        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isThemeAnimating)
                return;

            _isThemeAnimating = true;


            var fadeTo = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));

            fadeTo.Completed += (_, __) =>
            {

                var fadeBack = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));

                fadeBack.Completed += (___, ____) =>
                {
                    _isThemeAnimating = false;
                };

                FadeOverlay.BeginAnimation(OpacityProperty, fadeBack);
            };

            FadeOverlay.BeginAnimation(OpacityProperty, fadeTo);
        }

        private void LoadData()
        {
            _allFonts = FontStorage.LoadFonts();
            _font = _allFonts.FirstOrDefault(f => f.Id == _fontId);

            if (_font == null)
            {
                MessageBox.Show("Шрифт не найден.");
                Close();
                return;
            }

            _glyph = _font.Glyphs.FirstOrDefault(g => g.Char == _ch);

            if (_glyph == null)
            {
                _glyph = new GlyphEntry
                {
                    Char = _ch,
                    UpdatedAt = DateTime.Now
                };

                _font.Glyphs.Add(_glyph);
                SaveAll();
            }

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

            FontStorage.SaveFonts(_allFonts);
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
                Width = 280
            };

            var grid = new Grid();

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(220) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var top = new Grid();

            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var chText = new TextBlock
            {
                Text = _ch,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetTextBrush()
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
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 12, 0, 0)
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
                    // Если картинка не загрузилась, карточка останется пустой.
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
                    FontSize = 28,
                    FontWeight = FontWeights.Black,
                    Foreground = Brushes.Red,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 6, 10, 0)
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
                Margin = new Thickness(0, 12, 0, 0)
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
                VerticalAlignment = VerticalAlignment.Top
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
                Owner = this
            };

            if (editor.ShowDialog() == true)
            {
                variant.ImagePath = editor.SavedImagePath ?? "";
                variant.UpdatedAt = DateTime.Now;

                _glyph.Variants.Add(variant);

                MakeVariantDefault(variant.Id);

                _glyph.UpdatedAt = DateTime.Now;

                FontStorage.NormalizeDefaults(_font, _ch);
                SaveAll();
                Build();
            }
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
                Owner = this
            };

            if (editor.ShowDialog() == true)
            {
                variant.ImagePath = editor.SavedImagePath ?? variant.ImagePath;
                variant.UpdatedAt = DateTime.Now;

                MakeVariantDefault(variant.Id);

                _glyph.UpdatedAt = DateTime.Now;

                FontStorage.NormalizeDefaults(_font, _ch);
                SaveAll();
                Build();
            }
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

            MessageBoxResult result = MessageBox.Show(
                "Удалить этот вариант буквы?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

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
                // Не мешаем удалению записи, если файл занят.
            }

            _glyph.Variants.Remove(variant);

            FontStorage.NormalizeDefaults(_font, _ch);

            SaveAll();
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
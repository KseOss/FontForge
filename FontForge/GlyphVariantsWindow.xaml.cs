using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using FontForge.Classes;

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

            ThemeToggleButton.IsChecked = !App.IsDarkTheme;
            App.ThemeChanged += OnThemeChanged;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
            Build();
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            App.ThemeChanged -= OnThemeChanged;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void OnThemeChanged()
        {
            ThemeToggleButton.IsChecked = !App.IsDarkTheme;
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isThemeAnimating) return;
            _isThemeAnimating = true;
            ThemeToggleButton.IsEnabled = false;

            bool wantLight = ThemeToggleButton.IsChecked == true;
            bool wantDark = !wantLight;

            var fadeTo = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));
            fadeTo.Completed += (_, __) =>
            {
                App.SetTheme(wantDark);

                var fadeBack = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
                fadeBack.Completed += (___, ____) =>
                {
                    ThemeToggleButton.IsEnabled = true;
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
                _glyph = new GlyphEntry { Char = _ch, UpdatedAt = DateTime.Now };
                _font.Glyphs.Add(_glyph);
                SaveAll();
            }

            FontStorage.NormalizeDefaults(_font, _ch);

            TitleText.Text = $"Варианты буквы \"{_ch}\"";
        }

        private void SaveAll()
        {
            if (_font == null) return;

            var idx = _allFonts.FindIndex(x => x.Id == _font.Id);
            if (idx >= 0) _allFonts[idx] = _font;

            FontStorage.SaveFonts(_allFonts);
        }

        private void Build()
        {
            if (_font == null || _glyph == null) return;

            int total = _glyph.Variants.Count;
            CountText.Text = $"Количество вариантов: {total}";

            VariantsPanel.Children.Clear();

            foreach (var v in _glyph.Variants.OrderByDescending(x => x.CreatedAt))
            {
                VariantsPanel.Children.Add(BuildVariantCard(v));
            }

            VariantsPanel.Children.Add(BuildAddCard());
        }

        private UIElement BuildVariantCard(GlyphVariant v)
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

            // ===== верх: буква + удалить
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var chText = new TextBlock
            {
                Text = _ch,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["TextBrush"]
            };

            var delBtn = new Button
            {
                Style = (Style)FindResource("IconButton"),
                Content = "🗑️",
                ToolTip = "Удалить вариант",
                Tag = v.Id
            };
            delBtn.Click += DeleteVariant_Click;

            top.Children.Add(chText);
            Grid.SetColumn(delBtn, 1);
            top.Children.Add(delBtn);

            grid.Children.Add(top);

            // ===== картинка + красная точка если вариант дефолтный
            var imgBorder = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 12, 0, 0)
            };
            Grid.SetRow(imgBorder, 1);

            var img = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(10)
            };

            if (!string.IsNullOrWhiteSpace(v.ImagePath) && File.Exists(v.ImagePath))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(v.ImagePath, UriKind.Absolute);
                    bmp.EndInit();
                    img.Source = bmp;
                }
                catch { }
            }

            imgBorder.Child = img;

            // overlay чтобы поставить точку
            var overlay = new Grid();
            overlay.Children.Add(imgBorder);

            if (v.IsDefault)
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

            var wrap = new Border { Background = Brushes.Transparent, Child = overlay };
            Grid.SetRow(wrap, 1);
            grid.Children.Add(wrap);

            // ===== кнопки
            var buttons = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            Grid.SetRow(buttons, 2);

            var edit = new Button
            {
                Style = (Style)FindResource("ActionButton"),
                Content = "РЕДАКТИРОВАТЬ",
                Tag = v.Id
            };
            edit.Click += Edit_Click;

            var makeDef = new Button
            {
                Style = (Style)FindResource("ActionButton"),
                Content = "ИСПОЛЬЗОВАТЬ В ШРИФТЕ",
                Tag = v.Id,
                IsEnabled = !v.IsDefault
            };
            makeDef.Click += MakeDefault_Click;

            var copy = new Button
            {
                Style = (Style)FindResource("ActionButton"),
                Content = "СКОПИРОВАТЬ",
                Tag = v.Id
            };
            copy.Click += Copy_Click;

            buttons.Children.Add(edit);
            buttons.Children.Add(makeDef);
            buttons.Children.Add(copy);

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
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x18, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Top
            };

            var btn = new Button
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
                    Foreground = (Brush)Application.Current.Resources["TextBrush"]
                }
            };
            btn.Click += AddVariant_Click;

            wrap.Child = btn;
            return wrap;
        }

        private void AddVariant_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null || _glyph == null) return;

            var variant = new GlyphVariant
            {
                Id = Guid.NewGuid(),
                IsDefault = _glyph.Variants.Count == 0, // первый автоматически используется
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            var editor = new GlyphEditorWindow(_font.Id, _ch, variant.Id) { Owner = this };
            if (editor.ShowDialog() == true)
            {
                variant.ImagePath = editor.SavedImagePath ?? "";
                variant.UpdatedAt = DateTime.Now;

                _glyph.Variants.Add(variant);
                _glyph.UpdatedAt = DateTime.Now;

                FontStorage.NormalizeDefaults(_font, _ch);
                SaveAll();
                Build();
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null || _glyph == null) return;
            if (sender is not Button btn) return;

            if (btn.Tag is not Guid id) return;

            var v = _glyph.Variants.FirstOrDefault(x => x.Id == id);
            if (v == null) return;

            var editor = new GlyphEditorWindow(_font.Id, _ch, v.Id) { Owner = this };
            if (editor.ShowDialog() == true)
            {
                v.ImagePath = editor.SavedImagePath ?? v.ImagePath;
                v.UpdatedAt = DateTime.Now;
                _glyph.UpdatedAt = DateTime.Now;

                SaveAll();
                Build();
            }
        }

        private void MakeDefault_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null || _glyph == null) return;
            if (sender is not Button btn) return;

            if (btn.Tag is not Guid id) return;

            foreach (var v in _glyph.Variants)
                v.IsDefault = (v.Id == id);

            FontStorage.NormalizeDefaults(_font, _ch);
            SaveAll();
            Build();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null || _glyph == null) return;
            if (sender is not Button btn) return;

            if (btn.Tag is not Guid id) return;

            var src = _glyph.Variants.FirstOrDefault(x => x.Id == id);
            if (src == null) return;

            var newVar = new GlyphVariant
            {
                Id = Guid.NewGuid(),
                IsDefault = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            if (!string.IsNullOrWhiteSpace(src.ImagePath) && File.Exists(src.ImagePath))
            {
                var newPath = FontStorage.BuildVariantFilePath(_font.Id, _ch, newVar.Id);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(newPath) ?? "");
                    File.Copy(src.ImagePath, newPath, true);
                    newVar.ImagePath = newPath;
                }
                catch
                {
                    newVar.ImagePath = "";
                }
            }

            _glyph.Variants.Add(newVar);
            _glyph.UpdatedAt = DateTime.Now;

            SaveAll();
            Build();
        }

        private void DeleteVariant_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null || _glyph == null) return;
            if (sender is not Button btn) return;

            if (btn.Tag is not Guid id) return;

            var v = _glyph.Variants.FirstOrDefault(x => x.Id == id);
            if (v == null) return;

            var result = MessageBox.Show(
                "Удалить этот вариант буквы?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (!string.IsNullOrWhiteSpace(v.ImagePath) && File.Exists(v.ImagePath))
                    File.Delete(v.ImagePath);
            }
            catch { }

            _glyph.Variants.Remove(v);

            // если удалили используемый — назначим другой
            FontStorage.NormalizeDefaults(_font, _ch);

            SaveAll();
            Build();
        }
    }
}

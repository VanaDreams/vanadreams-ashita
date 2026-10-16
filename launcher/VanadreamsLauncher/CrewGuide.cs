using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Vanadreams
{
    /// <summary>
    /// One of the crew (assets\crew) with a cream speech bubble: the tip in the bottom-right corner of a page.
    /// It never takes a click, so it can sit over a box's corner without getting in the way.
    /// </summary>
    public sealed class CrewGuide : Grid
    {
        private readonly Image _image = new Image { Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Bottom };
        private readonly TextBlock _text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, FontSize = 13.5, FontWeight = FontWeights.SemiBold, LineHeight = 18,
            Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x1C, 0x0A)), Effect = null
        };

        public CrewGuide()
        {
            IsHitTestVisible = false;
            HorizontalAlignment = HorizontalAlignment.Right;
            VerticalAlignment = VerticalAlignment.Bottom;
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var bubble = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xD2)),
                CornerRadius = new CornerRadius(12, 12, 2, 12),
                Padding = new Thickness(11, 8, 11, 8),
                MaxWidth = 200,
                // beside the figure's head, never over it: the guide takes its own room at the foot of its box
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 6, 0),
                Child = _text,
                Effect = new DropShadowEffect { ShadowDepth = 4, Direction = 270, BlurRadius = 10, Opacity = 0.4, Color = Colors.Black }
            };
            // the bubble's own shadow must not reach the words, and the page-wide TextBlock style would add one
            _text.Effect = null;
            _image.Effect = new DropShadowEffect { ShadowDepth = 6, Direction = 270, BlurRadius = 6, Opacity = 0.6, Color = Colors.Black };
            Children.Add(bubble);
            SetColumn(_image, 1);
            Children.Add(_image);
            ImageHeight = 104;
            Margin = new Thickness(0, 6, 0, 0);
        }

        /// <summary>chocobo, goblin, mandragora, moogle or orc</summary>
        public string Who
        {
            get { return (string)GetValue(WhoProperty); }
            set { SetValue(WhoProperty, value); }
        }
        public static readonly DependencyProperty WhoProperty = DependencyProperty.Register("Who", typeof(string), typeof(CrewGuide),
            new PropertyMetadata(null, (d, e) => ((CrewGuide)d).Load()));

        public string Tip
        {
            get { return _text.Text; }
            set { _text.Text = value; }
        }

        /// <summary>Mirrored, so the chocobo faces into the page.</summary>
        public bool Flip
        {
            get { return _image.RenderTransform is ScaleTransform; }
            set { _image.RenderTransformOrigin = new Point(0.5, 0.5); _image.RenderTransform = value ? new ScaleTransform(-1, 1) : null; }
        }

        public double ImageHeight
        {
            get { return _image.Height; }
            set { _image.Height = value; }
        }

        public double BubbleWidth
        {
            get { return ((Border)Children[0]).MaxWidth; }
            set { ((Border)Children[0]).MaxWidth = value; }
        }

        private void Load()
        {
            try { _image.Source = new BitmapImage(new Uri("pack://application:,,,/assets/crew/" + Who + ".png")); }
            catch (Exception ex) { Services.Log.Warn("crew " + Who + ": " + ex.Message); }
        }

        /// <summary>The crew's picture for code that places one by hand (the Play page's row).</summary>
        public static ImageSource Picture(string who) => new BitmapImage(new Uri("pack://application:,,,/assets/crew/" + who + ".png"));
    }
}

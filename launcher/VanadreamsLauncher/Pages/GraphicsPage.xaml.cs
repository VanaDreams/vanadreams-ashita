using System.Windows.Controls;

namespace Vanadreams.Pages
{
    /// <summary>The current profile's look and window. Each half is its own control, so either can move to another page.</summary>
    public partial class GraphicsPage : UserControl
    {
        public GraphicsPage(MainWindow win)
        {
            InitializeComponent();
            var p = App.State.CurrentProfile();
            Presets.Show(p);
            WindowSettings.Show(p);
            Presets.Changed += () => WindowSettings.Draw();   // a preset sets the frame rate too
        }
    }
}

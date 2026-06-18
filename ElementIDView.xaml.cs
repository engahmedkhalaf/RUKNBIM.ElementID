using System.Windows.Controls;

namespace RUKNBIM.ElementID
{
    public partial class ElementIDView : UserControl
    {
        public ElementIDView()
        {
            InitializeComponent();
            DataContext = new ElementIDViewModel();
        }
    }
}

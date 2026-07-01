using System.Windows.Controls;

namespace RUKNBIM.SmartSelect
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

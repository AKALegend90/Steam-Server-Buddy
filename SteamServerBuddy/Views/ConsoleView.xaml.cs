using System.Windows.Controls;
using SteamServerBuddy.ViewModels;

namespace SteamServerBuddy.Views
{
    public partial class ConsoleView : UserControl
    {
        public ConsoleView()
        {
            InitializeComponent();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is ConsoleViewModel vm && vm.AutoScroll)
            {
                var tb = sender as TextBox;
                tb?.ScrollToEnd();
            }
        }
    }
}

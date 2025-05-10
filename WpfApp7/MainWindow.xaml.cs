using System.Windows;

namespace WpfApp7
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new GameViewModel(this);
        }

        private void RulesButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Правила игры \"Морской бой\":\n\n" +
                           "1. Расставьте свои корабли на поле.\n" +
                           "2. Корабли не могут быть расположены рядом друг с другом.\n" +
                           "3. По очереди с компьютером атакуйте корабли противника.\n" +
                           "4. Первый, кто потопит все вражеские корабли, побеждает!", "Правила игры");
        }
    }
}
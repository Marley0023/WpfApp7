using System.Windows;
using System.Windows.Media;

namespace WpfApp7
{
    public class CellViewModel : ViewModelBase
    {
        public int Row { get; }
        public int Column { get; }

        private string displayChar = "";
        private ShipType shipType = ShipType.None;
        private bool isHit;
        private bool isEnabled = true;
        private Brush backgroundColor = Brushes.White;

        public string DisplayChar
        {
            get => displayChar;
            set => SetProperty(ref displayChar, value);
        }

        public ShipType ShipType
        {
            get => shipType;
            set => SetProperty(ref shipType, value);
        }

        public bool IsHit
        {
            get => isHit;
            set => SetProperty(ref isHit, value);
        }

        public bool IsEnabled
        {
            get => isEnabled;
            set => SetProperty(ref isEnabled, value);
        }

        public Brush BackgroundColor
        {
            get => backgroundColor;
            set => SetProperty(ref backgroundColor, value);
        }

        public CellViewModel(int row, int column)
        {
            Row = row;
            Column = column;
        }
    }

    public enum ShipType
    {
        None,
        Battleship,
        Cruiser,
        Destroyer,
        Submarine
    }
}
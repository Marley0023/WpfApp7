using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfApp7
{
    public class GameViewModel : ViewModelBase
    {
        private const int GridSize = 10;
        private Random random = new Random();
        private bool isPlayerTurn = true;
        private string gameStatus = "Игрок 1: Расставьте свой линкор (4 клетки).";
        private bool isHorizontalPlacement = true;
        private bool canPlaceShips = true;
        private MainWindow mainWindow;
        private bool isHuntingMode = false;
        private CellViewModel lastHitCell = null;
        private List<(int rowOffset, int colOffset)> huntDirections = new List<(int, int)>
    {
        (-1, 0), // Вверх
        (1, 0),  // Вниз
        (0, -1), // Влево
        (0, 1)   // Вправо
    };
        private int currentHuntDirectionIndex = 0;
        private List<CellViewModel> hitCellsInHunt = new List<CellViewModel>();

        public List<CellViewModel> PlayerCells { get; } = new List<CellViewModel>(); // Поле игрока
        public List<CellViewModel> EnemyCells { get; } = new List<CellViewModel>();  // Поле компьютера

        public ICommand PlayerCellClick { get; }
        public ICommand EnemyCellClick { get; }
        public ICommand TogglePlacementCommand { get; }
        public ICommand RestartGameCommand { get; }
        public ICommand ShowShipsInfoCommand { get; }
        public ICommand AutoPlaceShipsCommand { get; }

        public string GameStatus
        {
            get => gameStatus;
            set => SetProperty(ref gameStatus, value);
        }

        public bool IsHorizontalPlacement
        {
            get => isHorizontalPlacement;
            set => SetProperty(ref isHorizontalPlacement, value);
        }

        public bool CanPlaceShips
        {
            get => canPlaceShips;
            set => SetProperty(ref canPlaceShips, value);
        }

        private Dictionary<ShipType, int> shipCounts = new Dictionary<ShipType, int>
    {
        { ShipType.Battleship, 1 },
        { ShipType.Cruiser, 2 },
        { ShipType.Destroyer, 3 },
        { ShipType.Submarine, 4 }
    };

        private ShipType currentShipType = ShipType.Battleship;
        private bool gameEnded = false;

        public GameViewModel(MainWindow window)
        {
            mainWindow = window;

            for (int row = 0; row < GridSize; row++)
            {
                for (int col = 0; col < GridSize; col++)
                {
                    PlayerCells.Add(new CellViewModel(row, col));
                    EnemyCells.Add(new CellViewModel(row, col));
                }
            }

            PlayerCellClick = new RelayCommand<CellViewModel>(OnPlayerCellClick,
                cell => currentShipType != ShipType.None && cell != null && cell.ShipType == ShipType.None);

            EnemyCellClick = new RelayCommand<CellViewModel>(OnEnemyCellClick,
                cell => isPlayerTurn && !gameEnded && cell != null && !cell.IsHit);

            TogglePlacementCommand = new RelayCommand(TogglePlacementDirection);
            RestartGameCommand = new RelayCommand(RestartGame);
            ShowShipsInfoCommand = new RelayCommand(ShowShipsInfo);
            AutoPlaceShipsCommand = new RelayCommand(AutoPlaceShips);

            PlaceComputerShips();
        }

        private void AutoPlaceShips()
        {
            int retryLimit = 5; // Максимальное количество попыток перезапуска расстановки
            int retryCount = 0;
            bool allShipsPlaced = false;

            while (retryCount < retryLimit && !allShipsPlaced)
            {
                // Сбрасываем поле перед каждой попыткой
                foreach (var cell in PlayerCells)
                {
                    cell.ShipType = ShipType.None;
                    cell.IsHit = false;
                    cell.DisplayChar = "";
                    cell.BackgroundColor = Brushes.White;
                    cell.IsEnabled = true;
                }

                shipCounts = new Dictionary<ShipType, int>
            {
                { ShipType.Battleship, 1 },
                { ShipType.Cruiser, 2 },
                { ShipType.Destroyer, 3 },
                { ShipType.Submarine, 4 }
            };

                allShipsPlaced = true; // Предполагаем, что всё получится

                foreach (var shipType in Enum.GetValues(typeof(ShipType)).Cast<ShipType>().Where(st => st != ShipType.None))
                {
                    for (int i = 0; i < shipCounts[shipType]; i++)
                    {
                        int attempts = 0;
                        const int maxAttempts = 100;
                        bool placed = false;

                        while (attempts < maxAttempts && !placed)
                        {
                            var availableCells = PlayerCells.Where(c => c.ShipType == ShipType.None && CanPlaceComputerShip(c, shipType, PlayerCells)).ToList();
                            if (availableCells.Count == 0) break;

                            var randomCell = availableCells[random.Next(availableCells.Count)];
                            if (CanPlaceComputerShip(randomCell, shipType, PlayerCells))
                            {
                                PlaceShipAuto(randomCell, shipType, PlayerCells);
                                placed = true;
                            }
                            attempts++;
                        }

                        if (!placed)
                        {
                            allShipsPlaced = false;
                            break; // Прерываем размещение кораблей данного типа
                        }
                    }

                    if (!allShipsPlaced)
                    {
                        break; // Прерываем внешний цикл, чтобы начать заново
                    }
                }

                retryCount++;
            }

            if (!allShipsPlaced)
            {
                MessageBox.Show("Не удалось автоматически расставить корабли после нескольких попыток. Попробуйте расставить корабли вручную.", "Ошибка расстановки");
                return;
            }

            currentShipType = ShipType.None;
            CanPlaceShips = false;
            GameStatus = "Все корабли расставлены. Кликните на поле противника для атаки.";
            isPlayerTurn = true;
            UpdateCellVisibility(PlayerCells, EnemyCells);
        }

        private void PlaceShipAuto(CellViewModel startCell, ShipType shipType, List<CellViewModel> cells)
        {
            int shipSize = GetShipSize(shipType);
            bool isHorizontal = random.Next(2) == 0;

            if (!CanPlaceComputerShip(startCell, shipType, cells))
            {
                isHorizontal = !isHorizontal;
            }

            if (isHorizontal)
            {
                for (int i = 0; i < shipSize; i++)
                {
                    var cell = cells.First(c => c.Row == startCell.Row && c.Column == startCell.Column + i);
                    cell.ShipType = shipType;
                    cell.DisplayChar = "S";
                    cell.IsEnabled = false;
                }
            }
            else
            {
                for (int i = 0; i < shipSize; i++)
                {
                    var cell = cells.First(c => c.Row == startCell.Row + i && c.Column == startCell.Column);
                    cell.ShipType = shipType;
                    cell.DisplayChar = "S";
                    cell.IsEnabled = false;
                }
            }

            BlockAdjacentCells(startCell, shipType, cells);
        }

        private void UpdateCellVisibility(List<CellViewModel> playerCells, List<CellViewModel> enemyCells)
        {
            foreach (var cell in playerCells)
            {
                cell.IsEnabled = false;
                cell.DisplayChar = cell.ShipType != ShipType.None && !cell.IsHit ? "S" : cell.DisplayChar;
            }

            foreach (var cell in enemyCells)
            {
                cell.IsEnabled = !cell.IsHit;
                cell.DisplayChar = cell.IsHit ? cell.DisplayChar : "";
            }
        }

        private void RestartGame()
        {
            foreach (var cell in PlayerCells)
            {
                cell.ShipType = ShipType.None;
                cell.IsHit = false;
                cell.DisplayChar = "";
                cell.BackgroundColor = Brushes.White;
                cell.IsEnabled = true;
            }

            foreach (var cell in EnemyCells)
            {
                cell.ShipType = ShipType.None;
                cell.IsHit = false;
                cell.DisplayChar = "";
                cell.BackgroundColor = Brushes.White;
                cell.IsEnabled = false;
            }

            currentShipType = ShipType.Battleship;
            isPlayerTurn = true;
            gameEnded = false;
            GameStatus = "Расставьте свой линкор (4 клетки).";
            IsHorizontalPlacement = true;
            CanPlaceShips = true;
            isHuntingMode = false;
            lastHitCell = null;
            currentHuntDirectionIndex = 0;
            hitCellsInHunt.Clear();

            shipCounts = new Dictionary<ShipType, int>
        {
            { ShipType.Battleship, 1 },
            { ShipType.Cruiser, 2 },
            { ShipType.Destroyer, 3 },
            { ShipType.Submarine, 4 }
        };

            PlaceComputerShips();
        }

        private void ShowShipsInfo()
        {
            string info = "Количество кораблей:\n\n" +
                         $"Линкор (4 клетки): {shipCounts[ShipType.Battleship]}\n" +
                         $"Крейсер (3 клетки): {shipCounts[ShipType.Cruiser]}\n" +
                         $"Эсминец (2 клетки): {shipCounts[ShipType.Destroyer]}\n" +
                         $"Подлодка (1 клетка): {shipCounts[ShipType.Submarine]}";

            MessageBox.Show(info, "Информация о кораблях");
        }

        private void TogglePlacementDirection()
        {
            IsHorizontalPlacement = !IsHorizontalPlacement;
            GameStatus = $"Расставьте свой {GetShipName(currentShipType)} ({GetShipSize(currentShipType)} клетки). " +
                       $"Направление: {(IsHorizontalPlacement ? "горизонтальное" : "вертикальное")}";
        }

        private void OnPlayerCellClick(CellViewModel cell)
        {
            if (cell.ShipType != ShipType.None || !CanPlaceShip(cell, currentShipType, PlayerCells)) return;

            PlaceShip(cell, currentShipType, PlayerCells);
            shipCounts[currentShipType]--;

            if (shipCounts[currentShipType] == 0)
            {
                currentShipType = GetNextShipType(currentShipType);
                if (currentShipType == ShipType.None)
                {
                    CanPlaceShips = false;
                    GameStatus = "Все корабли расставлены. Кликните на поле противника для атаки.";
                    isPlayerTurn = true;
                    UpdateCellVisibility(PlayerCells, EnemyCells);
                }
                else
                {
                    GameStatus = $"Расставьте свой {GetShipName(currentShipType)} ({GetShipSize(currentShipType)} клетки). " +
                                 $"Направление: {(IsHorizontalPlacement ? "горизонтальное" : "вертикальное")}";
                }
            }
        }

        private void OnEnemyCellClick(CellViewModel cell)
        {
            if (!isPlayerTurn || cell.IsHit || gameEnded) return;

            cell.IsHit = true;

            if (cell.ShipType != ShipType.None)
            {
                cell.DisplayChar = "X";
                cell.BackgroundColor = Brushes.Red;
                GameStatus = "Попадание! Ваш ход снова.";

                if (IsShipSunk(cell, EnemyCells))
                {
                    MarkAroundShip(cell, EnemyCells);
                    GameStatus = "Корабль потоплен! Ваш ход снова.";
                }

                if (CheckWin(EnemyCells))
                {
                    GameStatus = "Вы победили!";
                    gameEnded = true;
                    ShowRestartDialog();
                }
            }
            else
            {
                cell.DisplayChar = "•";
                cell.BackgroundColor = Brushes.LightBlue;
                GameStatus = "Промах! Ход противника.";
                isPlayerTurn = false;
                ComputerTurn();
            }
        }

        private void ComputerTurn()
        {
            CellViewModel targetCell = null;

            if (isHuntingMode)
            {
                targetCell = HuntForShip();
                if (targetCell == null)
                {
                    isHuntingMode = false;
                    currentHuntDirectionIndex = 0;
                    hitCellsInHunt.Clear();
                }
            }

            if (targetCell == null)
            {
                var availableCells = PlayerCells.Where(c => !c.IsHit).ToList();
                if (availableCells.Count == 0)
                {
                    GameStatus = "Нет доступных клеток для атаки! Ваш ход.";
                    isPlayerTurn = true;
                    return;
                }

                targetCell = availableCells[random.Next(availableCells.Count)];
            }

            targetCell.IsHit = true;

            if (targetCell.ShipType != ShipType.None)
            {
                targetCell.DisplayChar = "X";
                targetCell.BackgroundColor = Brushes.Red;
                GameStatus = "Противник попал! Ход противника снова.";

                if (isHuntingMode)
                {
                    hitCellsInHunt.Add(targetCell);
                }
                else
                {
                    isHuntingMode = true;
                    lastHitCell = targetCell;
                    hitCellsInHunt.Clear();
                    hitCellsInHunt.Add(targetCell);
                    currentHuntDirectionIndex = 0;
                }

                if (IsShipSunk(targetCell, PlayerCells))
                {
                    var shipCells = new List<CellViewModel>();
                    FindConnectedShipCells(targetCell, PlayerCells, shipCells);

                    foreach (var cell in shipCells)
                    {
                        cell.BackgroundColor = Brushes.Red;
                        cell.DisplayChar = "X";
                    }

                    MarkAroundShip(targetCell, PlayerCells);
                    GameStatus = "Противник потопил ваш корабль! Ход противника снова.";

                    isHuntingMode = false;
                    currentHuntDirectionIndex = 0;
                    hitCellsInHunt.Clear();
                }

                if (CheckWin(PlayerCells))
                {
                    GameStatus = "Вы проиграли!";
                    gameEnded = true;
                    ShowRestartDialog();
                    return;
                }

                // Computer hit, so it gets another turn
                ComputerTurn();
            }
            else
            {
                targetCell.DisplayChar = "•";
                targetCell.BackgroundColor = Brushes.LightBlue;
                GameStatus = "Противник промахнулся! Ваш ход.";

                if (isHuntingMode)
                {
                    currentHuntDirectionIndex++;
                }

                // Computer missed, so it's the player's turn
                isPlayerTurn = true;
            }
        }

        private CellViewModel HuntForShip()
        {
            bool isHorizontal = hitCellsInHunt.Count >= 2 && hitCellsInHunt.All(c => c.Row == hitCellsInHunt[0].Row);
            bool isVertical = hitCellsInHunt.Count >= 2 && hitCellsInHunt.All(c => c.Column == hitCellsInHunt[0].Column);

            if (isHorizontal)
            {
                var minCol = hitCellsInHunt.Min(c => c.Column);
                var maxCol = hitCellsInHunt.Max(c => c.Column);
                var row = hitCellsInHunt[0].Row;

                if (minCol > 0)
                {
                    var leftCell = PlayerCells.FirstOrDefault(c => c.Row == row && c.Column == minCol - 1 && !c.IsHit);
                    if (leftCell != null) return leftCell;
                }

                if (maxCol < GridSize - 1)
                {
                    var rightCell = PlayerCells.FirstOrDefault(c => c.Row == row && c.Column == maxCol + 1 && !c.IsHit);
                    if (rightCell != null) return rightCell;
                }
            }
            else if (isVertical)
            {
                var minRow = hitCellsInHunt.Min(c => c.Row);
                var maxRow = hitCellsInHunt.Max(c => c.Row);
                var col = hitCellsInHunt[0].Column;

                if (minRow > 0)
                {
                    var topCell = PlayerCells.FirstOrDefault(c => c.Row == minRow - 1 && c.Column == col && !c.IsHit);
                    if (topCell != null) return topCell;
                }

                if (maxRow < GridSize - 1)
                {
                    var bottomCell = PlayerCells.FirstOrDefault(c => c.Row == maxRow + 1 && c.Column == col && !c.IsHit);
                    if (bottomCell != null) return bottomCell;
                }
            }
            else
            {
                while (currentHuntDirectionIndex < huntDirections.Count)
                {
                    var (rowOffset, colOffset) = huntDirections[currentHuntDirectionIndex];
                    int newRow = lastHitCell.Row + rowOffset;
                    int newCol = lastHitCell.Column + colOffset;

                    if (newRow >= 0 && newRow < GridSize && newCol >= 0 && newCol < GridSize)
                    {
                        var nextCell = PlayerCells.FirstOrDefault(c => c.Row == newRow && c.Column == newCol && !c.IsHit);
                        if (nextCell != null)
                        {
                            return nextCell;
                        }
                    }

                    currentHuntDirectionIndex++;
                }
            }

            return null;
        }

        private void ShowRestartDialog()
        {
            var result = MessageBox.Show("Хотите сыграть еще раз?", "Игра окончена", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                RestartGame();
            }
        }

        private bool IsShipSunk(CellViewModel hitCell, List<CellViewModel> cells)
        {
            var shipCells = new List<CellViewModel>();
            FindConnectedShipCells(hitCell, cells, shipCells);
            return shipCells.All(c => c.IsHit);
        }

        private void FindConnectedShipCells(CellViewModel cell, List<CellViewModel> cells, List<CellViewModel> result)
        {
            if (result.Contains(cell) || cell.ShipType == ShipType.None) return;

            result.Add(cell);

            var neighbors = new[]
            {
            cells.FirstOrDefault(c => c.Row == cell.Row - 1 && c.Column == cell.Column),
            cells.FirstOrDefault(c => c.Row == cell.Row + 1 && c.Column == cell.Column),
            cells.FirstOrDefault(c => c.Row == cell.Row && c.Column == cell.Column - 1),
            cells.FirstOrDefault(c => c.Row == cell.Row && c.Column == cell.Column + 1)
        };

            foreach (var neighbor in neighbors.Where(n => n != null))
            {
                FindConnectedShipCells(neighbor, cells, result);
            }
        }

        private void MarkAroundShip(CellViewModel cell, List<CellViewModel> cells)
        {
            var shipCells = new List<CellViewModel>();
            FindConnectedShipCells(cell, cells, shipCells);

            foreach (var shipCell in shipCells)
            {
                for (int i = -1; i <= 1; i++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        int row = shipCell.Row + i;
                        int col = shipCell.Column + j;

                        if (row >= 0 && row < GridSize && col >= 0 && col < GridSize)
                        {
                            var aroundCell = cells.First(c => c.Row == row && c.Column == col);
                            if (!shipCells.Contains(aroundCell) && !aroundCell.IsHit)
                            {
                                aroundCell.IsHit = true;
                                aroundCell.DisplayChar = "•";
                                aroundCell.BackgroundColor = Brushes.LightBlue;
                            }
                        }
                    }
                }
            }
        }

        private void PlaceComputerShips()
        {
            foreach (var shipType in Enum.GetValues(typeof(ShipType)).Cast<ShipType>().Where(st => st != ShipType.None))
            {
                for (int i = 0; i < shipCounts[shipType]; i++)
                {
                    int attempts = 0;
                    const int maxAttempts = 100;
                    bool placed = false;

                    while (attempts < maxAttempts && !placed)
                    {
                        var availableCells = EnemyCells.Where(c => c.ShipType == ShipType.None && CanPlaceComputerShip(c, shipType, EnemyCells)).ToList();
                        if (availableCells.Count == 0) break;

                        var randomCell = availableCells[random.Next(availableCells.Count)];
                        if (CanPlaceComputerShip(randomCell, shipType, EnemyCells))
                        {
                            PlaceComputerShip(randomCell, shipType);
                            placed = true;
                        }
                        attempts++;
                    }

                    if (!placed)
                    {
                        MessageBox.Show($"Не удалось разместить {GetShipName(shipType)} для компьютера. Попробуйте перезапустить игру.", "Ошибка расстановки");
                        return;
                    }
                }
            }

            foreach (var enemyCell in EnemyCells)
            {
                enemyCell.IsEnabled = false;
            }
        }

        private bool CanPlaceComputerShip(CellViewModel startCell, ShipType shipType, List<CellViewModel> cells)
        {
            int shipSize = GetShipSize(shipType);
            bool isHorizontal = random.Next(2) == 0;

            if (isHorizontal)
            {
                // Check if the ship fits within the grid
                if (startCell.Column + shipSize > GridSize) return false;

                // Check if the cells where the ship will be placed are free
                for (int i = 0; i < shipSize; i++)
                {
                    var cell = cells.First(c => c.Row == startCell.Row && c.Column == startCell.Column + i);
                    if (cell.ShipType != ShipType.None)
                    {
                        return false;
                    }
                }

                // Check surrounding cells (including diagonals) for other ships
                for (int i = -1; i <= shipSize; i++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        int row = startCell.Row + j;
                        int col = startCell.Column + i;

                        if (row >= 0 && row < GridSize && col >= 0 && col < GridSize)
                        {
                            var cell = cells.First(c => c.Row == row && c.Column == col);
                            // If there's a ship in a surrounding cell, return false
                            if (cell.ShipType != ShipType.None)
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            else
            {
                // Check if the ship fits within the grid
                if (startCell.Row + shipSize > GridSize) return false;

                // Check if the cells where the ship will be placed are free
                for (int i = 0; i < shipSize; i++)
                {
                    var cell = cells.First(c => c.Row == startCell.Row + i && c.Column == startCell.Column);
                    if (cell.ShipType != ShipType.None)
                    {
                        return false;
                    }
                }

                // Check surrounding cells (including diagonals) for other ships
                for (int i = -1; i <= shipSize; i++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        int row = startCell.Row + i;
                        int col = startCell.Column + j;

                        if (row >= 0 && row < GridSize && col >= 0 && col < GridSize)
                        {
                            var cell = cells.First(c => c.Row == row && c.Column == col);
                            // If there's a ship in a surrounding cell, return false
                            if (cell.ShipType != ShipType.None)
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        private void PlaceComputerShip(CellViewModel startCell, ShipType shipType)
        {
            int shipSize = GetShipSize(shipType);
            bool isHorizontal = random.Next(2) == 0;

            if (!CanPlaceComputerShip(startCell, shipType, EnemyCells))
            {
                isHorizontal = !isHorizontal;
            }

            if (isHorizontal)
            {
                for (int i = 0; i < shipSize; i++)
                {
                    var cell = EnemyCells.First(c => c.Row == startCell.Row && c.Column == startCell.Column + i);
                    cell.ShipType = shipType;
                }
            }
            else
            {
                for (int i = 0; i < shipSize; i++)
                {
                    var cell = EnemyCells.First(c => c.Row == startCell.Row + i && c.Column == startCell.Column);
                    cell.ShipType = shipType;
                }
            }
        }

        private bool CanPlaceShip(CellViewModel startCell, ShipType shipType, List<CellViewModel> cells)
        {
            int shipSize = GetShipSize(shipType);

            if (IsHorizontalPlacement)
            {
                if (startCell.Column + shipSize > GridSize) return false;

                for (int i = 0; i < shipSize; i++)
                {
                    if (cells.Any(c => c.Row == startCell.Row && c.Column == startCell.Column + i && c.ShipType != ShipType.None))
                    {
                        return false;
                    }
                }

                for (int i = -1; i <= shipSize; i++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        int row = startCell.Row + j;
                        int col = startCell.Column + i;

                        if (row >= 0 && row < GridSize && col >= 0 && col < GridSize)
                        {
                            var cell = cells.FirstOrDefault(c => c.Row == row && c.Column == col);
                            if (cell != null && cell.ShipType != ShipType.None)
                            {
                                bool isPartOfShip = i >= 0 && i < shipSize && j == 0;
                                if (!isPartOfShip)
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (startCell.Row + shipSize > GridSize) return false;

                for (int i = 0; i < shipSize; i++)
                {
                    if (cells.Any(c => c.Row == startCell.Row + i && c.Column == startCell.Column && c.ShipType != ShipType.None))
                    {
                        return false;
                    }
                }

                for (int i = -1; i <= shipSize; i++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        int row = startCell.Row + i;
                        int col = startCell.Column + j;

                        if (row >= 0 && row < GridSize && col >= 0 && col < GridSize)
                        {
                            var cell = cells.FirstOrDefault(c => c.Row == row && c.Column == col);
                            if (cell != null && cell.ShipType != ShipType.None)
                            {
                                bool isPartOfShip = i >= 0 && i < shipSize && j == 0;
                                if (!isPartOfShip)
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }

        private void PlaceShip(CellViewModel startCell, ShipType shipType, List<CellViewModel> cells)
        {
            int shipSize = GetShipSize(shipType);

            if (IsHorizontalPlacement)
            {
                for (int i = 0; i < shipSize; i++)
                {
                    var cell = cells.First(c => c.Row == startCell.Row && c.Column == startCell.Column + i);
                    cell.ShipType = shipType;
                    cell.DisplayChar = "S";
                    cell.IsEnabled = false;
                }
            }
            else
            {
                for (int i = 0; i < shipSize; i++)
                {
                    var cell = cells.First(c => c.Row == startCell.Row + i && c.Column == startCell.Column);
                    cell.ShipType = shipType;
                    cell.DisplayChar = "S";
                    cell.IsEnabled = false;
                }
            }

            BlockAdjacentCells(startCell, shipType, cells);
        }

        private void BlockAdjacentCells(CellViewModel startCell, ShipType shipType, List<CellViewModel> cells)
        {
            int shipSize = GetShipSize(shipType);

            if (IsHorizontalPlacement)
            {
                for (int i = -1; i <= shipSize; i++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        int row = startCell.Row + j;
                        int col = startCell.Column + i;

                        if (row >= 0 && row < GridSize && col >= 0 && col < GridSize)
                        {
                            var cell = cells.First(c => c.Row == row && c.Column == col);
                            if (cell.ShipType == ShipType.None)
                            {
                                cell.IsEnabled = false;
                            }
                        }
                    }
                }
            }
            else
            {
                for (int i = -1; i <= shipSize; i++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        int row = startCell.Row + i;
                        int col = startCell.Column + j;

                        if (row >= 0 && row < GridSize && col >= 0 && col < GridSize)
                        {
                            var cell = cells.First(c => c.Row == row && c.Column == col);
                            if (cell.ShipType == ShipType.None)
                            {
                                cell.IsEnabled = false;
                            }
                        }
                    }
                }
            }
        }

        private int GetShipSize(ShipType shipType)
        {
            switch (shipType)
            {
                case ShipType.Battleship: return 4;
                case ShipType.Cruiser: return 3;
                case ShipType.Destroyer: return 2;
                case ShipType.Submarine: return 1;
                default: return 0;
            }
        }

        private ShipType GetNextShipType(ShipType currentShipType)
        {
            switch (currentShipType)
            {
                case ShipType.Battleship: return ShipType.Cruiser;
                case ShipType.Cruiser: return ShipType.Destroyer;
                case ShipType.Destroyer: return ShipType.Submarine;
                case ShipType.Submarine: return ShipType.None;
                default: return ShipType.None;
            }
        }

        private string GetShipName(ShipType shipType)
        {
            switch (shipType)
            {
                case ShipType.Battleship: return "линкор";
                case ShipType.Cruiser: return "крейсер";
                case ShipType.Destroyer: return "эсминец";
                case ShipType.Submarine: return "подводная лодка";
                default: return "";
            }
        }

        private bool CheckWin(List<CellViewModel> cells)
        {
            return cells.Where(c => c.ShipType != ShipType.None).All(c => c.IsHit);
        }
    }
}
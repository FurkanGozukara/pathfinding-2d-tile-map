using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Data;
using System.IO;
using System.Threading;
using System.Security.Cryptography;

namespace Pathfinding_Project
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool blEnableLogging = true;

        long lrProcessedPossibleLocations = 0;

        int irLongestProcessedPath = 0;

        Dictionary<int, int> dicPerLocationMaxIteration = new Dictionary<int, int>();

        private static object lock_dicPerLocationMaxIteration = new object();

        public MainWindow()
        {
            InitializeComponent();
            swWriteTempLogs.AutoFlush = true;
        }

        Dictionary<int, List<int>> dicShortestFoundPaths = new Dictionary<int, List<int>>();

        public Dictionary<Int16, Dictionary<int, byte>> dicMapFormat = new Dictionary<Int16, Dictionary<int, byte>>();//first route id, square id and shape id

        private void btnStartPathFinding_Click(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(() => { processPathFinding(); });
        }
        List<movementDirections> lstMovingDirections = new List<movementDirections>();

        private void processPathFinding()
        {
            init_MapDictionary();

            foreach (DataRow drwRouteInfo in DbConnection.db_Select_DataTable("select * from routes where routeid=23").Rows)
            {
                Int16 irRouteId = Convert.ToInt16(drwRouteInfo["routeId"].ToString());
                int routeRowCount = Convert.ToInt32(drwRouteInfo["maxRow"].ToString());
                int routeColumnCount = Convert.ToInt32(drwRouteInfo["maxColumn"].ToString());

                dicShortestFoundPaths = new Dictionary<int, List<int>>();

                lock (lock_hsCheckedPaths)
                    hsCheckedPaths = new HashSet<string>();

                lock (lock_dicPerLocationMaxIteration)
                    dicPerLocationMaxIteration = new Dictionary<int, int>();

                for (int currentLoc = 1; currentLoc < (routeRowCount * routeColumnCount); currentLoc++)
                {

                    irLongestProcessedPath = 0;

                    Dictionary<List<int>, bool> dicPerRouteMappings = new Dictionary<List<int>, bool>();

                    Application.Current.Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        lblCurrentRouteId.Content = "route id: " + irRouteId;
                    }));

                    if (currentLoc == 1880)
                    {
                        currentLoc = currentLoc;
                    }


                    Application.Current.Dispatcher.BeginInvoke(
new Action(() =>
{
    lblCurrentCoordinates.Content = "current location: " + currentLoc;
}));

                    if (dicMapFormat[irRouteId][currentLoc] == 1)//not possible to move this location
                        continue;

                    //if (dicShortestFoundPaths.ContainsKey(1507))
                    //{
                    //    string srPath = string.Join(" ", dicShortestFoundPaths[1507]);
                    //    File.WriteAllText("found.txt", srPath);
                    //    MessageBox.Show("found");
                    //    return;
                    //}

                    lock (lock_dicShortestFoundPaths)
                        if (dicShortestFoundPaths.ContainsKey(currentLoc))
                            continue;

                    //if the location is smaller than the max column count, the user can not move to up
                    //if the left over of the location divided by max column is 0, the user can not move to right
                    //if the left over of the location divided by max column is 1, the user can not move to left
                    //if the location divided by max row count +1 >= max row count, then the user can not move to down             




                    foreach (movementDirections direction in movementDirections.GetValues(typeof(movementDirections)))
                    {
                        lstMovingDirections.Add(direction);
                    }

                    //foreach (movementDirections direction in movementDirections.GetValues(typeof(movementDirections)))
                    //{
                    //    csChosenDirection chosenDirection = new csChosenDirection();

                    //    checkPossibleMovements(new List<int> { currentLoc }, dicPerRouteMappings, routeColumnCount, irRouteId, direction, routeRowCount, chosenDirection);
                    //}

                    ParallelOptions po = new ParallelOptions();
                    po.MaxDegreeOfParallelism = 4;

                    Parallel.ForEach(lstMovingDirections, po, direction =>
                    {
                        csChosenDirection chosenDirection = new csChosenDirection();

                        checkPossibleMovements(new List<int> { currentLoc }, dicPerRouteMappings, routeColumnCount, irRouteId, direction, routeRowCount, new csChosenDirection(chosenDirection));
                    });


                    //StreamWriter swWrite = new StreamWriter(currentLoc + ".txt");
                    //swWrite.AutoFlush = true;
                    //foreach (var item in dicPerRouteMappings)
                    //{
                    //    swWrite.WriteLine(String.Join(" ", item.Key) + "\t\t" + item.Value);
                    //}
                    //swWrite.Flush();
                    //swWrite.Close();

                    // break;
                }


                StringBuilder sbQueries = new StringBuilder();

                foreach (var vrPerPath in dicShortestFoundPaths)
                {
                    int irLoc = Convert.ToInt32(vrPerPath.Key);
                    int irNext = Convert.ToInt32(vrPerPath.Value[1]);

                    int irDirection = returnDirection(irLoc, irNext, routeColumnCount);
                    string srFullPath = string.Join(";", vrPerPath.Value.Skip(1));
                    sbQueries.AppendLine($"insert into ShortestPath ([RouteId],[CurrentLoc],[Direction],[FullPath]) values ({irRouteId},{vrPerPath.Key},{irDirection},'{srFullPath}');");

                    if(sbQueries.Length>100000)
                    {
                        DbConnection.db_Update_Delete_Query(sbQueries.ToString());
                        sbQueries = new StringBuilder();
                    }
                }

                DbConnection.db_Update_Delete_Query(sbQueries.ToString());

                 return;
            }
        }

        private int returnDirection(int irFirst,int irSecond,int irMaxColCount)
        {
            if (irSecond == irFirst + 1)
                return (int)movementDirections.Right;
            if (irSecond == irFirst - 1)
                return (int)movementDirections.Left;
            if (irSecond == irFirst - irMaxColCount)
                return (int)movementDirections.Top;
            if (irSecond == irFirst + irMaxColCount)
                return (int)movementDirections.Bottom;
            return 0;
        }

        public enum movementDirections
        {
            Top = 1,
            Right = 2,
            Bottom = 3,
            Left = 4
        }

        public class csChosenDirection
        {
            public bool blLeft { get; set; } = false;
            public bool blRight { get; set; } = false;
            public bool blTop { get; set; } = false;
            public bool blBottom { get; set; } = false;

            public csChosenDirection(csChosenDirection instance)
            {
                this.blBottom = instance.blBottom;
                this.blLeft = instance.blLeft;
                this.blRight = instance.blRight;
                this.blTop = instance.blTop;
            }

            public csChosenDirection()
            {

            }

        }

        private static StreamWriter swWriteTempLogs = new StreamWriter("temp_logs.txt");

        private static object lock_dicShortestFoundPaths = new object();

        private static HashSet<string> hsCheckedPaths = new HashSet<string>();

        private static object lock_hsCheckedPaths = new object();

        private void checkPossibleMovements(List<int> lstCheckLocations, Dictionary<List<int>, bool> dicPerRouteMappings, int colCount, short irRouteId, movementDirections movementDirection, int rowCount, csChosenDirection csChosenDirection)
        {
            // csChosenDirection csChosenDirection = new csChosenDirection(_csChosenDirection);//a deep copy of class type object

            if (lstCheckLocations.Contains(2076))
            {
                string gg = "";
            }

            Interlocked.Increment(ref lrProcessedPossibleLocations);

            int irOriginalLocation = lstCheckLocations.First();

            lock (lock_dicPerLocationMaxIteration)
            {
                if (dicPerLocationMaxIteration.ContainsKey(irOriginalLocation) == false)
                {
                    dicPerLocationMaxIteration.Add(irOriginalLocation, 1);
                }
                else
                {
                    dicPerLocationMaxIteration[irOriginalLocation]++;
                }
            }

            if (Interlocked.Read(ref lrProcessedPossibleLocations) % 10000 == 0)
            {
                Application.Current.Dispatcher.BeginInvoke(
        new Action(() =>
        {
            lblProcessedLocationsCount.Content = "processed path found " + Interlocked.Read(ref lrProcessedPossibleLocations).ToString("N0");
        }));
            }


            if (hsCheckedPaths.Count % 1000 == 0)
            {

                Application.Current.Dispatcher.BeginInvoke(
    new Action(() =>
    {
        lblDictionarySize.Content = "hashset size: " + hsCheckedPaths.Count;
    }));
            }

            int irLastLocation = lstCheckLocations.Last();
            int irNextTopLocation = irLastLocation - colCount;

            switch (movementDirection)
            {
                case movementDirections.Top:
                    break;
                case movementDirections.Right:
                    irNextTopLocation = irLastLocation + 1;
                    break;
                case movementDirections.Bottom:
                    irNextTopLocation = irLastLocation + colCount;
                    break;
                case movementDirections.Left:
                    irNextTopLocation = irLastLocation - 1;
                    break;
                default:
                    break;
            }

            if (lstCheckLocations.Contains(irNextTopLocation))
                return;

            if (dicMapFormat[irRouteId].ContainsKey(irNextTopLocation) == false)
            {
                //   addListToHash(lstCopyCheckLocations);
                return;
            }

            bool blNextLocationMoveable = blCheckLocationMoveable(irRouteId, irNextTopLocation);

            if (irLastLocation == 2140)
            {
                irNextTopLocation = irNextTopLocation;
            }

            switch (movementDirection)
            {
                case movementDirections.Top:
                    if (csChosenDirection.blBottom == true)
                        return;
                    if (blNextLocationMoveable == false)
                    {
                        List<int> lstFixedList = lstCheckLocations.GetRange(0, lstCheckLocations.Count).ToList();
                        int irNewAddedLoc = irLastLocation + colCount;
                        if (blCheckLocationMoveable(irRouteId, irNewAddedLoc))
                        {
                            if (lstFixedList.Contains(irNewAddedLoc) == false)
                            {
                                lstFixedList.Add(irNewAddedLoc);
                                csChosenDirection.blBottom = true;
                                csChosenDirection.blTop = false;
                                checkPossibleMovements(new List<int>(lstFixedList), dicPerRouteMappings, colCount, irRouteId, movementDirections.Bottom, rowCount, new csChosenDirection(csChosenDirection));
                                return;
                            }
                        }
                    }
                    csChosenDirection.blTop = true;
                    break;
                case movementDirections.Right:
                    if (csChosenDirection.blLeft == true)
                        return;
                    if (blNextLocationMoveable == false)
                    {
                        List<int> lstFixedList = lstCheckLocations.GetRange(0, lstCheckLocations.Count).ToList();
                        int irNewAddedLoc = irLastLocation - 1;
                        if (blCheckLocationMoveable(irRouteId, irNewAddedLoc))
                        {
                            if (lstFixedList.Contains(irNewAddedLoc) == false)
                            {
                                lstFixedList.Add(irNewAddedLoc);
                                csChosenDirection.blRight = false;
                                csChosenDirection.blLeft = true;
                                checkPossibleMovements(new List<int>(lstFixedList), dicPerRouteMappings, colCount, irRouteId, movementDirections.Left, rowCount, new csChosenDirection(csChosenDirection));
                                return;
                            }
                        }
                    }
                    csChosenDirection.blRight = true;
                    break;
                case movementDirections.Bottom:
                    if (csChosenDirection.blTop == true)
                        return;
                    if (blNextLocationMoveable == false)
                    {
                        List<int> lstFixedList = lstCheckLocations.GetRange(0, lstCheckLocations.Count).ToList();
                        int irNewAddedLoc = irLastLocation - colCount;
                        if (blCheckLocationMoveable(irRouteId, irNewAddedLoc))
                        {
                            if (lstFixedList.Contains(irNewAddedLoc) == false)
                            {
                                lstFixedList.Add(irNewAddedLoc);
                                csChosenDirection.blBottom = false;
                                csChosenDirection.blTop = true;
                                checkPossibleMovements(new List<int>(lstFixedList), dicPerRouteMappings, colCount, irRouteId, movementDirections.Top, rowCount, new csChosenDirection(csChosenDirection));
                                return;
                            }
                        }
                    }
                    csChosenDirection.blBottom = true;
                    break;
                case movementDirections.Left:
                    if (csChosenDirection.blRight == true)
                        return;
                    if (blNextLocationMoveable == false)
                    {
                        List<int> lstFixedList = lstCheckLocations.GetRange(0, lstCheckLocations.Count).ToList();
                        int irNewAddedLoc = irLastLocation + 1;
                        if (blCheckLocationMoveable(irRouteId, irNewAddedLoc))
                        {
                            if (lstFixedList.Contains(irNewAddedLoc) == false)
                            {
                                lstFixedList.Add(irNewAddedLoc);
                                csChosenDirection.blRight = true;
                                csChosenDirection.blLeft = false;
                                checkPossibleMovements(new List<int>(lstFixedList), dicPerRouteMappings, colCount, irRouteId, movementDirections.Right, rowCount, new csChosenDirection(csChosenDirection));
                                return;
                            }
                        }
                    }
                    csChosenDirection.blLeft = true;
                    break;
                default:
                    break;
            }





            List<int> lstCopyCheckLocations = new List<int>();
            lstCopyCheckLocations.AddRange(lstCheckLocations);
            lstCopyCheckLocations.Add(irNextTopLocation);

            lock (lock_dicShortestFoundPaths)
                if (dicShortestFoundPaths.ContainsKey(irOriginalLocation))
                {
                    if (lstCopyCheckLocations.Count >= dicShortestFoundPaths[irOriginalLocation].Count)
                        return;
                }

            if (lstCopyCheckLocations.Count > colCount + rowCount)
            {
                //   addListToHash(lstCopyCheckLocations);
                return;
            }

            if (dicPerRouteMappings.ContainsKey(lstCopyCheckLocations))
            {
                return;
            }



            if (blNextLocationMoveable == false)
            {
                return;
            }


            string srHashedPath = lstCopyCheckLocations.returnHashedValueOfPath();

            lock (lock_hsCheckedPaths)
            {
                if (hsCheckedPaths.Contains(srHashedPath))
                    return;
            }

            lock (lock_dicPerLocationMaxIteration)
                if (dicPerLocationMaxIteration[irOriginalLocation] > 1000000)
                {
                    lock (lock_dicShortestFoundPaths)
                        if (dicShortestFoundPaths.ContainsKey(irOriginalLocation))
                            return;
                    return;
                }

            if (lstCopyCheckLocations.Count > irLongestProcessedPath)
            {
                irLongestProcessedPath = lstCopyCheckLocations.Count;
                Dispatcher.BeginInvoke(
new Action(() =>
{
    lblLongestProcessedPath.Content = "longest processed path: " + irLongestProcessedPath;
}));
            }

            if (dicMapFormat[irRouteId][irNextTopLocation] == 3 || dicMapFormat[irRouteId][irNextTopLocation] == 11 || dicMapFormat[irRouteId][irNextTopLocation] == 15 || dicMapFormat[irRouteId][irNextTopLocation] == 19)
            {
                //   dicPerRouteMappings.Add(lstCopyCheckLocations, true);

                lock (lock_dicShortestFoundPaths)
                {
                    if (dicShortestFoundPaths.ContainsKey(irOriginalLocation) == false)
                    {
                        dicShortestFoundPaths.Add(irOriginalLocation, lstCopyCheckLocations);
                        dicPerLocationMaxIteration[irOriginalLocation] = 0;
                        printFoundShortestPaths(dicShortestFoundPaths, irRouteId);
                    }
                    else
                    {
                        if (dicShortestFoundPaths[irOriginalLocation].Count > lstCopyCheckLocations.Count)
                        {
                            dicPerLocationMaxIteration[irOriginalLocation] = 0;
                            dicShortestFoundPaths.Remove(irOriginalLocation);
                            dicShortestFoundPaths.Add(irOriginalLocation, lstCopyCheckLocations);
                            printFoundShortestPaths(dicShortestFoundPaths, irRouteId);
                        }
                    }

                    for (int i = 1; i < lstCopyCheckLocations.Count; i++)
                    {
                        List<int> lstTemp = lstCopyCheckLocations.GetRange(i, lstCopyCheckLocations.Count - i);

                        int irNewShortestLoc = lstTemp.First();

                        if (lstTemp.Count == 1)
                            continue;

                        if (dicShortestFoundPaths.ContainsKey(irNewShortestLoc) == false)
                        {
                            printFoundShortestPaths(dicShortestFoundPaths, irRouteId);
                            dicShortestFoundPaths.Add(irNewShortestLoc, lstTemp);
                            Dispatcher.BeginInvoke(
    new Action(() =>
    {
        lblfoundShortestPaths.Content = "found shortest path counts: " + dicShortestFoundPaths.Count;
    }));
                        }
                        else
                        {
                            if (dicShortestFoundPaths[irNewShortestLoc].Count > lstTemp.Count)
                            {
                                printFoundShortestPaths(dicShortestFoundPaths, irRouteId);
                                dicShortestFoundPaths.Remove(irNewShortestLoc);
                                dicShortestFoundPaths.Add(irNewShortestLoc, lstTemp);
                            }

                        }
                    }
                }

                // addListToHash(lstCopyCheckLocations);

                return;
            }

            //   dicPerRouteMappings.Add(lstCopyCheckLocations, false);

            //ParallelOptions po = new ParallelOptions();
            //po.MaxDegreeOfParallelism = 4;

            //Parallel.ForEach(lstMovingDirections, po, direction =>
            //{
            //    checkPossibleMovements(lstCopyCheckLocations, dicPerRouteMappings, colCount, irRouteId, direction, rowCount, csChosenDirection);
            //});

            if(blEnableLogging)
            {
                lock (swWriteTempLogs)
                {
                    swWriteTempLogs.WriteLine(String.Join(" ", lstCopyCheckLocations));
                }
            }
          

            addHash(lstCopyCheckLocations);

            List<Task> lstTasks = new List<Task>();

            foreach (movementDirections direction in movementDirections.GetValues(typeof(movementDirections)))
            {
                var vrtask = Task.Factory.StartNew(() => { checkPossibleMovements(new List<int>(lstCopyCheckLocations), dicPerRouteMappings, colCount, irRouteId, direction, rowCount, new csChosenDirection(csChosenDirection)); });
                lstTasks.Add(vrtask);
            }

            Task.WaitAll(lstTasks.ToArray());

        }
        private static object lock_printShortest = new object();

        private bool blCheckLocationMoveable(short irRouteId, int irNextTopLocation)
        {
            if (dicMapFormat[irRouteId].ContainsKey(irNextTopLocation) == false)
                return false;
            
            if (dicMapFormat[irRouteId][irNextTopLocation] == 1)
            {
                return false;
            }

            //if (dicMapFormat[irRouteId][irNextTopLocation] == 1 || dicMapFormat[irRouteId][irNextTopLocation] == 12 || dicMapFormat[irRouteId][irNextTopLocation] == 14)
            //{
            //    return false;
            //}
            return true;
        }
        private void printFoundShortestPaths(Dictionary<int, List<int>> dicFound, int irRouteId)
        {
            lock (lock_printShortest)
            {
                File.WriteAllLines("shortest_found_" + irRouteId + ".txt", dicFound.Select(pr => pr.Key + "\t\t" + String.Join(" ", pr.Value)));
            }

        }

        private void addListToHash(List<int> lstCheckLocations)
        {
            addHash(lstCheckLocations);

            for (int i = 1; i < lstCheckLocations.Count; i++)
            {
                List<int> lstCheckedPath = lstCheckLocations.GetRange(i, lstCheckLocations.Count - i);

                addHash(lstCheckedPath);
            }
        }

        private void addHash(List<int> lstCheckLocations)
        {
            string srHashedPath = lstCheckLocations.returnHashedValueOfPath();

            lock (lock_hsCheckedPaths)
            {
                hsCheckedPaths.Add(srHashedPath);
            }
        }

        private void init_MapDictionary()
        {
            foreach (DataRow drwRouteInfo in DbConnection.db_Select_DataTable("select routeId,squareId,shapeType from map").Rows)
            {
                Int16 irRouteId = Convert.ToInt16(drwRouteInfo["routeId"].ToString());
                int irSquareId = Convert.ToInt32(drwRouteInfo["squareId"].ToString());
                byte shapeType = Convert.ToByte(drwRouteInfo["shapeType"].ToString());

                if (dicMapFormat.ContainsKey(irRouteId) == false)
                {
                    Dictionary<int, byte> dicShapes = new Dictionary<int, byte>();
                    dicShapes.Add(irSquareId, shapeType);
                    dicMapFormat.Add(irRouteId, dicShapes);
                }
                else
                {
                    dicMapFormat[irRouteId].Add(irSquareId, shapeType);
                }
            }
        }


    }
}

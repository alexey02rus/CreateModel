using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreateModel
{
    [Transaction(TransactionMode.Manual)]
    public class Main : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            Levels levels = new Levels(doc);
            Level level1 = levels.GetLevel("Уровень 1");
            Level level2 = levels.GetLevel("Уровень 2");

            double width = 10000;
            double depth = 5000;

            List<Wall> walls = CreateWallsToRectangle(doc, width, depth, level1, level2, false);
            if (walls == null)
            {
                TaskDialog.Show("Ошибка", "Не удалось построить стены");
                return Result.Failed;
            }
            return Result.Succeeded;
        }

        public List<Wall> CreateWallsToRectangle(Document document, 
                                                 double width, 
                                                 double depth, 
                                                 Level baseLevel, 
                                                 Level upLevel, 
                                                 bool isStructural)
        {
            if (baseLevel == null || upLevel == null)
            {
                return null;
            }
            List<Wall> walls = new List<Wall>();
            if (width < 50 || depth < 50)
            {
                TaskDialog.Show("Внимание!", "Указаны слишком маленькие значения ширины или глубины");
                return null;
            }
            double widthToUnits = UnitUtils.ConvertToInternalUnits(width, UnitTypeId.Millimeters);
            double depthToUnits = UnitUtils.ConvertToInternalUnits(depth, UnitTypeId.Millimeters);
            double dx = widthToUnits / 2.0;
            double dy = depthToUnits / 2.0;
            List<XYZ> points = new List<XYZ>()
            {
                new XYZ(-dx, -dy, 0),
                new XYZ(dx, -dy, 0),
                new XYZ(dx, dy, 0),
                new XYZ(-dx, dy, 0),
                new XYZ(-dx, -dy, 0)
            };
            using(Transaction ts = new Transaction(document, "Создание контура стен"))
            {
                ts.Start();
                for (int i = 0; i < 4; i++)
                {
                    Line line = Line.CreateBound(points[i], points[i + 1]);
                    Wall wall = Wall.Create(document, line, baseLevel.Id, isStructural);
                    wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(upLevel.Id);
                    walls.Add(wall);
                }
                ts.Commit();
            }
            return walls;

        }
    }
}

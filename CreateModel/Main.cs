using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;

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
            double roofHight = 1500;

            List<Wall> walls = CreateWallsToRectangle(doc, width, depth, level1, level2, false);
            if (walls == null)
            {
                TaskDialog.Show("Ошибка", "Не удалось построить стены");
                return Result.Failed;
            }

            string doorFamilyName = "Одиночные-Щитовые";
            string doorTypeName = "0915 x 2134 мм";
            string windowFamilyName = "Фиксированные";
            string windowTypeName = "0915 x 1830 мм";
            FamilyInstance newDoor;
            List<FamilyInstance> newWindows = new List<FamilyInstance>();

            for (int i = 0; i < 4; i++)
            {
                if (i == 0)
                {
                    newDoor = CreateDoorOrWindowInCenterOfWall(doc, FamilySymbolType.Doors, doorFamilyName, doorTypeName, level1, walls[i]);
                    if (newDoor == null)
                    {
                        TaskDialog.Show("Ошибка", "Не удалось создать дверь");
                        return Result.Failed;
                    }
                }
                else
                {
                    FamilyInstance newWindow = CreateDoorOrWindowInCenterOfWall(doc, FamilySymbolType.Windows, windowFamilyName, windowTypeName, level1, walls[i], 304);
                    if (newWindow == null)
                    {
                        TaskDialog.Show("Ошибка", "Не удалось создать окна");
                        return Result.Failed;
                    }
                    newWindows.Add(newWindow);
                }
            }

            ExtrusionRoof newRoof = CreateExtrusionRoof(doc, width, depth, roofHight, level2);
            if (newRoof == null)
            {
                TaskDialog.Show("Ошибка", "Не удалось создать крышу");
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
            WallType defaultTypeWall = document.GetElement(document.GetDefaultElementTypeId(ElementTypeGroup.WallType)) as WallType;
            double offset = defaultTypeWall.Width / 2.0;
            double widthToUnits = UnitUtils.ConvertToInternalUnits(width, UnitTypeId.Millimeters);
            double depthToUnits = UnitUtils.ConvertToInternalUnits(depth, UnitTypeId.Millimeters);
            double dx = widthToUnits / 2.0 - offset;
            double dy = depthToUnits / 2.0 - offset;
            List<XYZ> points = new List<XYZ>()
            {
                new XYZ(-dx, -dy, 0),
                new XYZ(-dx, dy, 0),
                new XYZ(dx, dy, 0),
                new XYZ(dx, -dy, 0),
                new XYZ(-dx, -dy, 0)
            };
            using (Transaction ts = new Transaction(document, "Создание контура стен"))
            {
                ts.Start();
                for (int i = 0; i < 4; i++)
                {
                    Line line = Line.CreateBound(points[i], points[i + 1]);
                    Wall wall = Wall.Create(document, line, baseLevel.Id, isStructural);
                    wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(upLevel.Id);
                    wall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM).Set(2);
                    walls.Add(wall);
                }
                ts.Commit();
            }
            return walls;

        }

        public FamilyInstance CreateDoorOrWindowInCenterOfWall(Document document,
                                                       FamilySymbolType familySymbolType,
                                                       string familyName,
                                                       string typeName,
                                                       Level level,
                                                       Wall wall,
                                                       double offset = 0)
        {

            FamilySymbol familySymbol = new FilteredElementCollector(document)
                .OfClass(typeof(FamilySymbol))
                .OfCategory((BuiltInCategory)familySymbolType)
                .OfType<FamilySymbol>()
                .Where(d => d.FamilyName.Equals(familyName))
                .Where(d => d.Name.Equals(typeName))
                .FirstOrDefault();

            if (familySymbol == null || wall == null || level == null)
            {
                return null;
            }

            LocationCurve wallCurve = wall.Location as LocationCurve;
            XYZ centerPoint = wallCurve.Curve.Evaluate(0.5, true);

            if (!familySymbol.IsActive)
            {
                familySymbol.Activate();
            }

            FamilyInstance newFamilyInstance;
            using (Transaction ts = new Transaction(document, "Создание дверей/окон"))
            {
                ts.Start();
                newFamilyInstance = document.Create.NewFamilyInstance(centerPoint, familySymbol, wall, level, StructuralType.NonStructural);
                newFamilyInstance.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM)
                                 .Set(UnitUtils.ConvertToInternalUnits(offset, UnitTypeId.Millimeters));
                ts.Commit();
            }
            return newFamilyInstance;
        }

        public enum FamilySymbolType
        {
            Doors = BuiltInCategory.OST_Doors,
            Windows = BuiltInCategory.OST_Windows
        }

        public ExtrusionRoof CreateExtrusionRoof(Document document,
                                                 double width,
                                                 double depth,
                                                 double hight,
                                                 Level baseLevel)
        {
            RoofType roofType = new FilteredElementCollector(document)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(r => r.Name.Equals("Типовой - 400мм"))
                .FirstOrDefault();
            if (roofType == null || baseLevel == null)
            {
                return null;
            }
            double offset = UnitUtils.ConvertToInternalUnits(200, UnitTypeId.Millimeters); ;
            double dY = UnitUtils.ConvertToInternalUnits(depth / 2.0, UnitTypeId.Millimeters);
            double dX = UnitUtils.ConvertToInternalUnits(width / 2.0, UnitTypeId.Millimeters);
            double dZ = baseLevel.Elevation + offset;
            double dH = dZ + UnitUtils.ConvertToInternalUnits(hight, UnitTypeId.Millimeters);

            CurveArray sketchRoof = new CurveArray();
            sketchRoof.Append(Line.CreateBound(new XYZ(0, dY, dZ), new XYZ(0, 0, dH)));
            sketchRoof.Append(Line.CreateBound(new XYZ(0, 0, dH), new XYZ(0, -dY, dZ)));

            ExtrusionRoof extrusionRoof;
            using (Transaction ts = new Transaction(document))
            {
                ts.Start("Создание крыши");
                ReferencePlane plane = document.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 1), new XYZ(0, 1, 0), document.ActiveView);
                extrusionRoof = document.Create.NewExtrusionRoof(sketchRoof, plane, baseLevel, roofType, -dX, dX);
                extrusionRoof.get_Parameter(BuiltInParameter.ROOF_CONSTRAINT_OFFSET_PARAM).Set(UnitUtils.ConvertToInternalUnits(offset, UnitTypeId.Millimeters));
                extrusionRoof.get_Parameter(BuiltInParameter.ROOF_EAVE_CUT_PARAM).Set(33619);
                extrusionRoof.get_Parameter(BuiltInParameter.FASCIA_DEPTH_PARAM).Set(offset);
                ts.Commit();
            }
            return extrusionRoof;
        }
    }
}

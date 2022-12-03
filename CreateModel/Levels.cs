using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;

namespace CreateModel
{
    public class Levels
    {
        public List<Level> listLevel { get; set; } = new List<Level>();

        public Levels(Document document)
        {
            listLevel = new FilteredElementCollector(document)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();
        }

        public Level GetLevel(string levelName)
        {
            Level level = listLevel.Where(lvl => lvl.Name.Equals(levelName)).FirstOrDefault();
            if (level == null)
            {
                TaskDialog.Show("Внимание!", $"Не найден уровень с именем {levelName}");
            }
            return level;
        }
    }
}

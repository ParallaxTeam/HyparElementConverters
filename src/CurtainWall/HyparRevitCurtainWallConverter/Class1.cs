using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ADSK = Autodesk.Revit.DB;
using Elements.Conversion.Revit;
using Element = Elements.Element;

namespace HyparRevitCurtainWallConverter
{
    public class CurtainWallConverter : IRevitConverter
    {
        public ADSK.FilteredElementCollector AddElementFilters(ADSK.FilteredElementCollector collector)
        {
            //collect walls that are instance based on not in-place families
            return collector.OfCategory(ADSK.BuiltInCategory.OST_Walls).WhereElementIsNotElementType().WherePasses(new ADSK.ElementClassFilter(typeof(ADSK.Wall)));
        }

        public Element[] FromRevit(ADSK.Element revitElement, ADSK.Document document)
        {
            throw new NotImplementedException();
        }

        public Element[] FromRevit(Element revitElement, ADSK.Document document)
        {
            throw new NotImplementedException();
        }

        public ADSK.ElementId[] ToRevit(Element hyparElement, LoadContext context)
        {
            throw new NotImplementedException();
        }

        public Element[] OnlyLoadableElements(Element[] allElements)
        {
            throw new NotImplementedException();
        }

        public bool CanConvertToRevit { get; }
        public bool CanConvertFromRevit { get; }
    }
}

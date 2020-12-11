using System;
using System.Linq;
using ADSK = Autodesk.Revit.DB;
using Elements.Conversion.Revit;
using Element = Elements.Element;

namespace HyparRevitCurtainWallConverter
{
    public class CurtainWallConverter : IRevitConverter
    {
        public bool CanConvertToRevit => false;
        public bool CanConvertFromRevit => true;

        public ADSK.FilteredElementCollector AddElementFilters(ADSK.FilteredElementCollector collector)
        {
            //pre-collect curtain walls by seeing if they have a curtain grid. also exclude in-place families.
            var curtainWallIds = collector.OfCategory(ADSK.BuiltInCategory.OST_Walls).WhereElementIsNotElementType().WherePasses(new ADSK.ElementClassFilter(typeof(ADSK.Wall))).Cast<ADSK.Wall>().Where(w => w.CurtainGrid != null).Select(w => w.Id).ToArray();

            //generate a revit filter based on those ids
            ADSK.ElementFilter curtainWallIdFilter = new ADSK.ElementIdSetFilter(curtainWallIds);

            //collect the curtain walls using the filter
            return collector.OfCategory(ADSK.BuiltInCategory.OST_Walls).WherePasses(curtainWallIdFilter);
        }

        public Element[] FromRevit(ADSK.Element revitElement, ADSK.Document document)
        {
            
            return Create.MakeHyparCurtainWallFromRevitCurtainWall(revitElement, document).ToArray();
        }

        public ADSK.ElementId[] ToRevit(Element hyparElement, LoadContext context)
        {
            throw new NotImplementedException();
        }

        public Element[] OnlyLoadableElements(Element[] allElements)
        {
            throw new NotImplementedException();
        }

      
    }
}

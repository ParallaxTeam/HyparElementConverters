using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elements;
using ADSK = Autodesk.Revit.DB;
using Elements.Conversion.Revit;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
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
            return MakeHyparCurtainWallFromRevitCurtainWall(revitElement);
        }

        public ADSK.ElementId[] ToRevit(Element hyparElement, LoadContext context)
        {
            throw new NotImplementedException();
        }

        public Element[] OnlyLoadableElements(Element[] allElements)
        {
            throw new NotImplementedException();
        }

        private static Element[] MakeHyparCurtainWallFromRevitCurtainWall(ADSK.Element revitElement)
        {
            var doc = revitElement.Document;
            var curtainWall = revitElement as ADSK.Wall;

            if (curtainWall.CurtainGrid == null)
            {
                throw new InvalidOperationException("This curtain wall does not have a grid. Curtain walls with no grid are not supported at this time.");
            }

            var uGridLines = curtainWall.CurtainGrid.GetUGridLineIds()
                .Select(c => doc.GetElement(c) as ADSK.CurtainGridLine).ToArray();

            var lines = uGridLines.Select(u =>
                new Line(u.FullCurve.GetEndPoint(0).ToVector3(), u.FullCurve.GetEndPoint(1).ToVector3())).ToArray();

            var modelCurves = new List<ModelCurve>(); 
            foreach (var uGrid in uGridLines)
            {
                var fullCurve = uGrid.FullCurve;

               Curve curve = new Line(fullCurve.GetEndPoint(0).ToVector3(), fullCurve.GetEndPoint(1).ToVector3());

               modelCurves.Add(new ModelCurve(curve));
            }

            return modelCurves.ToArray();

        }
    }
}

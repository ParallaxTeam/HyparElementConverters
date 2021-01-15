using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elements;
using Elements.Conversion.Revit;
using Elements.Geometry;
using ADSK = Autodesk.Revit.DB;


namespace HyparRevitRoofConverter
{
    public class RoofConverter : IRevitConverter
    {
        public bool CanConvertToRevit => false;
        public bool CanConvertFromRevit => true;

        public ADSK.FilteredElementCollector AddElementFilters(ADSK.FilteredElementCollector collector)
        {
            //allows us to exclude in place roofs.
            ADSK.ElementClassFilter classFilter = new ADSK.ElementClassFilter(typeof(ADSK.FamilyInstance), true);
            return collector.OfCategory(ADSK.BuiltInCategory.OST_Roofs).WherePasses(classFilter);
        }

        public Element[] FromRevit(ADSK.Element revitElement, ADSK.Document document)
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

        private static Element[] HyparRoofFromRevitRoof(ADSK.Element revitRoof)
        {
            if (revitRoof is ADSK.FootPrintRoof footprintRoof)
            {
                var bottomFaceReferences = ADSK.HostObjectUtils.GetBottomFaces(footprintRoof);

                var bottomFaces = bottomFaceReferences.Select(r => footprintRoof.GetGeometryObjectFromReference(r)).Cast<Autodesk.Revit.DB.Face>().ToList();



                Mesh mesh = new Mesh();

            }

            return new Element[]{};
        }
    }
}

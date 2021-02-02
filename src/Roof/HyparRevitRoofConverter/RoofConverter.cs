using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Elements;
using Elements.Conversion.Revit;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using Elements.Geometry.Solids;
using ADSK = Autodesk.Revit.DB;
using Vertex = Elements.Geometry.Vertex;


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
            return HyparRoofFromRevitRoof(revitElement);
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
            var returnList = new List<Element>();

            ADSK.Document doc = revitRoof.Document;
            var bBox = revitRoof.get_BoundingBox(null);

            double elevation = Units.FeetToMeters(bBox.Min.Z);
            double highPoint = Units.FeetToMeters(bBox.Max.Z);
            double thickness = Units.FeetToMeters(revitRoof.get_Parameter(ADSK.BuiltInParameter.ROOF_ATTR_THICKNESS_PARAM).AsDouble()); //works for both
            double area = Units.FeetToMeters(revitRoof.get_Parameter(ADSK.BuiltInParameter.HOST_AREA_COMPUTED).AsDouble()); //works for both

            Mesh topside = null;
            Mesh underside = null;
            Mesh envelope = null;
            Polygon outerPerimeter = null;

            if (revitRoof is ADSK.FootPrintRoof footprintRoof)
            {
                //create topside
                var topReferences = ADSK.HostObjectUtils.GetTopFaces(footprintRoof);
                var topFaces = topReferences.Select(r => footprintRoof.GetGeometryObjectFromReference(r)).ToList();
                topside = Create.FacesToMesh(topFaces);

                //create underside
                var bottomReferences = ADSK.HostObjectUtils.GetBottomFaces(footprintRoof);
                var bottomFaces = bottomReferences.Select(r => footprintRoof.GetGeometryObjectFromReference(r)).ToList();
                underside = Create.FacesToMesh(bottomFaces);

                //create whole envelope
                var faces = footprintRoof.ExtractRoofFaces();
                envelope = Create.FacesToMesh(faces);
            }

            if (revitRoof is ADSK.ExtrusionRoof extrusionRoof)
            {
                //create topside and underside
                topside = extrusionRoof.ProfileRoofToMesh();
                underside = extrusionRoof.ProfileRoofToMesh(false);
                
                //build whole envelope
                var faces = extrusionRoof.ExtractRoofFaces();
                envelope = Create.FacesToMesh(faces);
            }
            //get the perimeter (same for both roof types)
            outerPerimeter = ((ADSK.RoofBase)revitRoof).ExtractRoofFootprint().First();

            //build the roof
            Roof hyparRoof = new Roof(envelope, topside, underside, outerPerimeter, elevation, highPoint, thickness, area, new Transform(), BuiltInMaterials.Black, null, false, Guid.NewGuid(), "Roof");

            //create mesh element for visualization
            returnList.Add(new MeshElement(envelope, BuiltInMaterials.Default));

            foreach (var line in outerPerimeter.Segments())
            {
                returnList.Add(new ModelCurve(line));
            }

            returnList.Add(hyparRoof);
            return returnList.ToArray();
        }

    }
}

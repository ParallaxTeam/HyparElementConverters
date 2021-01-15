using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elements;
using Elements.Conversion.Revit;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using Elements.Geometry.Solids;
using GeometryEx;
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
            var level = revitRoof.Document.GetElement(revitRoof.LevelId) as ADSK.Level;
            double levelElevation = level.Elevation;
            double baseOffset = revitRoof.get_Parameter(ADSK.BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM).AsDouble();

            double elevation = Units.FeetToMeters(levelElevation + baseOffset);
            double highPoint = 0;
            double thickness = Units.FeetToMeters(revitRoof.get_Parameter(ADSK.BuiltInParameter.ROOF_ATTR_THICKNESS_PARAM).AsDouble());
            double area = Units.FeetToMeters(revitRoof.get_Parameter(ADSK.BuiltInParameter.HOST_AREA_COMPUTED).AsDouble());

            Mesh underside = null;
            Mesh topside = null;
            Polygon outerPerimeter = null;
            if (revitRoof is ADSK.FootPrintRoof footprintRoof)
            {
                highPoint = Units.FeetToMeters(footprintRoof.get_Parameter(ADSK.BuiltInParameter.ACTUAL_MAX_RIDGE_HEIGHT_PARAM)
                    .AsDouble());

                var bottomFaceReferences = ADSK.HostObjectUtils.GetBottomFaces(footprintRoof);
                var bottomFaces = bottomFaceReferences.Select(r => footprintRoof.GetGeometryObjectFromReference(r)).Cast<Autodesk.Revit.DB.Face>().ToList();
                
                underside = MeshFromFaces(bottomFaces);

                var topFaceReferences = ADSK.HostObjectUtils.GetTopFaces(footprintRoof);
                var topFaces = topFaceReferences.Select(r => footprintRoof.GetGeometryObjectFromReference(r)).Cast<Autodesk.Revit.DB.Face>().ToList();
                topside = MeshFromFaces(topFaces);

                outerPerimeter = ToPolygon(footprintRoof.GetProfiles()).First();
            }

            Roof hyparRoof = new Roof(new Mesh(), topside, underside, outerPerimeter, elevation, highPoint, thickness, area, new Transform(), BuiltInMaterials.Black,null,false, Guid.NewGuid(),"");

            return new List<Element>() { hyparRoof }.ToArray();
        }

        private static Mesh MeshFromFaces(List<ADSK.Face> faces)
        {
            List<Vertex> vertices = new List<Vertex>();
            List<Triangle> triangles = new List<Triangle>();

            foreach (var face in faces)
            {
               
                var revitMesh = face.Triangulate();
                
                vertices.AddRange(revitMesh.Vertices.Select(v => v.ToElementsVertex()));

                for (int i = 0; i < revitMesh.NumTriangles; i++)
                {
                    var revitTriangle = revitMesh.get_Triangle(i);
                    Triangle t = new Triangle(revitTriangle.get_Vertex(0).ToElementsVertex(),
                        revitTriangle.get_Vertex(1).ToElementsVertex(),
                        revitTriangle.get_Vertex(2).ToElementsVertex());
                    triangles.Add(t);
                }
            }

            return new Mesh(vertices, triangles, BuiltInMaterials.Black);
        }
        private static Polygon[] ToPolygon(ADSK.ModelCurveArrArray value)
        {
            var count = value.Size;
            var list = new Polygon[count];

            int index = 0;
            foreach (var curveArray in value.Cast<ADSK.ModelCurveArray>())
            {
                List<Vector3> vertices = new List<Vector3>();

                foreach (var curve in curveArray.Cast<ADSK.ModelCurve>())
                    vertices.Add(curve.GeometryCurve.GetEndPoint(0).ToVector3(true));

                var polycurve = new Polygon(vertices);
               
                list[index++] = polycurve;
            }

            return list;
        }
    }
}

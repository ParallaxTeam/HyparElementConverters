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

            Mesh underside = new Mesh();
            Mesh topside = new Mesh();
            Mesh envelope = new Mesh();
            Polygon outerPerimeter = null;
            if (revitRoof is ADSK.FootPrintRoof footprintRoof)
            {
                highPoint = Units.FeetToMeters(footprintRoof.get_Parameter(ADSK.BuiltInParameter.ACTUAL_MAX_RIDGE_HEIGHT_PARAM)
                    .AsDouble());

                var bottomFaceReferences = ADSK.HostObjectUtils.GetBottomFaces(footprintRoof);
                var bottomFaces = bottomFaceReferences.Select(r => footprintRoof.GetGeometryObjectFromReference(r)).Cast<Autodesk.Revit.DB.Face>().ToList();
                foreach (ADSK.Face face in bottomFaces)
                {
                    var revitMesh = face.Triangulate(1);
                    foreach (var v in revitMesh.Vertices)
                    {
                        Vertex vertex = new Vertex(v.ToVector3(true));
                        underside.AddVertex(vertex);
                    }
                }

                var topFaceReferences = ADSK.HostObjectUtils.GetTopFaces(footprintRoof);
                var topFaces = topFaceReferences.Select(r => footprintRoof.GetGeometryObjectFromReference(r)).Cast<Autodesk.Revit.DB.Face>().ToList();
                foreach (ADSK.Face face in topFaces)
                {
                    var revitMesh = face.Triangulate(1);
                    foreach (var v in revitMesh.Vertices)
                    {
                        Vertex vertex = new Vertex(v.ToVector3(true));
                        topside.AddVertex(vertex);
                    }
                }


                var perimeter = topside.EdgesPerimeters().First();
                var ePoints = new List<Vector3>();
                perimeter.ForEach(e => ePoints.AddRange(e.Points()));
                ePoints = ePoints.Distinct().ToList();
                var uPoints = new List<Vector3>();
                ePoints.ForEach(p => uPoints.Add(new Vector3(p.X, p.Y, elevation)));

                // Use the topSide Mesh's edgePoints and the lower Mesh's underPoints
                // to construct a series of triangles forming the sides of the Roof.
                var sideTriangles = new List<Elements.Geometry.Triangle>();
                for (var i = 0; i < ePoints.Count; i++)
                {
                    sideTriangles.Add(
                        new Elements.Geometry.Triangle(new Vertex(ePoints[i]),
                            new Vertex(uPoints[i]),
                            new Vertex(uPoints[(i + 1) % uPoints.Count])));
                    sideTriangles.Add(
                        new Elements.Geometry.Triangle(new Vertex(ePoints[i]),
                            new Vertex(uPoints[(i + 1) % uPoints.Count]),
                            new Vertex(ePoints[(i + 1) % ePoints.Count])));
                }

                envelope.AddTriangles(sideTriangles.ToList());
                envelope.ComputeNormals();
                outerPerimeter = ToPolygon(footprintRoof.GetProfiles()).First();

            }

            //topside.ComputeNormals();
            //underside.ComputeNormals();
           
            Roof hyparRoof = new Roof(envelope, topside, underside, outerPerimeter, elevation, highPoint, thickness, area, new Transform(), BuiltInMaterials.Black,null,false, Guid.NewGuid(),"Roof");

            return new List<Element>() { hyparRoof }.ToArray();
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

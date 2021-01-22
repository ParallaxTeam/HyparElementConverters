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
            ADSK.Document doc = revitRoof.Document;
            var level = doc.GetElement(revitRoof.LevelId) as ADSK.Level;
            double levelElevation = level.Elevation;
            double baseOffset = revitRoof.get_Parameter(ADSK.BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM).AsDouble();

            double elevation = Units.FeetToMeters(levelElevation + baseOffset);
            double highPoint = 0;
            double thickness = Units.FeetToMeters(revitRoof.get_Parameter(ADSK.BuiltInParameter.ROOF_ATTR_THICKNESS_PARAM).AsDouble());
            double area = Units.FeetToMeters(revitRoof.get_Parameter(ADSK.BuiltInParameter.HOST_AREA_COMPUTED).AsDouble());


            Mesh topside = new Mesh();
            Mesh underside = new Mesh();
            Mesh envelope = new Mesh();

            Polygon outerPerimeter = null;
            if (revitRoof is ADSK.FootPrintRoof footprintRoof)
            {
                highPoint = Units.FeetToMeters(footprintRoof.get_Parameter(ADSK.BuiltInParameter.ACTUAL_MAX_RIDGE_HEIGHT_PARAM)
                    .AsDouble());

                #region Topside
                var topReferences = ADSK.HostObjectUtils.GetTopFaces(footprintRoof);
                var topFaces = topReferences.Select(r => footprintRoof.GetGeometryObjectFromReference(r)).Cast<Autodesk.Revit.DB.Face>().ToList();
                foreach (var face in topFaces)
                {
                    var currentRevitMesh = face.Triangulate();

                    for (int i = 0; i < currentRevitMesh.NumTriangles; i++)
                    {
                        var revitTriangle = currentRevitMesh.get_Triangle(i);

                        Triangle tri = new Triangle(
                            new Vertex(revitTriangle.get_Vertex(0).ToVector3(true)),
                            new Vertex(revitTriangle.get_Vertex(1).ToVector3(true)),
                            new Vertex(revitTriangle.get_Vertex(2).ToVector3(true))
                        );
                        topside.AddTriangle(tri);
                    }
                }
                //topside.ComputeNormals();
                #endregion

                #region bottom
                var bottomReferences = ADSK.HostObjectUtils.GetBottomFaces(footprintRoof);
                var bottomFaces = bottomReferences.Select(r => footprintRoof.GetGeometryObjectFromReference(r)).Cast<Autodesk.Revit.DB.Face>().ToList();
                foreach (var face in bottomFaces)
                {
                    var currentRevitMesh = face.Triangulate();

                    for (int i = 0; i < currentRevitMesh.NumTriangles; i++)
                    {
                        var revitTriangle = currentRevitMesh.get_Triangle(i);
                        
                        Triangle tri = new Triangle(
                            new Vertex(revitTriangle.get_Vertex(0).ToVector3(true)),
                            new Vertex(revitTriangle.get_Vertex(1).ToVector3(true)),
                            new Vertex(revitTriangle.get_Vertex(2).ToVector3(true))
                        );
                        underside.AddTriangle(tri);
                    }
                }
                //underside.ComputeNormals();
                #endregion

                outerPerimeter = ToPolygon(footprintRoof.GetProfiles()).First();
            }

            envelope = MakeEnvelope(doc);

            Roof hyparRoof = new Roof(envelope, topside, underside, outerPerimeter, elevation, highPoint, thickness, area, new Transform(), BuiltInMaterials.Black,null,false, Guid.NewGuid(),"Roof");

            return new List<Element>() { hyparRoof }.ToArray();
        }


        private static Mesh MakeEnvelope(ADSK.Document doc)
        {
            //for testing we will just pick the horizontal faces for the envelope

            Mesh envelope = new Mesh();
            UIDocument uiDoc = new UIDocument(doc);
            var references = uiDoc.Selection.PickObjects(ObjectType.Face);

            var faces = references.Select(r => doc.GetElement(r).GetGeometryObjectFromReference(r)).Cast<ADSK.PlanarFace>().ToList();

            foreach (var f in faces)
            {
                var currentRevitMesh = f.Triangulate();

                for (int i = 0; i < currentRevitMesh.NumTriangles; i++)
                {
                    var revitTriangle = currentRevitMesh.get_Triangle(i);

                    Triangle tri = new Triangle(
                        new Vertex(revitTriangle.get_Vertex(0).ToVector3(true)),
                        new Vertex(revitTriangle.get_Vertex(1).ToVector3(true)),
                        new Vertex(revitTriangle.get_Vertex(2).ToVector3(true))
                    );
                    envelope.AddTriangle(tri);
                }
            }

            return envelope;
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

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
                foreach (ADSK.PlanarFace face in topFaces)
                {
                    //get outer curve loop
                    var outerLoop = face.GetEdgesAsCurveLoops().First();

                    var vertices = outerLoop.Select(c => c.GetEndPoint(0).ToVector3(true)).ToList();

                    var currentMesh = new Polygon(vertices).ToMesh();

                    topside.AddTriangles(currentMesh.Triangles.ToList());
                }

                topside.ComputeNormals();
                #endregion

                #region bottom
                var bottomReferences = ADSK.HostObjectUtils.GetBottomFaces(footprintRoof);
                var bottomFaces = bottomReferences.Select(r => footprintRoof.GetGeometryObjectFromReference(r)).Cast<Autodesk.Revit.DB.Face>().ToList();
                foreach (ADSK.PlanarFace face in bottomFaces)
                {
                    //get outer curve loop
                    var outerLoop = face.GetEdgesAsCurveLoops().First();

                    var vertices = outerLoop.Select(c => c.GetEndPoint(0).ToVector3(true)).ToList();

                    var currentMesh = new Polygon(vertices).ToMesh();

                    topside.AddTriangles(currentMesh.Triangles.ToList());
                }
                underside.ComputeNormals();
                
                #endregion

                outerPerimeter = ToPolygon(footprintRoof.GetProfiles()).First();
            }


            var returnList = new List<Element>();

            //envelope = MakeEnvelope(doc);

            Roof hyparRoof = new Roof(envelope, topside, underside, outerPerimeter, elevation, highPoint, thickness, area, new Transform(), BuiltInMaterials.Black,null,false, Guid.NewGuid(),"Roof");


            returnList.Add(new MeshElement(topside));
            returnList.Add(new MeshElement(underside));
            returnList.Add(new MeshElement(envelope));
            returnList.Add(hyparRoof);

            return returnList.ToArray();
        }


        private static Mesh MakeEnvelope(ADSK.Document doc)
        {
            //for testing we will just pick the horizontal faces for the envelope

            Mesh envelope = new Mesh();
            UIDocument uiDoc = new UIDocument(doc);
            var references = uiDoc.Selection.PickObjects(ObjectType.Face);

            var faces = references.Select(r => doc.GetElement(r).GetGeometryObjectFromReference(r)).Cast<ADSK.PlanarFace>().ToList();

            foreach (var face in faces)
            {
                //get outer curve loop
                var outerLoop = face.GetEdgesAsCurveLoops().First();

                var vertices = outerLoop.Select(c => c.GetEndPoint(0).ToVector3(true)).ToList();

                var currentMesh = new Polygon(vertices).ToMesh(true);

                envelope.AddTriangles(currentMesh.Triangles.ToList());
            }
            envelope.ComputeNormals();
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
        private static Polygon[] ToPolygon(ADSK.EdgeArrayArray value)
        {
            var count = value.Size;
            var list = new Polygon[count];

            int index = 0;
            foreach (var edgeArray in value.Cast<ADSK.EdgeArray>())
            {
                List<Vector3> vertices = new List<Vector3>();

                foreach (var curve in edgeArray.Cast<ADSK.Edge>())
                    vertices.Add(curve.AsCurve().GetEndPoint(0).ToVector3(true));

                var polycurve = new Polygon(vertices);

                list[index++] = polycurve;
            }

            return list;
        }
        public static List<List<T>> Chunk<T>(IEnumerable<T> data, int size)
        {
            return data
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / size)
                .Select(x => x.Select(v => v.Value).ToList())
                .ToList();
        }


    }
}

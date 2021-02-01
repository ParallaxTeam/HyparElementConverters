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
            var returnList = new List<Element>();

            ADSK.Document doc = revitRoof.Document;
            double levelElevation = 0;
            

            

            double elevation = 0;
            double highPoint = 0;
            double thickness = Units.FeetToMeters(revitRoof.get_Parameter(ADSK.BuiltInParameter.ROOF_ATTR_THICKNESS_PARAM).AsDouble());
            double area = Units.FeetToMeters(revitRoof.get_Parameter(ADSK.BuiltInParameter.HOST_AREA_COMPUTED).AsDouble());

            Mesh topside = null;
            Mesh underside = null;
            Mesh envelope = new Mesh();
            Polygon outerPerimeter = null;

            if (revitRoof is ADSK.FootPrintRoof footprintRoof)
            {
                var level = doc.GetElement(revitRoof.LevelId) as ADSK.Level;
                levelElevation = level.Elevation;
                double baseOffset = revitRoof.get_Parameter(ADSK.BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM).AsDouble();
                elevation = Units.FeetToMeters(levelElevation + baseOffset);
                highPoint = Units.FeetToMeters(footprintRoof.get_Parameter(ADSK.BuiltInParameter.ACTUAL_MAX_RIDGE_HEIGHT_PARAM)
                    .AsDouble());

                //create topside
                var topReferences = ADSK.HostObjectUtils.GetTopFaces(footprintRoof);
                var topFaces = topReferences.Select(r => footprintRoof.GetGeometryObjectFromReference(r)).ToList();
                topside = Create.FacesToMesh(topFaces);

                //create underside
                var bottomReferences = ADSK.HostObjectUtils.GetBottomFaces(footprintRoof);
                var bottomFaces = bottomReferences.Select(r => footprintRoof.GetGeometryObjectFromReference(r)).ToList();
                underside = Create.FacesToMesh(bottomFaces);

                outerPerimeter = ToPolygon(footprintRoof.GetProfiles()).First();
            }

            if (revitRoof is ADSK.ExtrusionRoof extrusionRoof)
            {
                var topfaces = new List<ADSK.GeometryObject>{};
                var bottomFaces = new List<ADSK.GeometryObject> { };
                var geoElement = extrusionRoof.get_Geometry(new ADSK.Options());

                foreach (var geoObj in geoElement)
                {
                    if (geoObj is ADSK.Solid solid)
                    {
                        foreach (ADSK.Face face in solid.Faces)
                        {
                            var normal = face.ComputeNormal(new ADSK.UV(0.5, 0.5));
                            if (normal.Z > 0)
                            {
                                topfaces.Add(face);
                            }

                            if (normal.Z < 0)
                            {
                                bottomFaces.Add(face);
                            }
                        }
                        
                    }
                }

                topside = Create.FacesToMesh(topfaces);
                underside = Create.FacesToMesh(bottomFaces);
                //outerPerimeter = underside.PolygonBoundary();
            }

           envelope = Create.BuildEnvelope(outerPerimeter, thickness);


          


            Roof hyparRoof = new Roof(envelope, topside, underside, outerPerimeter, elevation, highPoint, thickness, area, new Transform(), BuiltInMaterials.Black, null, false, Guid.NewGuid(), "Roof");

            returnList.Add(new MeshElement(topside, BuiltInMaterials.Concrete));
            returnList.Add(new MeshElement(underside, BuiltInMaterials.Concrete));
            returnList.Add(new MeshElement(envelope, BuiltInMaterials.Black));
            //returnList.Add(hyparRoof);

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
                var currentMesh = face.Triangulate(1);
                for (int i = 0; i < currentMesh.NumTriangles; i++)
                {
                    var tri = currentMesh.get_Triangle(i);

                    var index0 = new Vertex(tri.get_Vertex(0).ToVector3(true));
                    var index1 = new Vertex(tri.get_Vertex(1).ToVector3(true));
                    var index2 = new Vertex(tri.get_Vertex(2).ToVector3(true));

                    envelope.AddVertex(index0);
                    envelope.AddVertex(index1);
                    envelope.AddVertex(index2);

                    envelope.AddTriangle(index0, index1, index2);
                }
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
 
    }
}

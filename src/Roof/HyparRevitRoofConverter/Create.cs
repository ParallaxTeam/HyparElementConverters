using Elements;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using System.Collections.Generic;
using System.Linq;
using ADSK = Autodesk.Revit.DB;

namespace HyparRevitRoofConverter
{
    public static class Create
    {
        public static Mesh FacesToMesh(List<ADSK.GeometryObject> faces)
        {
            Mesh mesh = new Mesh();
            foreach (var geo in faces)
            {
                if (geo is ADSK.Face face)
                {
                    //convert the face to a mesh. if it is a conical face, we use a lower precision
                    var currentMesh = face is ADSK.ConicalFace ? face.Triangulate(0.25) : face.Triangulate(1); ;

                    for (int i = 0; i < currentMesh.NumTriangles; i++)
                    {
                        var tri = currentMesh.get_Triangle(i);

                        var index0 = new Vertex(tri.get_Vertex(0).ToVector3(true));
                        var index1 = new Vertex(tri.get_Vertex(1).ToVector3(true));
                        var index2 = new Vertex(tri.get_Vertex(2).ToVector3(true));

                        mesh.AddVertex(index0);
                        mesh.AddVertex(index1);
                        mesh.AddVertex(index2);

                        mesh.AddTriangle(index0, index1, index2);
                    }
                }
            }
            mesh.ComputeNormals();

            return mesh;
        }

        public static List<ADSK.GeometryObject> ExtractRoofFaces(this ADSK.RoofBase roof)
        {
            var faces = new List<ADSK.GeometryObject>();
            var geoElement = roof.get_Geometry(new ADSK.Options());

            foreach (var geoObj in geoElement)
            {
                if (geoObj is ADSK.Solid solid)
                {
                    foreach (ADSK.Face face in solid.Faces)
                    {
                        faces.Add(face);
                    }
                }
            }
            return faces;
        }

        public static Polygon[] ExtractRoofFootprint(this ADSK.RoofBase roof)
        {
            List<Polygon> polygons = new List<Polygon>();
            var minimumPoint = roof.get_BoundingBox(null).Min;
            var plane = ADSK.Plane.CreateByNormalAndOrigin(ADSK.XYZ.BasisZ, minimumPoint);

            var geoElement = roof.get_Geometry(new ADSK.Options());
            foreach (var geoObj in geoElement)
            {
                if (geoObj is ADSK.Solid solid)
                {
                    var extrusionAnalyze = ADSK.ExtrusionAnalyzer.Create(solid, plane, ADSK.XYZ.BasisZ);
                    var face = extrusionAnalyze.GetExtrusionBase();
                    var outerCurves = face.GetEdgesAsCurveLoops().First();

                    List<Vector3> vertices = new List<Vector3>();
                    foreach (var c in outerCurves)
                    {
                        switch (c.GetType().ToString())
                        {
                            case "Autodesk.Revit.DB.Arc":
                                vertices.AddRange(c.Tessellate().Select(p => p.ToVector3(true)));
                                break;

                            case "Autodesk.Revit.DB.HermiteSpline":
                                vertices.AddRange(c.Tessellate().Select(p => p.ToVector3(true)));
                                break;

                            default:
                                vertices.Add(c.GetEndPoint(0).ToVector3(true));
                                break;
                        }
                    }
                    polygons.Add(new Polygon(vertices.Distinct().ToList()));
                }
            }

            return polygons.ToArray();
        }

        //this method is special to extrusion (profile) roofs. we use the profile lines to find the top surface or the bottom surface.
        public static Mesh ProfileRoofToMesh(this ADSK.ExtrusionRoof roof, bool top = true)
        {
            //to store the faces
            var faces = new List<ADSK.GeometryObject>();

            //extract profile curves.
            var profileCurves = new List<ADSK.ModelCurve>();
            foreach (ADSK.ModelCurve modelCurve in roof.GetProfile())
            {
                profileCurves.Add(modelCurve);
            }

            //iterate through the solids, then the faces.
            var geoElement = roof.get_Geometry(new ADSK.Options());
            foreach (var geoObj in geoElement)
            {
                if (geoObj is ADSK.Solid solid)
                {
                    foreach (ADSK.Face face in solid.Faces)
                    {
                        foreach (var curve in profileCurves)
                        {
                            var geoCurve = curve.GeometryCurve;
                            if (!top)
                            {
                                var translate = ADSK.Transform.CreateTranslation(new ADSK.XYZ(0, 0, -
                                    roof.get_Parameter(ADSK.BuiltInParameter.ROOF_ATTR_THICKNESS_PARAM).AsDouble()));

                                geoCurve = curve.GeometryCurve.CreateTransformed(translate);
                            }

                            var comparisonResult = face.Intersect(geoCurve);
                            var faceNormal = face.ComputeNormal(new ADSK.UV(0.5, 0.5));
                            var curveNormal = curve.SketchPlane.GetPlane().Normal;
                            if (comparisonResult == ADSK.SetComparisonResult.Subset && !faceNormal.IsAlmostEqualTo(curveNormal))
                            {
                                faces.Add(face);
                            }
                        }
                    }
                }
            }
            //return a new neat mesh
            return FacesToMesh(faces);
        }

        public static Dictionary<string, Elements.Material> GetMaterials(this ADSK.RoofBase roof)
        {
            ADSK.Document doc = roof.Document;

            var roofType = roof.RoofType;
            var allLayers = roofType.GetCompoundStructure().GetLayers();

            ADSK.Material topLayerMaterial = doc.GetElement(allLayers.First().MaterialId) as ADSK.Material;
            ADSK.Material bottomLayerMaterial = doc.GetElement(allLayers.Last().MaterialId) as ADSK.Material;

            return new Dictionary<string, Material>()
            {
                {"top", topLayerMaterial.ToElementsMaterial()},
                {"bottom", bottomLayerMaterial.ToElementsMaterial()},
            };
        }
    }
}
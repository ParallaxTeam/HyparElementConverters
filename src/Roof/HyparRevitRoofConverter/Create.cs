using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elements.Conversion.Revit.Extensions;
using ADSK = Autodesk.Revit.DB;
using Elements.Geometry;

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
        public static List<ADSK.GeometryObject> ExtractRoofFaces(this ADSK.Element roof)
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

        public static Polygon[] ExtractRoofFootprint(this ADSK.Element roof)
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
                        if (c is ADSK.Arc arc)
                        {
                            var points = arc.Tessellate();
                            vertices.AddRange(points.Select(p => p.ToVector3(true)));
                        }
                        else
                        {
                            vertices.Add(c.GetEndPoint(0).ToVector3(true));
                        }
                    }

                    polygons.Add(new Polygon(vertices.Distinct().ToList()));
                }
            }

            return polygons.ToArray();
        }
       
    }
}

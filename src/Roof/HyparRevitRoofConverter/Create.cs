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
                    //get outer curve loop
                    var currentMesh = face.Triangulate(1);

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

        public static Mesh BuildEnvelope(Polygon polygon, double thickness)
        {
            Mesh mesh = new Mesh();

            var transformedPolygon = polygon.TransformedPolygon(new Transform(0.0, 0.0, thickness));

            var ePoints = transformedPolygon.Vertices.ToList();
            var uPoints = polygon.Vertices.ToList();

            var sideTriangles = new List<Elements.Geometry.Triangle>();
            for (int i = 0; i < ePoints.Count; i++)
            {
                sideTriangles.Add(new Elements.Geometry.Triangle(new Vertex(ePoints[i]),
                    new Vertex(uPoints[i]),
                    new Vertex(uPoints[(i + 1) % uPoints.Count])));
                sideTriangles.Add(new Elements.Geometry.Triangle(new Vertex(ePoints[i]),
                    new Vertex(uPoints[(i + 1) % uPoints.Count]),
                    new Vertex(ePoints[(i + 1) % ePoints.Count])));
            }

            foreach (var t in sideTriangles)
            {
                mesh.AddTriangle(t);

                foreach (var v in t.Vertices)
                {
                    mesh.AddVertex(v);
                }
            }
            mesh.ComputeNormals();

            return mesh;
        }
    }
}

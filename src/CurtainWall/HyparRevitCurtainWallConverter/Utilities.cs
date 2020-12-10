using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;

namespace HyparRevitCurtainWallConverter
{
    public static class Utilities
    {
        public static List<Polygon> GetProfile(this Autodesk.Revit.DB.Element element)
        {
            Document doc = element.Document;
            List<Polygon> polygons = new List<Polygon>();
            IList<Reference> firstSideFaces = null;
            IList<Reference> secondSideFaces = null;
            switch (element)
            {
                case Wall revitWall:
                    //use host object utils to get the outside face
                    firstSideFaces = HostObjectUtils.GetSideFaces(revitWall, ShellLayerType.Exterior);
                    secondSideFaces = HostObjectUtils.GetSideFaces(revitWall, ShellLayerType.Interior);
                    break;
                case Autodesk.Revit.DB.Floor revitFloor:
                    firstSideFaces = HostObjectUtils.GetTopFaces(revitFloor);
                    secondSideFaces = HostObjectUtils.GetBottomFaces(revitFloor);
                    break;
            }
            Element faceElement = doc.GetElement(firstSideFaces[0]);

            if (!(faceElement.GetGeometryObjectFromReference(firstSideFaces[0]) is Face exteriorFace) || !(faceElement.GetGeometryObjectFromReference(secondSideFaces[0]) is Face interiorFace))
            {
                return null;
            }
            //this lets us pick the biggest face of the two sides. This is important because we want the shapes to close. 😁
            Face face = exteriorFace.Area > interiorFace.Area ? exteriorFace : interiorFace;
            // get the edges as curve loops and use the IFCUtils to sort them
            // credit: https://thebuildingcoder.typepad.com/blog/2015/01/getting-the-wall-elevation-profile.html
            IList<CurveLoop> curveLoops = face.GetEdgesAsCurveLoops();
            //this does the sorting so outside is the first item
            IList<CurveLoop> loops = ExporterIFCUtils.SortCurveLoops(curveLoops)[0];

            for (int i = 0; i < loops.Count; i++)
            {
                //here for outermost loop
                if (i == 0)
                {
                    var outer = loops[i];
                    List<Vector3> vertices = new List<Vector3>();
                    foreach (Autodesk.Revit.DB.Curve c in outer)
                    {
                        vertices.Add(c.GetEndPoint(0).ToVector3());
                    }
                    polygons.Add(new Polygon(vertices));
                }
                //here for the inner loops (voids)
                else
                {
                    var inner = loops[i];
                    List<Vector3> vertices = new List<Vector3>();
                    foreach (Autodesk.Revit.DB.Curve c in inner)
                    {
                        vertices.Add(c.GetEndPoint(0).ToVector3());
                    }
                    Polygon innerPolygon = new Polygon(vertices);
                    polygons.Add(innerPolygon);
                }
            }

            return polygons;
        }
    }
}

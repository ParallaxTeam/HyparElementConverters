using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB.IFC;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using Profile = Elements.Geometry.Profile;
using ADSK = Autodesk.Revit.DB;
namespace HyparRevitCurtainWallConverter
{
    public static class Utilities
    {
        public static List<Polygon> GetProfile(this ADSK.Element element)
        {
            ADSK.Document doc = element.Document;
            List<Polygon> polygons = new List<Polygon>();
            IList<ADSK.Reference> firstSideFaces = null;
            IList<ADSK.Reference> secondSideFaces = null;
            switch (element)
            {
                case ADSK.Wall revitWall:
                    //use host object utils to get the outside face
                    firstSideFaces = ADSK.HostObjectUtils.GetSideFaces(revitWall, ADSK.ShellLayerType.Exterior);
                    secondSideFaces = ADSK.HostObjectUtils.GetSideFaces(revitWall, ADSK.ShellLayerType.Interior);
                    break;
                case ADSK.Floor revitFloor:
                    firstSideFaces = ADSK.HostObjectUtils.GetTopFaces(revitFloor);
                    secondSideFaces = ADSK.HostObjectUtils.GetBottomFaces(revitFloor);
                    break;
            }
            ADSK.Element faceElement = doc.GetElement(firstSideFaces[0]);

            if (!(faceElement.GetGeometryObjectFromReference(firstSideFaces[0]) is ADSK.Face exteriorFace) || !(faceElement.GetGeometryObjectFromReference(secondSideFaces[0]) is ADSK.Face interiorFace))
            {
                return null;
            }
            //this lets us pick the biggest face of the two sides. This is important because we want the shapes to close. 😁
            ADSK.Face face = exteriorFace.Area > interiorFace.Area ? exteriorFace : interiorFace;
            // get the edges as curve loops and use the IFCUtils to sort them
            // credit: https://thebuildingcoder.typepad.com/blog/2015/01/getting-the-wall-elevation-profile.html
            IList<ADSK.CurveLoop> curveLoops = face.GetEdgesAsCurveLoops();
            //this does the sorting so outside is the first item
            IList<ADSK.CurveLoop> loops = ExporterIFCUtils.SortCurveLoops(curveLoops)[0];
            
            for (int i = 0; i < loops.Count; i++)
            {
                //here for outermost loop
                if (i == 0)
                {
                    var outer = loops[i];
                    List<Vector3> vertices = new List<Vector3>();
                    foreach (ADSK.Curve c in outer)
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
                    foreach (ADSK.Curve c in inner)
                    {
                        vertices.Add(c.GetEndPoint(0).ToVector3());
                    }
                    Polygon innerPolygon = new Polygon(vertices);
                    polygons.Add(innerPolygon);
                }
            }

            return polygons;
        }
        //this code is made possible thanks to this source code available under the MIT license
        //https://github.com/mcneel/rhino.inside-revit/blob/30b802048540c676ce81d5c924008c8cd31d8a7a/src/RhinoInside.Revit.GH/Components/Element/CurtainWall/AnalyzeCurtainGridLine.cs#L94
        public static List<ADSK.Mullion> AttachedMullions(this ADSK.CurtainGridLine gridLine)
        {
            // find attached mullions
            const double EPSILON = 0.1;
            var attachedMullions = new List<ADSK.Mullion>();
            var famInstFilter = new ADSK.ElementClassFilter(typeof(ADSK.FamilyInstance));
            // collect familyinstances and filter for DB.Mullion
            var dependentMullions = gridLine.GetDependentElements(famInstFilter).Select(x => gridLine.Document.GetElement(x)).OfType<ADSK.Mullion>();
            // for each DB.Mullion that is dependent on this DB.CurtainGridLine
            foreach (ADSK.Mullion mullion in dependentMullions)
            {
                if (mullion.LocationCurve != null)
                {
                    // check the distance of the DB.Mullion curve start and end, to the DB.CurtainGridLine axis curve
                    var mcurve = mullion.LocationCurve;
                    var mstart = mcurve.GetEndPoint(0);
                    var mend = mcurve.GetEndPoint(1);
                    // if the distance is less than EPSILON, the DB.Mullion axis and DB.CurtainGridLine axis are almost overlapping
                    if (gridLine.FullCurve.Distance(mstart) < EPSILON && gridLine.FullCurve.Distance(mend) < EPSILON)
                        attachedMullions.Add(mullion);
                }
            }

            return attachedMullions;
        }

        public static List<ADSK.PlanarFace> GetFaces(this ADSK.Element element)
        {
            List<ADSK.PlanarFace> faces = new List<ADSK.PlanarFace>();
            IEnumerable<ADSK.Solid> solids = element.get_Geometry(new ADSK.Options()).SelectMany<ADSK.GeometryObject, ADSK.Solid>((ADSK.GeometryObject g) => GetSolidsFromGeometry(g));

            foreach (ADSK.Solid solid in solids)
            {
                foreach (ADSK.Face face in solid.Faces)
                {
                    ADSK.PlanarFace planarFace = face as ADSK.PlanarFace;
                    faces.Add(planarFace);
                }
            }
            
            return faces.OrderBy(f => f.Area).ToList();
        }

        public static Profile FaceToProfile(ADSK.PlanarFace planarFace)
        {
            var profiles = planarFace.GetProfiles(true);

            Profile profile = null;
            try
            {
                profile = profiles.Aggregate<Profile>((Profile p1, Profile p2) => p1.Union(p2, 1E-05));
            }
            catch
            {
                profile = (
                    from p in profiles
                    orderby p.Perimeter.Start.Z
                    select p).First<Profile>();
            }

            return profile;
        }

        private static IEnumerable<ADSK.Solid> GetSolidsFromGeometry(ADSK.GeometryObject g)
        {
            IEnumerable<ADSK.Solid> solids;
            if ((object)(g as ADSK.GeometryInstance) == (object)null)
            {
                solids = (!(g is ADSK.Solid) ? new ADSK.Solid[0] : new ADSK.Solid[] { g as ADSK.Solid });
            }
            else
            {
                solids = (
                    from s in ((ADSK.GeometryInstance)g).GetInstanceGeometry()
                    where s.GetType() == typeof(ADSK.Solid)
                    select s).Cast<ADSK.Solid>();
            }
            return solids;
        }
        public static Curve[] ToCurves(this ADSK.CurveArrArray value)
        {
            var count = value.Size;
            var curves = new Curve[count];

            int index = 0;
            foreach (var curve in value.Cast<ADSK.Curve>())
                curves[index++] = new Line(curve.GetEndPoint(0).ToVector3(true),curve.GetEndPoint(1).ToVector3(true));

            return curves;
        }
        public static Polyline[] ToPolyCurves(this ADSK.CurveArrArray value)
        {
            var count = value.Size;
            var list = new Polyline[count];

            int index = 0;
            foreach (var curveArray in value.Cast<ADSK.CurveArray>())
            {
                List<Vector3> vertices = new List<Vector3>();

                foreach (var curve in curveArray.Cast<ADSK.Curve>())
                    vertices.Add(curve.GetEndPoint(0).ToVector3(true));

                var polycurve = new Polyline(vertices);
                list[index++] = polycurve;
            }

            return list;
        }
    }
}

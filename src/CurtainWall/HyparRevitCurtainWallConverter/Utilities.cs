using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB.IFC;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using ADSK = Autodesk.Revit.DB;
namespace HyparRevitCurtainWallConverter
{
    public static class Utilities
    {
        //this code is made possible thanks to this source code available under the MIT license
        //https://github.com/mcneel/rhino.inside-revit/blob/30b802048540c676ce81d5c924008c8cd31d8a7a/src/RhinoInside.Revit.GH/Components/Element/CurtainWall/AnalyzeCurtainGridLine.cs#L94
        public static List<ADSK.Mullion> AttachedMullions(this ADSK.CurtainGridLine gridLine)
        {
            // find attached mullions
            const double EPSILON = 0.1;
            var attachedMullions = new List<ADSK.Mullion>();
            var famInstFilter = new ADSK.ElementClassFilter(typeof(ADSK.FamilyInstance));
            // collect family instances and filter for DB.Mullion
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
        public static ADSK.XYZ curveDirection(ADSK.Curve c)
        {
            ADSK.XYZ xyz = c.GetEndPoint(0);
            ADSK.XYZ xYZ = c.GetEndPoint(1);
            ADSK.XYZ xYZ1 = new ADSK.XYZ(xYZ.X - xyz.X, xYZ.Y - xyz.Y, xYZ.Z - xyz.X);
            return xYZ1;
        }

        public static ADSK.XYZ curveListNormal(ADSK.Curve[] profile)
        {
            ADSK.XYZ zero = ADSK.XYZ.Zero;
            for (int i = 0; i < (int)profile.Length; i++)
            {
                ADSK.Curve curve = profile[i];
                ADSK.Curve curve1 = profile[(i + 1) % (int)profile.Length];
                ADSK.XYZ xYZ = curveDirection(curve);
                ADSK.XYZ xYZ1 = curveDirection(curve1);
                zero += xYZ.CrossProduct(xYZ1).Normalize();
            }
            return zero.Normalize();
        }
    }
}

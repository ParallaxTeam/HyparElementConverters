using Elements;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ADSK = Autodesk.Revit.DB;
using Profile = Elements.Geometry.Profile;

namespace HyparRevitBeamConverter
{
    public static class Create
    {
        private static double _endExtension = 0;
        private static double _startExtension = 0;
        private static double _crossRotation = 0;

        public static Beam BeamFromRevitBeam(ADSK.FamilyInstance beam)
        {
            ADSK.Document doc = beam.Document;

            string levelName = string.Empty;

            string serializedRevitData = $"{beam.Name},{beam.Symbol.Name},{levelName},{beam.StructuralType}";

            var profile = GetProfile(beam);
            var locationCurve = GetLocationCurve(beam);

            GetStartEndExtension(beam);
            
            Elements.Beam newBeam =
                new Beam(locationCurve, profile, null, _startExtension, _endExtension, _crossRotation, null, false, new Guid(), serializedRevitData);

            return newBeam;
        }


        private static void GetStartEndExtension(ADSK.FamilyInstance beam)
        {
            var start = beam.get_Parameter(ADSK.BuiltInParameter.START_EXTENSION).AsDouble();
            var end = beam.get_Parameter(ADSK.BuiltInParameter.END_EXTENSION).AsDouble();
            var cross = beam.get_Parameter(ADSK.BuiltInParameter.STRUCTURAL_BEND_DIR_ANGLE).AsDouble();

            _startExtension = Elements.Units.FeetToMeters(start);
            _endExtension = Elements.Units.FeetToMeters(end);

            var crv = beam.GetSweptProfile().GetDrivingCurve();
            //double buffer = crv is ADSK.Arc || crv is ADSK.HermiteSpline ? 90 : 0;

            _crossRotation = (cross / Math.PI * 180);
        }

        private static Elements.Geometry.Curve GetLocationCurve(ADSK.FamilyInstance beam)
        {
            var sweptProfile = beam.GetSweptProfile();
            var drivingCurve = sweptProfile.GetDrivingCurve();

            Elements.Geometry.Curve curve = null;

            switch (drivingCurve)
            {
                case ADSK.Line line:
                    curve = new Elements.Geometry.Line(line.GetEndPoint(0).ToVector3(true), line.GetEndPoint(1).ToVector3(true));
                    break;

                case ADSK.Arc arc:
                    curve = arc.ToHyparPolyline(); 
                    break;

                case ADSK.HermiteSpline spline:
                    curve = new Polyline(spline.ControlPoints.Select(p => p.ToVector3(true)).ToList());
                    break;
            }

            return curve;
        }

        private static Elements.Geometry.Profile GetProfile(ADSK.FamilyInstance beam)
        {
            List<ADSK.PlanarFace> faces = new List<ADSK.PlanarFace>();
            var geoElem = beam.get_Geometry(new ADSK.Options());

            foreach (var g in geoElem)
            {
                if (g is ADSK.GeometryInstance instance)
                {
                    foreach (var geoObj in instance.GetSymbolGeometry())
                    {
                        if (geoObj is ADSK.Solid solid)
                        {
                            var faceEnum = solid.Faces.GetEnumerator();
                            while (faceEnum.MoveNext())
                            {
                                if (faceEnum.Current is ADSK.PlanarFace planarFace)
                                {
                                    faces.Add(planarFace);
                                }
                            }
                        }
                    }
                }
            }

            List<Autodesk.Revit.DB.Curve> curves = new List<Autodesk.Revit.DB.Curve>();
            var sweptProfile = beam.GetSweptProfile();
            var profile = sweptProfile.GetSweptProfile();
            IEnumerator enumerator = profile.Curves.GetEnumerator();

            while (enumerator.MoveNext())
            {
                Autodesk.Revit.DB.Curve currentCurve = enumerator.Current as Autodesk.Revit.DB.Curve;
                curves.Add(currentCurve);
            }

            Polygon polygon = new Polygon(curves.Select(c => c.GetEndPoint(0).ToVector3(true)).ToList());

            foreach (var f in faces)
            {
                ADSK.PlanarFace pl = f as ADSK.PlanarFace;

                var outerCurveLoop = f.GetEdgesAsCurveLoops().First();

                var curveIterator = outerCurveLoop.GetCurveLoopIterator();

                List<Vector3> points = new List<Vector3>();
                while (curveIterator.MoveNext())
                {
                    points.Add(curveIterator.Current.GetEndPoint(0).ToVector3(true));
                }

                Polygon tempPolygon = new Polygon(curves.Select(c => c.GetEndPoint(0).ToVector3(true)).ToList());

                if (tempPolygon.Area().ApproximatelyEquals(polygon.Area()))
                {
                    return new Profile(tempPolygon);
                }
            }
            return null;
        }

        //I like this way more but orientation is hard
        //private static Elements.Geometry.Profile GetProfile(FamilyInstance beam)
        //{
        //    List<ADSK.Face> faces = new List<Face>();
        //    var geoElem = beam.get_Geometry(new Options());

        //    List<Autodesk.Revit.DB.Curve> curves = new List<Autodesk.Revit.DB.Curve>();
        //    var sweptProfile = beam.GetSweptProfile();
        //    var profile = sweptProfile.GetSweptProfile();

        //    IEnumerator enumerator = profile.Curves.GetEnumerator();

        //    while (enumerator.MoveNext())
        //    {
        //        Autodesk.Revit.DB.Curve currentCurve = enumerator.Current as Autodesk.Revit.DB.Curve;
        //        curves.Add(currentCurve);
        //    }

        //    Polygon polygon = new Polygon(curves.Select(c => c.GetEndPoint(0).ToVector3(true)).ToList());

        //    Elements.Geometry.Profile hyparProfile = new Elements.Geometry.Profile(polygon);

        //    return hyparProfile;
        //}

        private static double GetLengthOfBeam(ADSK.FamilyInstance beam)
        {
            return beam.get_Parameter(ADSK.BuiltInParameter.INSTANCE_LENGTH_PARAM).AsDouble();
        }

        public static Elements.Geometry.Polyline ToHyparPolyline(this ADSK.Arc arc)
        {
            return new Polyline(arc.Tessellate().Select(p => p.ToVector3(true)).ToList());
        }
    }
}
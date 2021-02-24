using Autodesk.Revit.DB;
using Elements;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ADSK = Autodesk.Revit.DB;
using Arc = Elements.Geometry.Arc;

namespace HyparRevitBeamConverter
{
    public static class Create
    {
        private static double _endExtension = 0;
        private static double _startExtension = 0;
        private static double _crossRotation = 0;

        public static Beam BeamFromRevitBeam(ADSK.FamilyInstance beam)
        {
            Document doc = beam.Document;

            string levelName = string.Empty;

            string serializedRevitData = $"{beam.Name},{beam.Symbol.Name},{levelName},{beam.StructuralType}";

            var profile = GetProfile(beam);
            var locationCurve = GetLocationCurve(beam);

            GetStartEndExtension(beam);
            
            Elements.Beam newBeam =
                new Beam(locationCurve, profile, null, _startExtension, _endExtension, _crossRotation, null, false, new Guid(), serializedRevitData);

            return newBeam;
        }


        private static void GetStartEndExtension(FamilyInstance beam)
        {
            var start = beam.get_Parameter(BuiltInParameter.START_EXTENSION).AsDouble();
            var end = beam.get_Parameter(BuiltInParameter.END_EXTENSION).AsDouble();
            var cross = beam.get_Parameter(BuiltInParameter.STRUCTURAL_BEND_DIR_ANGLE).AsDouble();

            _startExtension = Elements.Units.FeetToMeters(start);
            _endExtension = Elements.Units.FeetToMeters(end);

            var crv = beam.GetSweptProfile().GetDrivingCurve();
            //double buffer = crv is ADSK.Arc || crv is ADSK.HermiteSpline ? 90 : 0;

            _crossRotation = (cross / Math.PI * 180);
        }

        private static Elements.Geometry.Curve GetLocationCurve(FamilyInstance beam)
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

        private static Elements.Geometry.Profile GetProfile(FamilyInstance beam)
        {
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

            Elements.Geometry.Profile hyparProfile = new Elements.Geometry.Profile(polygon);

            return hyparProfile;
        }

        private static double GetLengthOfBeam(FamilyInstance beam)
        {
            return beam.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM).AsDouble();
        }

        public static Elements.Geometry.Polyline ToHyparPolyline(this ADSK.Arc arc)
        {
            return new Polyline(arc.Tessellate().Select(p => p.ToVector3(true)).ToList());
        }
    }
}
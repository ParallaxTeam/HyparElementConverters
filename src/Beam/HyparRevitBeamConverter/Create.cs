using Autodesk.Revit.DB.Structure.StructuralSections;
using Elements;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using Elements.Geometry.Profiles;
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

            //TODO: This will only do cutbacks right now. Need to figure out best way to do extensions.
            _startExtension = start < 0 ? Elements.Units.FeetToMeters(Math.Abs(start)) : 0;
            _endExtension = end < 0 ? Elements.Units.FeetToMeters(Math.Abs(end)) : 0;

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

        private static double _height = 0;
        private static double _width = 0;

        private static Elements.Geometry.Profile GetProfile(ADSK.FamilyInstance beam)
        {
            GetDims(beam);

            Profile profile;

            var crv = beam.GetSweptProfile().GetDrivingCurve();
            double rotation = crv is ADSK.Arc || crv is ADSK.HermiteSpline ? 90 : 0;
            Transform tForm = new Transform(new Vector3(), rotation);

            switch (beam.Symbol.Family.StructuralSectionShape)
            {
                case StructuralSectionShape.IWideFlange:
                    profile = new WideFlangeProfile(beam.Name, new Guid(), Elements.Units.FeetToMeters(_width), Elements.Units.FeetToMeters(_height));
                    return tForm.OfProfile(profile);

                case StructuralSectionShape.IParallelFlange:
                    profile = new WideFlangeProfile(beam.Name, new Guid(), Elements.Units.FeetToMeters(_width), Elements.Units.FeetToMeters(_height));
                    return tForm.OfProfile(profile);
                //case StructuralSectionShape.RoundHSS:
                //    profile = new HSSPipeProfile(beam.Name,new Guid())
                default:
                    return CalculateProfile(beam);
            }
        }

        private static void GetDims(ADSK.FamilyInstance beam)
        {
            var sweptProfile = beam.GetSweptProfile();
            var profile = sweptProfile.GetSweptProfile();
            IEnumerator enumerator = profile.Curves.GetEnumerator();
            List<ADSK.XYZ> points = new List<ADSK.XYZ>();
            while (enumerator.MoveNext())
            {
                ADSK.Curve currentCurve = enumerator.Current as ADSK.Curve;
                points.AddRange(currentCurve.Tessellate());
            }

            var ordered = points.OrderBy(p => p.X + p.Y);
            var min = ordered.First();
            var max = ordered.Last();
            _height = max.Y - min.Y;
            _width = max.X - min.X;
        }

        private static Profile CalculateProfile(ADSK.FamilyInstance beam)
        {
            var sweptProfile = beam.GetSweptProfile();
            var profile = sweptProfile.GetSweptProfile();
            IEnumerator enumerator = profile.Curves.GetEnumerator();

            List<Vector3> points = new List<Vector3>();

            while (enumerator.MoveNext())
            {
                Autodesk.Revit.DB.Curve currentCurve = enumerator.Current as Autodesk.Revit.DB.Curve;

                points.AddRange(currentCurve.Tessellate().Select(p => p.ToVector3(true)));
            }

            Polygon polygon = new Polygon(points.Distinct().ToList());
            Elements.Geometry.Profile hyparProfile = new Elements.Geometry.Profile(polygon);

            return hyparProfile;
        }

        private static double GetLengthOfBeam(ADSK.FamilyInstance beam)
        {
            return beam.get_Parameter(ADSK.BuiltInParameter.INSTANCE_LENGTH_PARAM).AsDouble();
        }

        public static Polyline ToHyparPolyline(this ADSK.Arc arc)
        {
            return new Polyline(arc.Tessellate().Select(p => p.ToVector3(true)).ToList());
        }

        private static IEnumerable<ADSK.Solid> GetSolidsFromGeometry(ADSK.GeometryObject g)
        {
            // TODO merge this with the future recursive solid retrieval method.
            // It may be in the plain Hypar.Revit project rather than in Elements.Conversion.Revit
            if (g is ADSK.GeometryInstance geometryInstance)
            {
                return ((ADSK.GeometryInstance)g).GetSymbolGeometry().Where(s => s.GetType() == typeof(ADSK.Solid)).Cast<ADSK.Solid>();
            }
            else if (g is ADSK.Solid)
            {
                return new[] { g as ADSK.Solid };
            }
            else
            {
                return new ADSK.Solid[] { };
            }
        }
    }
}
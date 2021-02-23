using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using ADSK = Autodesk.Revit.DB;

namespace HyparRevitBeamConverter
{
    public static partial class Create
    {
        public static Elements.Beam BeamFromRevitBeam(ADSK.FamilyInstance column, Document doc)
        {

            return null;
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
    }
}

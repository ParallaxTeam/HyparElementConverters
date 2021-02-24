using ADSK = Autodesk.Revit.DB;

namespace HyparRevitBeamConverter.Utilities
{
    public static class Utilities
    {
        public static double StartAngle(this ADSK.Arc arc)
        {
            ADSK.XYZ center = arc.Center;

            ADSK.XYZ dir0 = (arc.GetEndPoint(0) - center).Normalize();

            double startAngle = dir0.AngleTo(arc.XDirection);

            return startAngle;
        }

        public static double EndAngle(this ADSK.Arc arc)
        {
            ADSK.XYZ center = arc.Center;

            ADSK.XYZ dir1 = (arc.GetEndPoint(1) - center).Normalize();

            double endAngle = dir1.AngleTo(arc.XDirection);

            return endAngle;
        }

    }
}
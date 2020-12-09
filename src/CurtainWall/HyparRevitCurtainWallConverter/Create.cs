using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elements;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using Elements.Geometry.Profiles;
using Elements.Geometry.Solids;
using Elements.Properties;
using ADSK = Autodesk.Revit.DB;

namespace HyparRevitCurtainWallConverter
{
    public static class Create
    {
        public static Element[] MakeHyparCurtainWallFromRevitCurtainWall(Autodesk.Revit.DB.Element revitElement, ADSK.Document doc)
        {
            var curtainWall = revitElement as Autodesk.Revit.DB.Wall;

            //model curves to return
            var curtainWallMullions = new List<Mullion>();

            if (curtainWall.CurtainGrid == null)
            {
                throw new InvalidOperationException("This curtain wall does not have a grid. Curtain walls with no grid are not supported at this time.");
            }

            var uGridLines = curtainWall.CurtainGrid.GetUGridLineIds()
                .Select(c => doc.GetElement(c) as Autodesk.Revit.DB.CurtainGridLine).ToArray();

            var vGridLines = curtainWall.CurtainGrid.GetVGridLineIds()
                .Select(c => doc.GetElement(c) as Autodesk.Revit.DB.CurtainGridLine).ToArray();

            foreach (var uGrid in uGridLines)
            {
                var fullCurve = uGrid.FullCurve;

                Line line = new Line(fullCurve.GetEndPoint(0).ToVector3(), fullCurve.GetEndPoint(1).ToVector3());

                curtainWallMullions.Add(new Mullion(GetMullionShape(0.5,0.5),line));
            }

            foreach (var vGrid in vGridLines)
            {
                var fullCurve = vGrid.FullCurve;

                Line line = new Line(fullCurve.GetEndPoint(0).ToVector3(), fullCurve.GetEndPoint(1).ToVector3());

                curtainWallMullions.Add(new Mullion(GetMullionShape(0.5, 0.5), line));
            }

            Elements.CurtainWallPanel hyparCurtainWallPanel = new CurtainWallPanel(curtainWallMullions,null,null);



            return curtainWallMullions;
        }

        private static Profile GetMullionShape(double width, double height)
        {
            Polygon outerPolygon = Polygon.Rectangle(width,height);
            return new Profile(outerPolygon);
        }
    }
}

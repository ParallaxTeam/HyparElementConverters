﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elements;
using Elements.Conversion.Revit;
using Elements.Conversion.Revit.Extensions;
using Elements.Geometry;
using Elements.Geometry.Solids;
using ADSK = Autodesk.Revit.DB;


namespace HyparRevitRoofConverter
{
    public class RoofConverter : IRevitConverter
    {
        public bool CanConvertToRevit => false;
        public bool CanConvertFromRevit => true;

        public ADSK.FilteredElementCollector AddElementFilters(ADSK.FilteredElementCollector collector)
        {
            //allows us to exclude in place roofs.
            ADSK.ElementClassFilter classFilter = new ADSK.ElementClassFilter(typeof(ADSK.FamilyInstance), true);
            return collector.OfCategory(ADSK.BuiltInCategory.OST_Roofs).WherePasses(classFilter);
        }

        public Element[] FromRevit(ADSK.Element revitElement, ADSK.Document document)
        {
            return HyparRoofFromRevitRoof(revitElement);
        }

        public ADSK.ElementId[] ToRevit(Element hyparElement, LoadContext context)
        {
            throw new NotImplementedException();
        }

        public Element[] OnlyLoadableElements(Element[] allElements)
        {
            throw new NotImplementedException();
        }

        private static Element[] HyparRoofFromRevitRoof(ADSK.Element revitRoof)
        {
            Mesh bottomMesh = null;
            if (revitRoof is ADSK.FootPrintRoof footprintRoof)
            {
                var bottomFaceReferences = ADSK.HostObjectUtils.GetBottomFaces(footprintRoof);

                var bottomFaces = bottomFaceReferences.Select(r => footprintRoof.GetGeometryObjectFromReference(r)).Cast<Autodesk.Revit.DB.Face>().ToList();

                List<Vertex> bottomVertices = new List<Vertex>();

                foreach (var face in bottomFaces)
                {
                    bottomVertices.AddRange(face.Triangulate().Vertices.Select(v => new Vertex(v.ToVector3(true))));
                }

                bottomMesh = new Mesh(bottomVertices, null,null);

            }

            Roof hyparRoof = new Roof(null,null, bottomMesh, null, 0,0,0,0,null, BuiltInMaterials.Black,null,false, Guid.NewGuid(),"");

            return new List<Element>() { hyparRoof }.ToArray();
        }
    }
}

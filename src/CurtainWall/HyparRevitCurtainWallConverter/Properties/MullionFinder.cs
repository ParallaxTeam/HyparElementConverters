using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace HyparRevitCurtainWallConverter.Properties
{
    public class MullionFinder : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor a)
        {
            IList<FailureMessageAccessor> failures = a.GetFailureMessages();
            foreach (FailureMessageAccessor f in failures)
            {
                // check failure definition ids 
                // against ones to dismiss:

                FailureDefinitionId id = f.GetFailureDefinitionId();

                if (BuiltInFailures.CurtainWallFailures.MullionsWillBeDeleted == id)
                {
                    Create.InteriorMullionIds.AddRange(f.GetFailingElementIds());
                    a.ResolveFailure(f);
                }
            }
            return FailureProcessingResult.ProceedWithCommit;
        }
    }
}

using OSPSuite.Core.Services;
using OSPSuite.Presentation.UICommands;
using PKSim.Core.Model;
using PKSim.Core.Services;

namespace PKSim.Presentation.UICommands
{
   public class StartResidualVsTimeSimulationAnalysisUICommand : ActiveObjectUICommand<IndividualSimulation>
   {
      private readonly ISimulationAnalysisCreator _simulationAnalysisCreator;

      public StartResidualVsTimeSimulationAnalysisUICommand(ISimulationAnalysisCreator simulationAnalysisCreator,
         IActiveSubjectRetriever activeSubjectRetriever) : base(activeSubjectRetriever)
      {
         _simulationAnalysisCreator = simulationAnalysisCreator;
      }

      protected override void PerformExecute()
      {
         _simulationAnalysisCreator.CreateResidualsVsTimeAnalysisFor(Subject);
      }
   }
}
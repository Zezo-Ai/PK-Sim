﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Events;
using OSPSuite.Core.Serialization.SimModel.Services;
using OSPSuite.Utility.Extensions;
using PKSim.Core.Model;

namespace PKSim.Core.Services
{
   public interface IInteractiveSimulationRunner
   {
      Task RunSimulation(Simulation simulation, bool selectOutput);
      void StopSimulation();
   }

   public class InteractiveSimulationRunner : IInteractiveSimulationRunner
   {
      private readonly ISimulationSettingsRetriever _simulationSettingsRetriever;
      private readonly ISimulationRunner _simulationRunner;
      private readonly ICloner _cloner;
      private readonly ISimulationAnalysisCreator _simulationAnalysisCreator;
      private readonly ILazyLoadTask _lazyLoadTask;
      private readonly IExecutionContext _executionContext;

      private readonly SimulationRunOptions _simulationRunOptions;

      public InteractiveSimulationRunner(
         ISimulationSettingsRetriever simulationSettingsRetriever, 
         ISimulationRunner simulationRunner, 
         ICloner cloner, 
         ISimulationAnalysisCreator simulationAnalysisCreator, 
         ILazyLoadTask lazyLoadTask,
         IExecutionContext executionContext)
      {
         _simulationSettingsRetriever = simulationSettingsRetriever;
         _simulationRunner = simulationRunner;
         _cloner = cloner;
         _simulationAnalysisCreator = simulationAnalysisCreator;
         _lazyLoadTask = lazyLoadTask;
         _executionContext = executionContext;

         _simulationRunOptions = new SimulationRunOptions
         {
            CheckForNegativeValues = true,
            RaiseEvents = true,
            RunForAllOutputs = false,
            SimModelExportMode = SimModelExportMode.Optimized
         };
      }

      public async Task RunSimulation(Simulation simulation, bool selectOutput)
      {
         _lazyLoadTask.Load(simulation);

         if (outputSelectionRequired(simulation, selectOutput))
         {
            var outputSelections = _simulationSettingsRetriever.SettingsFor(simulation);
            if (outputSelections == null)
               return;

            simulation.OutputSelections.UpdatePropertiesFrom(outputSelections, _cloner);
            mappingsNotSelected(simulation).Each(simulation.OutputMappings.Remove);
            
            _executionContext.PublishEvent(new SimulationOutputSelectionsChangedEvent(simulation));
         }

         await _simulationRunner.RunSimulation(simulation, _simulationRunOptions);

         addAnalysableToSimulationIfRequired(simulation);
      }

      private static IReadOnlyList<OutputMapping> mappingsNotSelected(Simulation simulation) => 
         simulation.OutputMappings.Where(outputMapping => !outputMappingIsSelected(simulation, outputMapping)).ToList();

      private static bool outputMappingIsSelected(Simulation simulation, OutputMapping outputMapping) => 
         simulation.OutputSelections.AllOutputs.Any(quantitySelection => Equals(quantitySelection.Path, outputMapping.OutputPath));

      private bool outputSelectionRequired(Simulation simulation, bool selectOutput)
      {
         if (selectOutput)
            return true;

         if (simulation.OutputSelections == null)
            return true;

         return !simulation.OutputSelections.HasSelection;
      }

      public void StopSimulation()
      {
         _simulationRunner.StopSimulation();
      }

      private void addAnalysableToSimulationIfRequired(Simulation simulation)
      {
         if (simulation == null || !simulation.HasResults) return;
         if (simulation.Analyses.Count() != 0) return;
         _simulationAnalysisCreator.CreateAnalysisFor(simulation);
      }
   }
}
﻿using System;
using System.Collections.Generic;
using System.Linq;
using OSPSuite.Core.Domain;
using OSPSuite.Utility.Extensions;
using PKSim.Assets;
using PKSim.Core.Mappers;
using PKSim.Core.Model;

namespace PKSim.Core.Services
{
   public interface ISimulationBuildingBlockUpdater
   {
      /// <summary>
      ///    Sets a clone of the template building block as "used building block" in the simulation
      /// </summary>
      /// <param name="simulation">Simulation that will be using the template building block</param>
      /// <param name="templateBuildingBlock">template building block to be used</param>
      /// <param name="buildingBlockType">Type of building block</param>
      /// <remarks>
      ///    This Method should only be used for building blocks whose occurrence in a simulation is 0 or 1. (e.g Individual,
      ///    Protocol, Compound)
      ///    For other type (e.g. Formulation, Events), this method is not suited
      /// </remarks>
      void UpdateUsedBuildingBlockInSimulationFromTemplate(Simulation simulation, IPKSimBuildingBlock templateBuildingBlock, PKSimBuildingBlockType buildingBlockType);

      /// <summary>
      ///    Update building block used in the given simulation.
      /// </summary>
      /// <param name="simulation">Simulation</param>
      /// <param name="templateBuildingBlocks">All template building blocks</param>
      /// <param name="buildingBlockType">Type of building block</param>
      /// <remarks>
      ///    This method should only be used for building blocks whose occurrence in a simulation is 0 ..* (e.g. Formulation,
      ///    Events) for which all building blocks need to be updated at once
      /// </remarks>
      void UpdateMultipleUsedBuildingBlockInSimulationFromTemplate(Simulation simulation, IEnumerable<IPKSimBuildingBlock> templateBuildingBlocks, PKSimBuildingBlockType buildingBlockType);

      /// <summary>
      ///    Returns true if the parameter values from the  simulation building block can simply be updated from the
      ///    templateBuilding block parameters.
      ///    Returns false if a simple parameter value update cannot be performed (i.e. structural change was made)
      /// </summary>
      /// <param name="templateBuildingBlock">Template building block as defined in repository</param>
      /// <param name="usedBuildingBlock">
      ///    Used building block (the one based on the template building block at a given time). It
      ///    it assumed that the object is loaded (e.g. reference to building block can be used
      /// </param>
      bool QuickUpdatePossibleFor(IPKSimBuildingBlock templateBuildingBlock, UsedBuildingBlock usedBuildingBlock);

      /// <summary>
      ///    Update the used <see cref="Protocol" /> building blocks used in the <paramref name="simulation" /> based
      ///    on the simulation properties
      /// </summary>
      void UpdateProtocolsInSimulation(Simulation simulation);

      /// <summary>
      ///    Update the used <see cref="Formulation" /> building blocks used in the <paramref name="simulation" /> based
      ///    on the simulation properties
      /// </summary>
      void UpdateFormulationsInSimulation(Simulation simulation);

      bool BuildingBlockSupportsQuickUpdate(IPKSimBuildingBlock templateBuildingBlock);

      /// <summary>
      ///    Returns whether a building block comparison is available for the building block
      /// </summary>
      bool BuildingBlockSupportComparison(IPKSimBuildingBlock templateBuildingBlock);
   }

   public class SimulationBuildingBlockUpdater : ISimulationBuildingBlockUpdater
   {
      private readonly IBuildingBlockToUsedBuildingBlockMapper _buildingBlockMapper;

      public SimulationBuildingBlockUpdater(IBuildingBlockToUsedBuildingBlockMapper buildingBlockMapper)
      {
         _buildingBlockMapper = buildingBlockMapper;
      }

      public void UpdateUsedBuildingBlockInSimulationFromTemplate(Simulation simulation, IPKSimBuildingBlock templateBuildingBlock, PKSimBuildingBlockType buildingBlockType)
      {
         if (!templateBuildingBlockCanBeUpdatedAsSingle(buildingBlockType))
            throw new ArgumentException($"Should not call this function with a building block of type '{buildingBlockType}'");

         var previousUsedBuildingBlock = simulation.UsedBuildingBlockInSimulation(buildingBlockType);
         var newUsedBuildingBlock = _buildingBlockMapper.MapFrom(templateBuildingBlock, previousUsedBuildingBlock);
         simulation.RemoveUsedBuildingBlock(previousUsedBuildingBlock);
         simulation.AddUsedBuildingBlock(newUsedBuildingBlock);
      }

      public void UpdateMultipleUsedBuildingBlockInSimulationFromTemplate(Simulation simulation, IEnumerable<IPKSimBuildingBlock> templateBuildingBlocks, PKSimBuildingBlockType buildingBlockType)
      {
         var allTemplates = templateBuildingBlocks.ToList();

         if (!allTemplates.Any())
            simulation.RemoveAllBuildingBlockOfType(buildingBlockType);

         if (!templateBuildingBlockCanBeUpdatedAsMultiple(buildingBlockType))
            throw new ArgumentException($"Should not call this function with a building block of type '{buildingBlockType}'");

         //templateBuildingBlocks contains all the building block that have been selected
         //remove the one that are not used anymore . Used to list to remove while looping
         foreach (var usedBuildingBlock in simulation.UsedBuildingBlocksInSimulation(buildingBlockType).ToList())
         {
            //template is used, continue
            if (allTemplates.ExistsById(usedBuildingBlock.TemplateId))
               continue;

            //user defined building block reused?
            if (allTemplates.ExistsById(usedBuildingBlock.Id))
               continue;

            simulation.RemoveUsedBuildingBlock(usedBuildingBlock);
         }

         allTemplates.Each(bb => addUsedBuildingBlockToSimulation(simulation, bb));
      }

      private void addUsedBuildingBlockToSimulation(Simulation simulation, IPKSimBuildingBlock templateBuildingBlock)
      {
         var previousUsedBuildingBlock = simulation.UsedBuildingBlockById(templateBuildingBlock.Id);
         var newUsedBuildingBlock = _buildingBlockMapper.MapFrom(templateBuildingBlock, previousUsedBuildingBlock);
         simulation.RemoveUsedBuildingBlock(previousUsedBuildingBlock);
         simulation.AddUsedBuildingBlock(newUsedBuildingBlock);
      }

      private bool templateBuildingBlockCanBeUpdatedAsSingle(PKSimBuildingBlockType buildingBlockType)
      {
         return buildingBlockType.IsOneOf(PKSimBuildingBlockType.Individual, PKSimBuildingBlockType.Population);
      }

      private bool templateBuildingBlockCanBeUpdatedAsMultiple(PKSimBuildingBlockType buildingBlockType)
      {
         return !templateBuildingBlockCanBeUpdatedAsSingle(buildingBlockType);
      }

      public bool QuickUpdatePossibleFor(IPKSimBuildingBlock templateBuildingBlock, UsedBuildingBlock usedBuildingBlock)
      {
         if (templateBuildingBlock.Id != usedBuildingBlock.TemplateId)
            return false;

         if (!BuildingBlockSupportsQuickUpdate(templateBuildingBlock))
            return false;

         //not the same structure, easy return
         var sameStructureVersion = templateBuildingBlock.StructureVersion == usedBuildingBlock.StructureVersion;
         if (!sameStructureVersion)
            return false;

         //For individual, there is a special handling required as we need to also check that the used expression profile have the same structure version
         if (templateBuildingBlock is not Individual individualTemplate)
            return true;

         //It has to be available by construction
         var usedIndividual = usedBuildingBlock.BuildingBlock as Individual;
         //but we return false just in case :)
         if (usedIndividual == null)
            return false;

         //let's compare the expression profile in each individuals and see if they are comparable
         return individualTemplate.AllExpressionProfiles().All(x =>
         {
            //assume we can find it by name. Otherwise => structural change
            var usedExpressionProfile = usedIndividual.AllExpressionProfiles().FindByName(x.Name);
            return usedExpressionProfile != null && x.StructureVersion == usedExpressionProfile.StructureVersion;
         });
      }

      public void UpdateProtocolsInSimulation(Simulation simulation)
      {
         var protocolProperties = simulation.CompoundPropertiesList.Select(x => x.ProtocolProperties)
            .Where(x => x.Protocol != null).ToList();

         UpdateMultipleUsedBuildingBlockInSimulationFromTemplate(simulation, protocolProperties.Select(x => x.Protocol), PKSimBuildingBlockType.Protocol);

         var allSimulationProtocols = simulation.AllBuildingBlocks<Protocol>().ToList();
         //update selected protocol with references in simulation instead of templates
         protocolProperties.Each(x => x.Protocol = allSimulationProtocols.FindByName(x.Protocol.Name));
      }

      public void UpdateFormulationsInSimulation(Simulation simulation)
      {
         var allFormulationMappings = simulation.CompoundPropertiesList.SelectMany(x => x.ProtocolProperties.FormulationMappings);

         var allFormulationUsed = allFormulationMappings.Select(x => x.Formulation).Distinct().ToList();
         checkThatFormulationIsUsedEitherAsTemplateOrAsSimulationFormulation(allFormulationUsed);
         UpdateMultipleUsedBuildingBlockInSimulationFromTemplate(simulation, allFormulationUsed, PKSimBuildingBlockType.Formulation);
      }

      private static void checkThatFormulationIsUsedEitherAsTemplateOrAsSimulationFormulation(IReadOnlyList<Formulation> allFormulationUsed)
      {
         var allFormulationNames = allFormulationUsed.AllNames().Distinct().ToList();

         if (allFormulationNames.Count == allFormulationUsed.Count)
            return;

         throw new PKSimException(PKSimConstants.Error.FormulationShouldBeUsedAsTemplateOrAsSimulationBuildingBlock);
      }

      public bool BuildingBlockSupportsQuickUpdate(IPKSimBuildingBlock templateBuildingBlock)
      {
         return !templateBuildingBlock.BuildingBlockType.IsOneOf(
            PKSimBuildingBlockType.Protocol,
            PKSimBuildingBlockType.Population,
            PKSimBuildingBlockType.ObserverSet);
      }

      public bool BuildingBlockSupportComparison(IPKSimBuildingBlock templateBuildingBlock)
      {
         return !templateBuildingBlock.BuildingBlockType.IsOneOf(
            PKSimBuildingBlockType.Protocol,
            PKSimBuildingBlockType.Population);
      }
   }
}
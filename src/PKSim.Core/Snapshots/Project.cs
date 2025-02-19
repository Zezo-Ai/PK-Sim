﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using OSPSuite.Core.Domain;
using OSPSuite.Utility.Extensions;
using PKSim.Assets;

namespace PKSim.Core.Snapshots
{
   public class Project : IWithDescription, IWithName
   {
      [Required]
      public int Version { get; set; }

      public string Name { get; set; }
      public string Description { get; set; }
      public ExpressionProfile[] ExpressionProfiles { get; set; }
      public Individual[] Individuals { get; set; }
      public Population[] Populations { get; set; }
      public Compound[] Compounds { get; set; }
      public Formulation[] Formulations { get; set; }
      public Protocol[] Protocols { get; set; }
      public ObserverSet[] ObserverSets { get; set; }
      public Event[] Events { get; set; }
      public Simulation[] Simulations { get; set; }
      public ParameterIdentification[] ParameterIdentifications { get; set; }
      public DataRepository[] ObservedData { get; set; }
      public SimulationComparison[] SimulationComparisons { get; set; }
      public QualificationPlan[] QualificationPlans { get; set; }
      public Classification[] ObservedDataClassifications { get; set; }
      public Classification[] SimulationComparisonClassifications { get; set; }
      public Classification[] SimulationClassifications { get; set; }
      public Classification[] ParameterIdentificationClassifications { get; set; }
      public Classification[] QualificationPlanClassifications { get; set; }

      public IReadOnlyList<IBuildingBlockSnapshot> BuildingBlocksByType(PKSimBuildingBlockType buildingBlockType)
      {
         switch (buildingBlockType)
         {
            case PKSimBuildingBlockType.ExpressionProfile:
               return ExpressionProfiles;
            case PKSimBuildingBlockType.Compound:
               return Compounds;
            case PKSimBuildingBlockType.Formulation:
               return Formulations;
            case PKSimBuildingBlockType.Protocol:
               return Protocols;
            case PKSimBuildingBlockType.Individual:
               return Individuals;
            case PKSimBuildingBlockType.Population:
               return Populations;
            case PKSimBuildingBlockType.Event:
               return Events;
            case PKSimBuildingBlockType.Simulation:
               return Simulations;
            case PKSimBuildingBlockType.ObserverSet:
               return ObserverSets;
            default:
               return null;
         }
      }

      private string buildingBlockNameFor(IBuildingBlockSnapshot buildingBlockSnapshot)
      {
         //Special case for Expression profile where the name is not defined in the snapshot

         switch (buildingBlockSnapshot)
         {
            case ExpressionProfile expressionProfile:
               return Constants.ContainerName.ExpressionProfileName(expressionProfile.Molecule, expressionProfile.Species, expressionProfile.Category);
            default:
               return buildingBlockSnapshot.Name;
         }
      }

      public IBuildingBlockSnapshot BuildingBlockByTypeAndName(PKSimBuildingBlockType buildingBlockType, string name)
      {
         var buildingBlocks = BuildingBlocksByType(buildingBlockType);
         if (buildingBlocks == null)
            return null;

         switch (buildingBlockType)
         {
            //special case for expression profile where name is a created based of a composite key
            case PKSimBuildingBlockType.ExpressionProfile:
            {
               var (moleculeName, speciesName, category) = Constants.ContainerName.NamesFromExpressionProfileName(name);
               return ExpressionProfiles.Find(x => string.Equals(x.Molecule, moleculeName) && string.Equals(x.Species, speciesName) && string.Equals(x.Category, category));
            }

            default:
               return buildingBlocks.FindByName(name);
         }
      }

      

      public bool Swap(IBuildingBlockSnapshot newBuildingBlock)
      {
         if (newBuildingBlock == null)
            return false;

         var type = newBuildingBlock.BuildingBlockType;
         var name = buildingBlockNameFor(newBuildingBlock);
         var originalBuildingBlock = BuildingBlockByTypeAndName(type, name);

         if (originalBuildingBlock == null)
            throw new PKSimException(PKSimConstants.Error.CannotFindBuildingBlockInSnapshot(type.ToString(), name, Name));

         switch (type)
         {
            case PKSimBuildingBlockType.Compound:
               return swap(Compounds, originalBuildingBlock, newBuildingBlock);
            case PKSimBuildingBlockType.Formulation:
               return swap(Formulations, originalBuildingBlock, newBuildingBlock);
            case PKSimBuildingBlockType.Protocol:
               return swap(Protocols, originalBuildingBlock, newBuildingBlock);
            case PKSimBuildingBlockType.Individual:
               return swap(Individuals, originalBuildingBlock, newBuildingBlock);
            case PKSimBuildingBlockType.Population:
               return swap(Populations, originalBuildingBlock, newBuildingBlock);
            case PKSimBuildingBlockType.Event:
               return swap(Events, originalBuildingBlock, newBuildingBlock);
            case PKSimBuildingBlockType.ObserverSet:
               return swap(ObserverSets, originalBuildingBlock, newBuildingBlock);
            case PKSimBuildingBlockType.ExpressionProfile:
               return swap(ExpressionProfiles, originalBuildingBlock, newBuildingBlock);
            default:
               return false;
         }
      }

      private bool swap<T>(T[] buildingBlocks, IBuildingBlockSnapshot originalBuildingBlock, IBuildingBlockSnapshot newBuildingBlock)
      {
         var index = Array.IndexOf(buildingBlocks, originalBuildingBlock);
         if (index < 0)
            return false;

         buildingBlocks[index] = newBuildingBlock.DowncastTo<T>();
         return true;
      }
   }
}
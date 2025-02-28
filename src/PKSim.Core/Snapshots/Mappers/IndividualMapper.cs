﻿using System.Linq;
using System.Threading.Tasks;
using OSPSuite.Core.Domain;
using OSPSuite.Utility.Extensions;
using PKSim.Core.Model;
using PKSim.Core.Services;
using SnapshotIndividual = PKSim.Core.Snapshots.Individual;
using ModelIndividual = PKSim.Core.Model.Individual;

namespace PKSim.Core.Snapshots.Mappers
{
   public class IndividualMapper : ObjectBaseSnapshotMapperBase<ModelIndividual, SnapshotIndividual>
   {
      private readonly ParameterMapper _parameterMapper;
      private readonly ExpressionProfileMapper _expressionProfileMapper;
      private readonly IIndividualFactory _individualFactory;
      private readonly IMoleculeExpressionTask<ModelIndividual> _moleculeExpressionTask;
      private readonly OriginDataMapper _originDataMapper;

      public IndividualMapper(
         ParameterMapper parameterMapper,
         ExpressionProfileMapper expressionProfileMapper,
         OriginDataMapper originDataMapper,
         IIndividualFactory individualFactory,
         IMoleculeExpressionTask<ModelIndividual> moleculeExpressionTask
      )
      {
         _parameterMapper = parameterMapper;
         _expressionProfileMapper = expressionProfileMapper;
         _individualFactory = individualFactory;
         _moleculeExpressionTask = moleculeExpressionTask;
         _originDataMapper = originDataMapper;
      }

      public override async Task<SnapshotIndividual> MapToSnapshot(ModelIndividual individual)
      {
         var snapshot = await SnapshotFrom(individual, x => { x.Seed = individual.Seed; });
         snapshot.OriginData = await _originDataMapper.MapToSnapshot(individual.OriginData);
         snapshot.ExpressionProfiles = allExpressionProfilesFrom(individual);
         snapshot.Parameters = await allParametersChangedByUserFrom(individual);
         return snapshot;
      }

      private string[] allExpressionProfilesFrom(ModelIndividual individual)
      {
         return individual.AllExpressionProfiles().AllNames().ToArray();
      }

      private Task<LocalizedParameter[]> allParametersChangedByUserFrom(ModelIndividual individual)
      {
         //Expression profile parameters are exported now in the expression profile building block
         var changedParameters = individual.GetAllChildren<IParameter>(x => !x.IsExpressionProfile() && x.ShouldExportToSnapshot());
         return _parameterMapper.LocalizedParametersFrom(changedParameters);
      }

      public override async Task<ModelIndividual> MapToModel(SnapshotIndividual individualSnapshot, SnapshotContext snapshotContext)
      {
         var originData = await _originDataMapper.MapToModel(individualSnapshot.OriginData, snapshotContext);
         var individual = _individualFactory.CreateAndOptimizeFor(originData, individualSnapshot.Seed);
         MapSnapshotPropertiesToModel(individualSnapshot, individual);

         await updateIndividualParameters(individualSnapshot, individual, snapshotContext);

         if (snapshotContext.IsV10FormatOrEarlier)
            await convertMoleculesToExpressionProfiles(individualSnapshot, snapshotContext);

         individualSnapshot.ExpressionProfiles?.Each(x =>
         {
            var expressionProfile = snapshotContext.Project.BuildingBlockByName<Model.ExpressionProfile>(x);
            _moleculeExpressionTask.AddExpressionProfile(individual, expressionProfile);
         });

         individual.Icon = individual.Species.Icon;
         return individual;
      }

      private async Task convertMoleculesToExpressionProfiles(SnapshotIndividual individualSnapshot, SnapshotContext snapshotContext)
      {
         var expressionProfilesSnapshot = individualSnapshot.Molecules;
         if (expressionProfilesSnapshot == null)
            return;

         var project = snapshotContext.Project;

         expressionProfilesSnapshot.Each(x =>
         {
            x.Species = individualSnapshot.OriginData.Species;
            x.Category = individualSnapshot.Name;
            x.Molecule = x.Name;
         });

         var expressionProfiles = await _expressionProfileMapper.MapToModels(expressionProfilesSnapshot, snapshotContext);
         foreach (var expressionProfile in expressionProfiles)
         {
            var (_, individual) = expressionProfile;
            //Expression profile may have been added already to the project when converting a project with population 
            //and individual. So we only add if not available already
            if (project.BuildingBlockByName<Model.ExpressionProfile>(expressionProfile.Name) == null)
               project.AddBuildingBlock(expressionProfile);

            //this needs to happen here since molecule parameters were defined in individual in v10
            await updateIndividualParameters(individualSnapshot, individual, snapshotContext);
         }

         individualSnapshot.ExpressionProfiles = expressionProfiles.AllNames().ToArray();
      }

      private Task updateIndividualParameters(SnapshotIndividual snapshot, ModelIndividual individual, SnapshotContext snapshotContext)
      {
         //We do not show warning for v10 format as we will FOR SURE have missing parameters
         return _parameterMapper.MapLocalizedParameters(snapshot.Parameters, individual, snapshotContext, showParameterNotFoundWarning: !snapshotContext.IsV10FormatOrEarlier);
      }
   }
}
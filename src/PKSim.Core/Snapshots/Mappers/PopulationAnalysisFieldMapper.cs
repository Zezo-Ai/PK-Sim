﻿using System;
using System.Linq;
using System.Threading.Tasks;
using OSPSuite.Utility.Extensions;
using PKSim.Core.Model.PopulationAnalyses;
using PKSim.Core.Repositories;

namespace PKSim.Core.Snapshots.Mappers
{
   public class PopulationAnalysisFieldMapper : ObjectBaseSnapshotMapperBase<IPopulationAnalysisField, PopulationAnalysisField>
   {
      private readonly GroupingDefinitionMapper _groupingDefinitionMapper;
      private readonly IDimensionRepository _dimensionRepository;

      public PopulationAnalysisFieldMapper(GroupingDefinitionMapper groupingDefinitionMapper, IDimensionRepository dimensionRepository)
      {
         _groupingDefinitionMapper = groupingDefinitionMapper;
         _dimensionRepository = dimensionRepository;
      }

      public override async Task<PopulationAnalysisField> MapToSnapshot(IPopulationAnalysisField field)
      {
         var snapshot = await SnapshotFrom(field);
         mapIf<PopulationAnalysisOutputField>(snapshot, field, mapOutputFieldToSnapshot);
         mapIf<PopulationAnalysisParameterField>(snapshot, field, mapParameterFieldToSnapshot);
         mapIf<PopulationAnalysisPKParameterField>(snapshot, field, mapPKParameterFieldToSnapshot);
         mapIf<PopulationAnalysisCovariateField>(snapshot, field, mapCovariateField);
         await mapGroupingFieldProperties(snapshot, field as PopulationAnalysisGroupingField);
         return snapshot;
      }

      public override async Task<IPopulationAnalysisField> MapToModel(PopulationAnalysisField snapshot, SnapshotContext snapshotContext)
      {
         var populationAnalysisField = await createFieldFrom(snapshot, snapshotContext);
         MapSnapshotPropertiesToModel(snapshot, populationAnalysisField);
         mapIf<PopulationAnalysisParameterField>(snapshot, populationAnalysisField, mapParameterFieldToModel);
         mapIf<PopulationAnalysisPKParameterField>(snapshot, populationAnalysisField, mapPKParameterFieldToModel);
         mapIf<PopulationAnalysisCovariateField>(snapshot, populationAnalysisField, mapCovariateFieldToModel);
         mapIf<PopulationAnalysisOutputField>(snapshot, populationAnalysisField, mapOutputFieldToModel);
         return populationAnalysisField;
      }

      private void mapCovariateField(PopulationAnalysisField snapshot, PopulationAnalysisCovariateField field)
      {
         snapshot.Covariate = field.Covariate;
         snapshot.GroupingItems = field.GroupingItems.ToArray();
      }

      private void mapCovariateFieldToModel(PopulationAnalysisField snapshot, PopulationAnalysisCovariateField field)
      {
         field.Covariate = snapshot.Covariate;
         snapshot.GroupingItems?.Each(field.AddGroupingItem);
      }

      private async Task mapGroupingFieldProperties(PopulationAnalysisField snapshot, PopulationAnalysisGroupingField field)
      {
         if (field == null)
            return;

         snapshot.GroupingDefinition = await _groupingDefinitionMapper.MapToSnapshot(field.GroupingDefinition);
      }

      private void mapPKParameterFieldToSnapshot(PopulationAnalysisField snapshot, PopulationAnalysisPKParameterField field)
      {
         mapQuantityFieldToSnapshot(snapshot, field);
         snapshot.PKParameter = field.PKParameter;
      }

      private void mapPKParameterFieldToModel(PopulationAnalysisField snapshot, PopulationAnalysisPKParameterField field)
      {
         mapQuantityFieldToModel(snapshot, field);
         field.PKParameter = snapshot.PKParameter;
      }

      private void mapParameterFieldToSnapshot(PopulationAnalysisField snapshot, PopulationAnalysisParameterField field)
      {
         mapNumericFieldToSnapshot(snapshot, field);
         snapshot.ParameterPath = field.ParameterPath;
      }

      private void mapParameterFieldToModel(PopulationAnalysisField snapshot, PopulationAnalysisParameterField field)
      {
         mapNumericFieldToModel(snapshot, field);
         field.ParameterPath = snapshot.ParameterPath;
      }

      private void mapOutputFieldToSnapshot(PopulationAnalysisField snapshot, PopulationAnalysisOutputField field)
      {
         mapQuantityFieldToSnapshot(snapshot, field);
         snapshot.Color = field.Color;
      }

      private void mapOutputFieldToModel(PopulationAnalysisField snapshot, PopulationAnalysisOutputField field)
      {
         mapQuantityFieldToModel(snapshot, field);
         field.Color = ModelValueFor(snapshot.Color, field.Color);
      }

      private void mapQuantityFieldToSnapshot(PopulationAnalysisField snapshot, IQuantityField field)
      {
         mapNumericFieldToSnapshot(snapshot, field);
         snapshot.QuantityPath = field.QuantityPath;
         snapshot.QuantityType = field.QuantityType;
      }

      private void mapQuantityFieldToModel(PopulationAnalysisField snapshot, IQuantityField field)
      {
         mapNumericFieldToModel(snapshot, field);
         field.QuantityPath = snapshot.QuantityPath;
         if (snapshot.QuantityType != null)
            field.QuantityType = snapshot.QuantityType.Value;
      }

      private void mapNumericFieldToSnapshot(PopulationAnalysisField snapshot, INumericValueField field)
      {
         snapshot.Dimension = field.Dimension.Name;
         snapshot.Unit = SnapshotValueFor(field.DisplayUnit.Name);
         snapshot.Scaling = field.Scaling;
      }

      private void mapNumericFieldToModel(PopulationAnalysisField snapshot, INumericValueField field)
      {
         field.Dimension = _dimensionRepository.DimensionByName(snapshot.Dimension);
         var optimalDimension = _dimensionRepository.MergedDimensionFor(field);
         field.DisplayUnit = optimalDimension.Unit(ModelValueFor(snapshot.Unit));
         field.Scaling = ModelValueFor(snapshot.Scaling, field.Scaling);
      }

      private void mapIf<T>(PopulationAnalysisField snapshot, IPopulationAnalysisField populationAnalysisField, Action<PopulationAnalysisField, T> mapAction) where T : class, IPopulationAnalysisField
      {
         if (!(populationAnalysisField is T field))
            return;

         mapAction(snapshot, field);
      }

      private async Task<IPopulationAnalysisField> createFieldFrom(PopulationAnalysisField snapshot, SnapshotContext snapshotContext)
      {
         if (snapshot.ParameterPath != null)
            return new PopulationAnalysisParameterField();

         if (snapshot.PKParameter != null)
            return new PopulationAnalysisPKParameterField();

         if (snapshot.Covariate != null)
            return new PopulationAnalysisCovariateField();

         if (snapshot.GroupingDefinition != null)
         {
            var groupingDefinition = await _groupingDefinitionMapper.MapToModel(snapshot.GroupingDefinition, snapshotContext);
            return new PopulationAnalysisGroupingField(groupingDefinition);
         }

         return new PopulationAnalysisOutputField();
      }
   }
}
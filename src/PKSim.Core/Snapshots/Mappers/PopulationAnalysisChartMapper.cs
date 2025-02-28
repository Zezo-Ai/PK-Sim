﻿using System.Linq;
using System.Threading.Tasks;
using OSPSuite.Utility.Extensions;
using PKSim.Core.Model.PopulationAnalyses;
using ModelPopulationAnalysisChart = PKSim.Core.Model.PopulationAnalyses.PopulationAnalysisChart;
using SnapshotPopulationAnalysisChart = PKSim.Core.Snapshots.PopulationAnalysisChart;

namespace PKSim.Core.Snapshots.Mappers
{
   public class PopulationAnalysisChartMapper : ObjectBaseSnapshotMapperBase<ModelPopulationAnalysisChart, SnapshotPopulationAnalysisChart, SimulationAnalysisContext>
   {
      private readonly ChartMapper _chartMapper;
      private readonly PopulationAnalysisMapper _populationAnalysisMapper;
      private readonly ObservedDataCollectionMapper _observedDataCollectionMapper;
      private readonly IPopulationAnalysisChartFactory _populationAnalysisChartFactory;

      public PopulationAnalysisChartMapper(ChartMapper chartMapper, PopulationAnalysisMapper populationAnalysisMapper, ObservedDataCollectionMapper observedDataCollectionMapper, IPopulationAnalysisChartFactory populationAnalysisChartFactory)
      {
         _chartMapper = chartMapper;
         _populationAnalysisMapper = populationAnalysisMapper;
         _observedDataCollectionMapper = observedDataCollectionMapper;
         _populationAnalysisChartFactory = populationAnalysisChartFactory;
      }

      public override async Task<SnapshotPopulationAnalysisChart> MapToSnapshot(ModelPopulationAnalysisChart populationAnalysisChart)
      {
         var snapshot = await SnapshotFrom(populationAnalysisChart, x =>
         {
            x.Type = populationAnalysisChart.AnalysisType;
            x.XAxisSettings = populationAnalysisChart.PrimaryXAxisSettings;
            x.YAxisSettings = populationAnalysisChart.PrimaryYAxisSettings;
            x.SecondaryYAxisSettings = populationAnalysisChart.SecondaryYAxisSettings.ToArray();
         });

         await _chartMapper.MapToSnapshot(populationAnalysisChart, snapshot);
         snapshot.Analysis = await _populationAnalysisMapper.MapToSnapshot(populationAnalysisChart.BasePopulationAnalysis);
         snapshot.ObservedDataCollection = await _observedDataCollectionMapper.MapToSnapshot(populationAnalysisChart.ObservedDataCollection);

         return snapshot;
      }

      public override async Task<ModelPopulationAnalysisChart> MapToModel(SnapshotPopulationAnalysisChart snapshot, SimulationAnalysisContext simulationAnalysisContext)
      {
         var populationAnalysisChart = _populationAnalysisChartFactory.Create(snapshot.Type);
         MapSnapshotPropertiesToModel(snapshot, populationAnalysisChart);
         populationAnalysisChart.PrimaryXAxisSettings = snapshot.XAxisSettings;
         populationAnalysisChart.PrimaryYAxisSettings = snapshot.YAxisSettings;
         snapshot.SecondaryYAxisSettings.Each(populationAnalysisChart.AddSecondaryAxis);

         await _chartMapper.MapToModel(snapshot, new ChartSnapshotContext(populationAnalysisChart, simulationAnalysisContext));
         await _populationAnalysisMapper.MapToModel(snapshot.Analysis, new PopulationAnalysisSnapshotContext(populationAnalysisChart.BasePopulationAnalysis, simulationAnalysisContext));

         var observedDataCollection = await _observedDataCollectionMapper.MapToModel(snapshot.ObservedDataCollection, simulationAnalysisContext);
         populationAnalysisChart.ObservedDataCollection.UpdateFrom(observedDataCollection);
         return populationAnalysisChart;
      }
   }
}
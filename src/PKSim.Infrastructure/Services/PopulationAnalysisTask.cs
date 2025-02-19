﻿using OSPSuite.Core.Chart;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Services;
using OSPSuite.Core.Services;
using OSPSuite.Utility.Extensions;
using PKSim.Assets;
using PKSim.Core.Chart;
using PKSim.Core.Mappers;
using PKSim.Core.Model.PopulationAnalyses;
using PKSim.Core.Services;

namespace PKSim.Infrastructure.Services
{
   public class PopulationAnalysisTask : IPopulationAnalysisTask
   {
      private readonly IChartDataToTableMapperFactory _chartDataToTableMapperFactory;
      private readonly IPKSimProjectRetriever _projectRetriever;
      private readonly IDialogCreator _dialogCreator;
      private readonly IDataRepositoryExportTask _dataRepositoryTask;

      public PopulationAnalysisTask(
         IDialogCreator dialogCreator,
         IDataRepositoryExportTask dataRepositoryTask,
         IChartDataToTableMapperFactory chartDataToTableMapperFactory,
         IPKSimProjectRetriever projectRetriever)
      {
         _chartDataToTableMapperFactory = chartDataToTableMapperFactory;
         _projectRetriever = projectRetriever;
         _dialogCreator = dialogCreator;
         _dataRepositoryTask = dataRepositoryTask;
      }

      public void SetOriginText(PopulationAnalysisChart populationAnalysisChart, string simulationName)
      {
         populationAnalysisChart.SetOriginTextFor(_projectRetriever.CurrentProject.Name, simulationName);
      }

      public void ExportToExcel<TXValue, TYValue>(ChartData<TXValue, TYValue> chartData, string analysisName) where TXValue : IXValue where TYValue : IYValue
      {
         if (string.IsNullOrEmpty(analysisName))
            analysisName = PKSimConstants.UI.Analysis;

         var fileName = _dialogCreator.AskForFileToSave(PKSimConstants.UI.ExportPopulationAnalysisToExcelTitle, Constants.Filter.EXCEL_SAVE_FILE_FILTER, Constants.DirectoryKey.REPORT, analysisName);
         if (string.IsNullOrEmpty(fileName))
            return;

         var mapper = _chartDataToTableMapperFactory.Create<TXValue, TYValue>();
         var tables = mapper.MapFrom(chartData);
         tables.Each(t => t.TableName = $"{analysisName} {t.TableName}");

         _dataRepositoryTask.ExportToExcel(tables, fileName, launchExcel: true);
      }
   }
}
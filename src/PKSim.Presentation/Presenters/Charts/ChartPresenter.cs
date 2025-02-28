using System.Collections.Generic;
using System.Linq;
using OSPSuite.Core.Chart;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Data;
using OSPSuite.Presentation.Binders;
using OSPSuite.Presentation.Core;
using OSPSuite.Presentation.Presenters;
using OSPSuite.Presentation.Presenters.Charts;
using OSPSuite.Presentation.Services.Charts;
using OSPSuite.Utility.Collections;
using OSPSuite.Utility.Extensions;
using PKSim.Assets;
using PKSim.Core.Model;
using PKSim.Core.Services;
using PKSim.Presentation.Presenters.Simulations;
using PKSim.Presentation.Views.Charts;
using IChartTemplatingTask = PKSim.Presentation.Services.IChartTemplatingTask;

namespace PKSim.Presentation.Presenters.Charts
{
   public enum ChartDisplayMode
   {
      Chart,
      PKAnalysis
   }

   public abstract class
      ChartPresenter<TChart, TView, TPresenter> : OSPSuite.Presentation.Presenters.Charts.ChartPresenter<TChart, TView, TPresenter>,
         IPKAnalysisWithChartPresenter
      where TChart : ChartWithObservedData
      where TView : class, IChartView<TPresenter>
      where TPresenter : IPresenter
   {
      protected const string _chartDisplayModeSetting = "chartDisplayMode";

      protected readonly IIndividualPKAnalysisPresenter _pkAnalysisPresenter;
      protected readonly IChartTask _chartTask;
      protected readonly IObservedDataTask _observedDataTask;
      private readonly IChartUpdater _chartUpdater;
      private readonly bool _useSimulationNameToCreateCurveName;
      private readonly IUserSettings _userSettings;

      protected ChartDisplayMode _chartDisplayMode;
      protected readonly ICache<DataRepository, IndividualSimulation> _repositoryCache;
      private readonly ObservedDataDragDropBinder _observedDataDragDropBinder;
      protected readonly IChartTemplatingTask _chartTemplatingTask;

      protected ChartPresenter(
         TView view,
         ChartPresenterContext chartPresenterContext,
         IChartTemplatingTask chartTemplatingTask,
         IIndividualPKAnalysisPresenter pkAnalysisPresenter,
         IChartTask chartTask,
         IObservedDataTask observedDataTask,
         IChartUpdater chartUpdater, bool useSimulationNameToCreateCurveName,
         IUserSettings userSettings)
         : base(view, chartPresenterContext)
      {
         _chartTask = chartTask;
         _observedDataTask = observedDataTask;
         _chartUpdater = chartUpdater;
         _useSimulationNameToCreateCurveName = useSimulationNameToCreateCurveName;
         _view.SetChartView(chartPresenterContext.EditorAndDisplayPresenter.BaseView);
         _pkAnalysisPresenter = pkAnalysisPresenter;
         _view.SetPKAnalysisView(_pkAnalysisPresenter.View);
         AddSubPresenters(_pkAnalysisPresenter);
         _chartTemplatingTask = chartTemplatingTask;
         _repositoryCache = new Cache<DataRepository, IndividualSimulation> {OnMissingKey = noDataForSimulation};

         ChartEditorPresenter.SetShowDataColumnInDataBrowserDefinition(IsColumnVisibleInDataBrowser);
         ChartDisplayPresenter.DragDrop += OnDragDrop;
         ChartDisplayPresenter.DragOver += OnDragOver;
         AddAllButtons();
         _chartDisplayMode = ChartDisplayMode.Chart;
         _observedDataDragDropBinder = new ObservedDataDragDropBinder();
         _userSettings = userSettings;
      }

      public override void InitializeAnalysis(TChart chart)
      {
         base.InitializeAnalysis(chart);
         AddDataRepositoriesToEditor(chart.AllObservedData());
      }

      protected bool IsColumnVisibleInDataBrowser(DataColumn dataColumn) => _chartTask.IsColumnVisibleInDataBrowser(dataColumn);

      private IndividualSimulation noDataForSimulation(DataRepository dataRepository) => null;

      protected override ISimulation SimulationFor(DataColumn dataColumn)
      {
         return _repositoryCache[dataColumn.Repository];
      }

      protected DataRepository DataRepositoryFor(Simulation simulation)
      {
         return _repositoryCache.KeyValues.Where(keyValue => Equals(keyValue.Value, simulation))
            .Select(keyValue => keyValue.Key)
            .FirstOrDefault();
      }

      protected virtual void UpdateAnalysisBasedOn(IndividualSimulation simulation, DataRepository dataRepository)
      {
         //For some reasons, we land in this where the simulation does not have results but we had a chart. This can be the case after loading from snapshot
         if (dataRepository.IsNull())
            return;

         if (_repositoryCache.Contains(dataRepository))
         {
            using (_chartUpdater.UpdateTransaction(Chart))
            {
               ChartEditorPresenter.RemoveUnusedColumns();
               AddDataRepositoriesToEditor(new[] {dataRepository});
               ChartEditorPresenter.AddOutputMappings(simulation.OutputMappings);
            }

            //after refresh, some data might not be available anymore=>in that case init chart from template
            InitializeFromTemplateIfRequired();

            RefreshPKAnalysisIfVisible();
         }
         else
         {
            _repositoryCache[dataRepository] = simulation;
            AddDataRepositoriesToEditor(new[] {dataRepository});
            ChartEditorPresenter.AddOutputMappings(simulation.OutputMappings);
         }
      }

      protected override string NameForColumn(DataColumn dataColumn)
      {
         return _chartPresenterContext.CurveNamer.CurveNameForColumn(SimulationFor(dataColumn), dataColumn, _useSimulationNameToCreateCurveName);
      }

      protected void InitializeFromTemplate()
      {
         InitializeFromTemplate(_repositoryCache.Keys.SelectMany(x => x.Columns).ToList(), _repositoryCache);
      }

      protected virtual void InitializeFromTemplate(IReadOnlyCollection<DataColumn> allColumns, IReadOnlyCollection<IndividualSimulation> simulations)
      {
         _chartTemplatingTask.InitFromTemplate(Chart, _chartPresenterContext.EditorAndDisplayPresenter, allColumns, simulations, NameForColumn,
            DefaultChartTemplate);
      }

      public override void Clear()
      {
         base.Clear();
         _repositoryCache.Clear();
      }

      public void SwitchPKAnalysisPlot()
      {
         if (_chartDisplayMode == ChartDisplayMode.Chart)
            ShowAnalysis();
         else
            ShowChart();

         _settings.SetSetting(_chartDisplayModeSetting, _chartDisplayMode);
      }

      public override void LoadSettingsForSubject(IWithId subject)
      {
         base.LoadSettingsForSubject(subject);
         _pkAnalysisPresenter.LoadSettingsForSubject(subject);

         if (_settings.IsEqual(_chartDisplayModeSetting, ChartDisplayMode.PKAnalysis))
            ShowAnalysis();
         else
            ShowChart();
      }

      protected virtual void ShowChart()
      {
         _chartDisplayMode = ChartDisplayMode.Chart;
         _view.ShowChartView();
      }

      protected virtual void ShowAnalysis()
      {
         _chartDisplayMode = ChartDisplayMode.PKAnalysis;
         CalculatePKAnalysis();
         _view.ShowPKAnalysisView();
      }

      protected void RefreshPKAnalysisIfVisible()
      {
         if (_chartDisplayMode != ChartDisplayMode.PKAnalysis)
            return;
         CalculatePKAnalysis();
      }

      protected void CalculatePKAnalysis()
      {
         _pkAnalysisPresenter.ShowPKAnalysis(_repositoryCache, Chart.Curves.Where(curve => curve.Visible));
      }

      protected override void ChartChanged()
      {
         base.ChartChanged();
         RefreshPKAnalysisIfVisible();
      }

      protected virtual void AddObservedData(IReadOnlyList<DataRepository> observedData, bool asResultOfDragAndDrop)
      {
         AddDataRepositoriesToEditor(observedData);

         //make curve visible
         if (!asResultOfDragAndDrop) return;

         using (_chartUpdater.UpdateTransaction(Chart))
         {
            var columnsToAdd = observedData.SelectMany(x => x.ObservationColumns());
            columnsToAdd.Each(c => ChartEditorPresenter.AddCurveForColumn(c));
         }
      }

      protected virtual void AddColorGroupedObservedData(IReadOnlyList<IReadOnlyList<DataRepository>> observedDataListGroupedByFolder)
      {
         using (_chartUpdater.UpdateTransaction(Chart))
         {
            foreach (var observesDataListInFolder in observedDataListGroupedByFolder)
            {
               AddDataRepositoriesToEditor(observesDataListInFolder);
               var columnsToAdd = observesDataListInFolder.SelectMany(x => x.ObservationColumns());
               ChartEditorPresenter.AddCurvesWithSameColorForColumn(columnsToAdd.ToList());
            }
         }
      }

      protected virtual void OnDragOver(object sender, IDragEvent e)
      {
         _observedDataDragDropBinder.PrepareDrag(e);
      }

      protected virtual void OnDragDrop(object sender, IDragEvent e)
      {
         if (_userSettings.ColorGroupObservedDataFromSameFolder)
         {
            var observedDataListGroupedByFolder = _observedDataDragDropBinder.DroppedObservedDataWithFolderPathFrom(e);
            AddColorGroupedObservedData(observedDataListGroupedByFolder);
         }
         else
         {
            var droppedObservedData = _observedDataDragDropBinder.DroppedObservedDataFrom(e).ToList();
            AddObservedData(droppedObservedData, asResultOfDragAndDrop: true);
         }
      }

      protected override void ConfigureColumns()
      {
         base.ConfigureColumns();
         Column(AxisOptionsColumns.UnitName).Caption = PKSimConstants.UI.Unit;
      }

      protected void InitializeFromTemplateIfRequired()
      {
         if (Chart != null && !Chart.Curves.Any())
            InitializeFromTemplate();
      }
   }
}
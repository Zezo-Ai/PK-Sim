﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Formulas;
using OSPSuite.Core.Domain.Services;
using OSPSuite.Core.Domain.UnitSystem;
using OSPSuite.Core.Extensions;
using OSPSuite.Core.Services;
using OSPSuite.Utility.Extensions;
using PKSim.Assets;
using static PKSim.Core.CoreConstants.ContainerName;
using SnapshotParameter = PKSim.Core.Snapshots.Parameter;
using SnapshotTableFormula = PKSim.Core.Snapshots.TableFormula;
using ModelTableFormula = OSPSuite.Core.Domain.Formulas.TableFormula;

namespace PKSim.Core.Snapshots.Mappers
{
   public class ParameterSnapshotContext : SnapshotContext
   {
      public IParameter Parameter { get; }

      public ParameterSnapshotContext(IParameter parameter, SnapshotContext baseContext) : base(baseContext)
      {
         Parameter = parameter;
      }
   }

   public class ParameterMapper : ObjectBaseSnapshotMapperBase<IParameter, SnapshotParameter, ParameterSnapshotContext>
   {
      private readonly TableFormulaMapper _tableFormulaMapper;
      private readonly ValueOriginMapper _valueOriginMapper;
      private readonly IEntityPathResolver _entityPathResolver;
      private readonly IOSPSuiteLogger _logger;
      private readonly IContainerTask _containerTask;

      public ParameterMapper(
         TableFormulaMapper tableFormulaMapper,
         ValueOriginMapper valueOriginMapper,
         IEntityPathResolver entityPathResolver,
         IOSPSuiteLogger logger,
         IContainerTask containerTask)
      {
         _tableFormulaMapper = tableFormulaMapper;
         _valueOriginMapper = valueOriginMapper;
         _entityPathResolver = entityPathResolver;
         _logger = logger;
         _containerTask = containerTask;
      }

      public override Task<SnapshotParameter> MapToSnapshot(IParameter modelParameter)
      {
         return createFrom<SnapshotParameter>(modelParameter, x => { x.Name = modelParameter.Name; });
      }

      public virtual async Task UpdateSnapshotFromParameter(SnapshotParameter snapshot, IParameter parameter)
      {
         updateParameterValue(snapshot, parameter.Value, parameter.DisplayUnit.Name, parameter.Dimension);
         snapshot.ValueOrigin = await _valueOriginMapper.MapToSnapshot(parameter.ValueOrigin);
         snapshot.TableFormula = await mapFormula(parameter.Formula);
      }

      public virtual SnapshotParameter ParameterFrom(double? parameterBaseValue, string parameterDisplayUnit, IDimension dimension)
      {
         if (parameterBaseValue == null)
            return null;

         var snapshot = new SnapshotParameter();
         updateParameterValue(snapshot, parameterBaseValue.Value, parameterDisplayUnit, dimension);
         return snapshot;
      }

      private void updateParameterValue(SnapshotParameter snapshot, double parameterBaseValue, string parameterDisplayUnit, IDimension dimension)
      {
         var unit = dimension.UnitOrDefault(parameterDisplayUnit);
         snapshot.Unit = SnapshotValueFor(unit.Name);
         snapshot.Value = dimension.BaseUnitValueToUnitValue(unit, parameterBaseValue);
      }

      private async Task<TSnapshotParameter> createFrom<TSnapshotParameter>(IParameter parameter, Action<TSnapshotParameter> configurationAction) where TSnapshotParameter : SnapshotParameter, new()
      {
         var snapshot = new TSnapshotParameter();
         await UpdateSnapshotFromParameter(snapshot, parameter);
         configurationAction(snapshot);
         return snapshot;
      }

      public override async Task<IParameter> MapToModel(SnapshotParameter snapshot, ParameterSnapshotContext snapshotContext)
      {
         var parameter = snapshotContext.Parameter;
         ;
         _valueOriginMapper.UpdateValueOrigin(parameter.ValueOrigin, snapshot.ValueOrigin);

         //only update formula if required
         if (snapshot.TableFormula != null)
            parameter.Formula = await _tableFormulaMapper.MapToModel(snapshot.TableFormula, snapshotContext);

         if (snapshot.Value == null)
            return parameter;

         var displayUnit = ModelValueFor(snapshot.Unit);
         if (!parameter.Dimension.HasUnit(displayUnit))
            _logger.AddWarning(PKSimConstants.Warning.UnitNotFoundInDimensionForParameter(displayUnit, parameter.Dimension.Name, parameter.Name));

         parameter.DisplayUnit = parameter.Dimension.UnitOrDefault(displayUnit);

         //This needs to come AFTER formula update so that the base value is accurate
         var snapshotValueInBaseUnit = parameter.ConvertToBaseUnit(snapshot.Value);
         var (value, success) = parameter.TryGetValue();
         
         //Value could not be parsed (e.g. Initial concentration was overwritten) or the value are indeed different
         if (!success || !ValueComparer.AreValuesEqual(value, snapshotValueInBaseUnit))
         {
            parameter.Value = snapshotValueInBaseUnit;
            parameter.IsDefault = false;
         }

         return parameter;
      }

      private async Task<SnapshotTableFormula> mapFormula(IFormula formula)
      {
         if (!(formula is ModelTableFormula tableFormula))
            return null;

         return await _tableFormulaMapper.MapToSnapshot(tableFormula);
      }

      public virtual Task<LocalizedParameter> LocalizedParameterFrom(IParameter parameter)
      {
         return LocalizedParameterFrom(parameter, _entityPathResolver.PathFor);
      }

      public virtual Task<LocalizedParameter> LocalizedParameterFrom(IParameter parameter, Func<IParameter, string> pathResolverFunc)
      {
         return createFrom<LocalizedParameter>(parameter, x => { x.Path = pathResolverFunc(parameter); });
      }

      public virtual Task<LocalizedParameter[]> LocalizedParametersFrom(IEnumerable<IParameter> parameters) => orderByPath(SnapshotMapperBaseExtensions.MapTo(parameters, LocalizedParameterFrom));

      public virtual Task<LocalizedParameter[]> LocalizedParametersFrom(IEnumerable<IParameter> parameters, Func<IParameter, string> pathResolverFunc)
      {
         return orderByPath(SnapshotMapperBaseExtensions.MapTo(parameters, x => LocalizedParameterFrom(x, pathResolverFunc)));
      }

      private async Task<LocalizedParameter[]> orderByPath(Task<LocalizedParameter[]> localizedParametersTask)
      {
         var localizedParameters = await localizedParametersTask;
         return localizedParameters?.OrderBy(x => x.Path).ToArray();
      }

      public virtual Task MapLocalizedParameters(IReadOnlyList<LocalizedParameter> localizedParameters, IContainer container, SnapshotContext snapshotContext, bool showParameterNotFoundWarning = true)
      {
         //undefined or empty or actually not localized parameters (coming from conversions probably)
         if (localizedParameters == null || !localizedParameters.Any() || localizedParameters.All(x => x.Path.IsNullOrEmpty()))
            return Task.FromResult(false);

         var allParameters = _containerTask.CacheAllChildren<IParameter>(container);
         
         return mapParameters(localizedParameters, x => allParameters[adjustedPath(x, snapshotContext)], x => x.Path, container.Name, snapshotContext, showParameterNotFoundWarning);
      }

      private string adjustedPath(LocalizedParameter localizedParameter, SnapshotContext snapshotContext)
      {
         if(!snapshotContext.IsV11FormatOrEarlier)
            return localizedParameter.Path;

         //for V11 or earlier, we may have to convert the path if it starts with Applications which was removed for Events
         if (localizedParameter.Path.StartsWith(Applications))
         {
            var path = new ObjectPath(localizedParameter.Path.ToPathArray());
            path.Replace(Applications, Constants.EVENTS);
            return path.ToString();
         }

         return localizedParameter.Path; 
      }

      public virtual Task MapParameters(IReadOnlyList<SnapshotParameter> snapshots, IContainer container, string containerDescriptor, SnapshotContext snapshotContext)
      {
         return mapParameters(snapshots, x => container.Parameter(x.Name), x => x.Name, containerDescriptor, snapshotContext);
      }

      private Task mapParameters<T>(IReadOnlyList<T> snapshots, Func<T, IParameter> parameterRetrieverFunc, Func<T, string> parameterIdentifierFunc, string containerDescriptor, SnapshotContext snapshotContext, bool showParameterNotFoundWarning = true) where T : SnapshotParameter
      {
         if (snapshots == null || !snapshots.Any())
            return Task.FromResult(false);

         var tasks = new List<Task>();

         foreach (var snapshot in snapshots)
         {
            var parameter = parameterRetrieverFunc(snapshot);

            if (parameter == null)
            {
               if (showParameterNotFoundWarning)
                  _logger.AddWarning(PKSimConstants.Error.SnapshotParameterNotFoundInContainer(parameterIdentifierFunc(snapshot), containerDescriptor));
            }
            else
               tasks.Add(MapToModel(snapshot, new ParameterSnapshotContext(parameter, snapshotContext)));
         }

         return Task.WhenAll(tasks);
      }
   }
}
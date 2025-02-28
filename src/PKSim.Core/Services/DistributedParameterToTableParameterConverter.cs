﻿using System;
using System.Collections.Generic;
using System.Linq;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Builder;
using OSPSuite.Core.Domain.Formulas;
using OSPSuite.Core.Domain.Services;
using OSPSuite.Core.Domain.UnitSystem;
using OSPSuite.Core.Extensions;
using OSPSuite.Core.Maths.Interpolations;
using OSPSuite.Core.Maths.Random;
using OSPSuite.Core.Maths.Statistics;
using OSPSuite.Utility.Extensions;
using PKSim.Assets;
using PKSim.Core.Extensions;
using PKSim.Core.Mappers;
using PKSim.Core.Model;
using PKSim.Core.Repositories;
using IFormulaFactory = PKSim.Core.Model.IFormulaFactory;
using IParameterFactory = PKSim.Core.Model.IParameterFactory;

namespace PKSim.Core.Services
{
   public interface IDistributedParameterToTableParameterConverter
   {
      /// <summary>
      ///    Create a table parameter for each distributed parameter defined in the simulation subject of the simulation if the
      ///    simulation is aging
      /// </summary>
      /// <param name="simulationConfiguration"> spatialStructure that will be use to create the simulation </param>
      /// <param name="simulation"> Model less simulation that whose spatial structure will be created </param>
      /// <param name="createAgingDataInPopulationSimulation">
      ///    Set to <c>True</c>, generates the AgingData if <paramref name="simulation" /> is a population simulation. Note that
      ///    aging data will be added to the population simulation directly
      ///    and therefor modifying the instance of the simulation
      /// </param>
      void UpdateSimulationConfigurationForAging(SimulationConfiguration simulationConfiguration, Simulation simulation, bool createAgingDataInPopulationSimulation);
   }

   public class DistributedParameterToTableParameterConverter : IDistributedParameterToTableParameterConverter
   {
      private readonly IFormulaFactory _formulaFactory;
      private readonly IEntityPathResolver _entityPathResolver;
      private readonly IParameterFactory _parameterFactory;
      private readonly IParameterQuery _parameterQuery;
      private readonly IDimensionRepository _dimensionRepository;
      private readonly IOntogenyRepository _ontogenyRepository;
      private readonly IFullPathDisplayResolver _fullPathDisplayResolver;
      private readonly IInterpolation _interpolation;
      private readonly IGenderRepository _genderRepository;
      private readonly IIndividualToIndividualBuildingBlockMapper _individualBuildingBlockMapper;
      private readonly IDimension _timeDimension;
      private readonly Unit _yearUnit;
      private Simulation _simulation;
      private Individual _baseIndividual;
      private OriginData _baseOriginData;
      private IReadOnlyList<ParameterDistributionMetaData> _allHeightDistributionMaleParameters;
      private IReadOnlyList<ParameterDistributionMetaData> _allHeightDistributionFemaleParameters;
      private bool _createAgingDataInPopulationSimulation;

      public DistributedParameterToTableParameterConverter(
         IFormulaFactory formulaFactory,
         IEntityPathResolver entityPathResolver,
         IParameterFactory parameterFactory,
         IParameterQuery parameterQuery,
         IDimensionRepository dimensionRepository,
         IOntogenyRepository ontogenyRepository,
         IFullPathDisplayResolver fullPathDisplayResolver,
         IInterpolation interpolation,
         IGenderRepository genderRepository,
         IIndividualToIndividualBuildingBlockMapper individualBuildingBlockMapper
      )
      {
         _formulaFactory = formulaFactory;
         _entityPathResolver = entityPathResolver;
         _parameterFactory = parameterFactory;
         _parameterQuery = parameterQuery;
         _dimensionRepository = dimensionRepository;
         _ontogenyRepository = ontogenyRepository;
         _fullPathDisplayResolver = fullPathDisplayResolver;
         _interpolation = interpolation;
         _genderRepository = genderRepository;
         _individualBuildingBlockMapper = individualBuildingBlockMapper;
         _timeDimension = dimensionRepository.Time;
         _yearUnit = _timeDimension.Unit(dimensionRepository.AgeInYears.BaseUnit.Name);
      }

      public void UpdateSimulationConfigurationForAging(SimulationConfiguration simulationConfiguration, Simulation simulation, bool createAgingDataInPopulationSimulation)
      {
         if (!simulation.AllowAging)
            return;

         try
         {
            _simulation = simulation;
            _createAgingDataInPopulationSimulation = createAgingDataInPopulationSimulation;
            _baseIndividual = simulation.Individual;
            _baseOriginData = _baseIndividual.OriginData;
            var allHeightDistributionParameters = _parameterQuery.ParameterDistributionsFor(_baseIndividual.Organism, _baseOriginData.Population, _baseOriginData.SubPopulation, CoreConstants.Parameters.MEAN_HEIGHT);
            _allHeightDistributionMaleParameters = allHeightDistributionParameters.Where(p => p.Gender == CoreConstants.Gender.MALE).ToList();
            _allHeightDistributionFemaleParameters = allHeightDistributionParameters.Where(p => p.Gender == CoreConstants.Gender.FEMALE).ToList();
            createIndividualTableParameters(simulationConfiguration);
            createOntogenyTableParameters(simulationConfiguration);
            updateAgeParameter(simulationConfiguration);
         }
         finally
         {
            _simulation = null;
            _baseIndividual = null;
            _baseOriginData = null;
         }
      }

      private void updateAgeParameter(SimulationConfiguration simulationConfiguration)
      {
         var individual = simulationConfiguration.Individual;
         var organism = _baseIndividual.Organism;
         var ageParameter = organism.Parameter(CoreConstants.Parameters.AGE);
         var minToYearFactor = _timeDimension.BaseUnitValueToUnitValue(_yearUnit, 1);

         //dummy parameters that will be added to the individualBuildingBlock. We use a dummy organism to ensure the path of dynamic parameters are correct
         var dummyOrganism = new Organism();
         var age0Parameter = _parameterFactory.CreateFor(CoreConstants.Parameters.AGE_0, ageParameter.Value, ageParameter.Dimension.Name, PKSimBuildingBlockType.Simulation);
         age0Parameter.DisplayUnit = ageParameter.DisplayUnit;
         age0Parameter.Visible = false;

         var minToYearFactorParameter = _parameterFactory.CreateFor(CoreConstants.Parameters.MIN_TO_YEAR_FACTOR, minToYearFactor, PKSimBuildingBlockType.Simulation);
         minToYearFactorParameter.Visible = false;

         dummyOrganism.AddChildren(age0Parameter, minToYearFactorParameter);

         var _ = getOrCreateIndividualParameter(age0Parameter, individual);
         _ = getOrCreateIndividualParameter(minToYearFactorParameter, individual);
         var individualAgeParameter = getOrCreateIndividualParameter(ageParameter, individual);
         var formula = _formulaFactory.AgeFormulaFor(age0Parameter, minToYearFactorParameter);
         updateParameterToTableFormula(individualAgeParameter, formula, individual);
      }

      private void createIndividualTableParameters(SimulationConfiguration simulationConfiguration)
      {
         var individual = simulationConfiguration.Individual;
         var allBaseIndividualDistributedParameters = new PathCache<IDistributedParameter>(_entityPathResolver).For(_baseIndividual.GetAllChildren<IDistributedParameter>(parameterShouldBeDefinedAsTable));
         if (!allBaseIndividualDistributedParameters.Any())
            return;

         var populationSimulation = _simulation as PopulationSimulation;


         foreach (var individualParameterKeyValue in allBaseIndividualDistributedParameters.KeyValues)
         {
            var individualParameterPath = individualParameterKeyValue.Key;
            var individualParameter = individualParameterKeyValue.Value;
            var individualBuildingBlockParameter = individual.FindByPath(individualParameterPath);

            //if it's not found, it was not exported in the first place...should not happen but who knows
            if (individualBuildingBlockParameter == null)
               continue;

            //cache all distributions for this parameter defined for the population and sub population.
            var allDistributionsForParameter = _parameterQuery.ParameterDistributionsFor(individualParameter.ParentContainer, _baseOriginData.Population, _baseOriginData.SubPopulation, individualParameter.Name);
            var allDistributionsForMaleParameter = allDistributionsForParameter.Where(p => p.Gender == CoreConstants.Gender.MALE).ToList();
            var allDistributionsForFemaleParameter = allDistributionsForParameter.Where(p => p.Gender == CoreConstants.Gender.FEMALE).ToList();

            createIndividualTableParameter(individualBuildingBlockParameter, individualParameter, allDistributionsForMaleParameter, allDistributionsForFemaleParameter, individual);

            createPopulationTableParameter(individualParameterPath, individualParameter, populationSimulation, allDistributionsForMaleParameter, allDistributionsForFemaleParameter);
         }

         //Height parameter is not a distributed parameter. However, we need to define this parameter as table to ensure that height dependent parameters are updated properly
         var heightParameterPath = new ObjectPath(Constants.ORGANISM, CoreConstants.Parameters.HEIGHT).ToPathString();
         var heightParameter = individual.FindByPath(heightParameterPath);
         var individualMeanHeightParameter = _baseIndividual.Organism.EntityAt<IDistributedParameter>(CoreConstants.Parameters.MEAN_HEIGHT);
         createIndividualTableParameter(heightParameter, individualMeanHeightParameter, _allHeightDistributionMaleParameters, _allHeightDistributionFemaleParameters, individual);
         createPopulationHeightTableParameter(heightParameterPath, individualMeanHeightParameter, populationSimulation);
      }

      private void createOntogenyTableParameters(SimulationConfiguration simulationConfiguration)
      {
         var simulationPopulation = _simulation as PopulationSimulation;
         var individual = simulationConfiguration.Individual;
         foreach (var molecule in _baseIndividual.AllMolecules().Where(m => m.Ontogeny.IsDefined()))
         {
            var ontogenyFactorPath = _entityPathResolver.ObjectPathFor(molecule.OntogenyFactorParameter);
            var ontogenyFactorGIPath = _entityPathResolver.ObjectPathFor(molecule.OntogenyFactorGIParameter);
            createParameterValueVersionOntogenyTableParameter(molecule.OntogenyFactorParameter, individual, molecule);
            createParameterValueVersionOntogenyTableParameter(molecule.OntogenyFactorGIParameter, individual, molecule);

            createPopulationOntogenyTableParameter(molecule.OntogenyFactorParameter, ontogenyFactorPath, molecule, simulationPopulation);
            createPopulationOntogenyTableParameter(molecule.OntogenyFactorGIParameter, ontogenyFactorGIPath, molecule, simulationPopulation);
         }

         createPlasmaProteinOntogenyTable(individual);
      }

      private void createPlasmaProteinOntogenyTable(IndividualBuildingBlock individual)
      {
         var organism = _baseIndividual.Organism;
         foreach (var ontogenyParameterName in CoreConstants.Parameters.AllPlasmaProteinOntogenyFactors)
         {
            var parameter = organism.Parameter(ontogenyParameterName);
            var formula = createPlasmaProteinOntogenyTableFormulaFrom(parameter, _baseOriginData);
            if (formula == null)
               continue;

            var individualParameter = getOrCreateIndividualParameter(parameter, individual);
            updateParameterToTableFormula(individualParameter, formula, individual);
            createPopulationPlasmaProteinOntogenyTableParameter(parameter, _simulation as PopulationSimulation);
         }
      }

      private void createParameterValueVersionOntogenyTableParameter(
         IParameter ontogenyFactorParameter,
         IndividualBuildingBlock individual,
         IndividualMolecule molecule)
      {
         var individualParameter = createIndividualParameter(ontogenyFactorParameter, individual);
         individualParameter.Formula = createOntogenyTableFormulaFrom(ontogenyFactorParameter, molecule.Ontogeny, _baseOriginData);
         if (individualParameter.Formula == null)
            return;

         individualParameter.Value = null;
         individual[individualParameter.Path] = individualParameter;
         individual.AddFormula(individualParameter.Formula);
      }

      private TableFormula createMoleculeOntogenyTableFormula(IParameter ontogenyFactor, OriginData originData, IReadOnlyList<Sample> allOntogenies)
      {
         //null is ok here. It's the default value for formula in ParameterValue
         if (allOntogenies.Count == 0)
            return null;

         var tableFormula = _formulaFactory.CreateTableFormula();
         updateTableFormulaFrom(tableFormula, ontogenyFactor);

         //0 because of the offset with age
         tableFormula.AddPoint(0, ontogenyFactor.Value);

         foreach (var ontogenyForAge in allOntogenies)
         {
            var age = ageWithOffsetInMin(ontogenyForAge.X, originData.Age.Value);
            tableFormula.AddPoint(age, ontogenyForAge.Y);
         }

         tableFormula.UseDerivedValues = false;

         return tableFormula;
      }

      private TableFormula createOntogenyTableFormulaFrom(IParameter ontogenyFactor, Ontogeny ontogeny, OriginData originData, RandomGenerator randomize = null)
      {
         var containerName = containerNameForOntogenyFactor(ontogenyFactor);

         var allOntogenies = _ontogenyRepository.AllOntogenyFactorForStrictBiggerThanPMA(ontogeny, originData, containerName, randomize).ToList();
         return createMoleculeOntogenyTableFormula(ontogenyFactor, originData, allOntogenies);
      }

      private static string containerNameForOntogenyFactor(IParameter ontogenyFactor)
      {
         if (ontogenyFactor.IsNamed(CoreConstants.Parameters.ONTOGENY_FACTOR_GI))
            return CoreConstants.Groups.ONTOGENY_DUODENUM;

         return CoreConstants.Groups.ONTOGENY_LIVER;
      }

      private TableFormula createPlasmaProteinOntogenyTableFormulaFrom(IParameter ontogenyFactor, OriginData originData, RandomGenerator randomize = null)
      {
         var allOntogenies = _ontogenyRepository.AllPlasmaProteinOntogenyFactorForStrictBiggerThanPMA(ontogenyFactor.Name, originData, randomize).ToList();
         return createMoleculeOntogenyTableFormula(ontogenyFactor, originData, allOntogenies);
      }

      private bool parameterShouldBeDefinedAsTable(IDistributedParameter parameter)
      {
         return !parameter.NameIsOneOf(CoreConstants.Parameters.MEAN_HEIGHT, CoreConstants.Parameters.MEAN_WEIGHT);
      }

      private void createPopulationTableParameter(
         string individualParameterPath,
         IDistributedParameter individualParameter,
         PopulationSimulation populationSimulation,
         IReadOnlyList<ParameterDistributionMetaData> allDistributionsForMaleParameter,
         IReadOnlyList<ParameterDistributionMetaData> allDistributionsForFemaleParameter)
      {
         if (populationSimulation == null)
            return;

         addAgingDataToPopulationSimulation(populationSimulation, individualParameterPath, individualParameter,
            p =>
            {
               var distributions = allDistributionsWithAgeStrictBiggerThanOriginData(allDistributionsForMaleParameter, allDistributionsForFemaleParameter, p.OriginData);
               return createTableFormulaFrom(p, distributions);
            });
      }

      private void createPopulationHeightTableParameter(
         string heightParameterPath,
         IDistributedParameter meanHeightParameter,
         PopulationSimulation populationSimulation
      )
      {
         if (populationSimulation == null || !_createAgingDataInPopulationSimulation)
            return;

         var originData = _baseOriginData.Clone();
         var allAges = populationSimulation.AllOrganismValuesFor(CoreConstants.Parameters.AGE, _entityPathResolver);
         var allGAs = populationSimulation.AllOrganismValuesFor(Constants.Parameters.GESTATIONAL_AGE, _entityPathResolver);
         var allHeights = populationSimulation.AllOrganismValuesFor(CoreConstants.Parameters.HEIGHT, _entityPathResolver);
         var allGender = populationSimulation.AllGenders(_genderRepository).ToList();

         var tableFormulaParameter = new TableFormulaParameter<IDistributedParameter> {OriginData = originData, Parameter = meanHeightParameter};
         for (int individualIndex = 0; individualIndex < populationSimulation.NumberOfItems; individualIndex++)
         {
            //create origin data for individual i
            originData.Age.Value = allAges[individualIndex];
            originData.GestationalAge.Value = allGAs[individualIndex];
            originData.Height.Value = allHeights[individualIndex];
            originData.Gender = allGender[individualIndex];
            tableFormulaParameter.Value = originData.Height.Value;

            var heightDistributions = heightDistributionsFor(originData);
            tableFormulaParameter.Percentile = heightDistributions.currentPercentile;

            var distributions = allDistributionsWithAgeStrictBiggerThanOriginData(_allHeightDistributionMaleParameters, _allHeightDistributionFemaleParameters, tableFormulaParameter.OriginData);
            var tableFormula = createTableFormulaFrom(tableFormulaParameter, distributions);
            if (tableFormula == null)
               continue;

            populationSimulation.AddAgingTableFormula(heightParameterPath, individualIndex, tableFormula);
         }
      }

      private void createPopulationOntogenyTableParameter(IParameter ontogenyFactorParameter, ObjectPath ontogenyFactorPath, IndividualMolecule molecule, PopulationSimulation populationSimulation)
      {
         addAgingDataToPopulationSimulation(populationSimulation, ontogenyFactorPath.ToString(), ontogenyFactorParameter,
            p => createOntogenyTableFormulaFrom(p.Parameter, molecule.Ontogeny, p.OriginData, populationSimulation.RandomGenerator));
      }

      private void createPopulationPlasmaProteinOntogenyTableParameter(IParameter ontogenyFactorParameter, PopulationSimulation populationSimulation)
      {
         if (populationSimulation == null) return;

         var parameterPath = _entityPathResolver.PathFor(ontogenyFactorParameter);
         addAgingDataToPopulationSimulation(populationSimulation, parameterPath, ontogenyFactorParameter,
            p => createPlasmaProteinOntogenyTableFormulaFrom(ontogenyFactorParameter, _baseOriginData, populationSimulation.RandomGenerator));
      }

      private void addAgingDataToPopulationSimulation<TParameter>(
         PopulationSimulation populationSimulation,
         string parameterPath,
         TParameter parameter,
         Func<TableFormulaParameter<TParameter>, TableFormula> tableFormulaRetriever) where TParameter : IParameter
      {
         if (populationSimulation == null || !_createAgingDataInPopulationSimulation) return;

         var originData = _baseOriginData.Clone();
         var allAges = populationSimulation.AllOrganismValuesFor(CoreConstants.Parameters.AGE, _entityPathResolver);
         var allGAs = populationSimulation.AllOrganismValuesFor(Constants.Parameters.GESTATIONAL_AGE, _entityPathResolver);
         var allHeights = populationSimulation.AllOrganismValuesFor(CoreConstants.Parameters.HEIGHT, _entityPathResolver);
         var allGender = populationSimulation.AllGenders(_genderRepository).ToList();
         var allValues = populationSimulation.AllValuesFor(parameterPath).ToList();
         var allPercentiles = populationSimulation.AllPercentilesFor(parameterPath)
            .Select(x => x.CorrectedPercentileValue()).ToList();

         var tableFormulaParameter = new TableFormulaParameter<TParameter> {OriginData = originData, Parameter = parameter};
         for (int individualIndex = 0; individualIndex < populationSimulation.NumberOfItems; individualIndex++)
         {
            //create origin data for individual i
            originData.Age.Value = allAges[individualIndex];
            originData.GestationalAge.Value = allGAs[individualIndex];
            originData.Height.Value = allHeights[individualIndex];
            originData.Gender = allGender[individualIndex];
            tableFormulaParameter.Value = allValues[individualIndex];
            tableFormulaParameter.Percentile = allPercentiles[individualIndex];

            var tableFormula = tableFormulaRetriever(tableFormulaParameter);
            if (tableFormula == null)
               continue;

            populationSimulation.AddAgingTableFormula(parameterPath, individualIndex, tableFormula);
         }
      }

      private void createIndividualTableParameter(
         IndividualParameter individualParameter,
         IDistributedParameter baseIndividualParameter,
         IReadOnlyList<ParameterDistributionMetaData> distributionsForMale,
         IReadOnlyList<ParameterDistributionMetaData> distributionsForFemale,
         IndividualBuildingBlock individualBuildingBlock)
      {
         var allDistributionsForParameter = allDistributionsWithAgeStrictBiggerThanOriginData(distributionsForMale, distributionsForFemale, _baseIndividual.OriginData);
         if (!allDistributionsForParameter.Any())
            return;

         //remove the parameter from the parent container and add a new one that will contain the table formula
         //retrieve the table formula corresponding to the individual values
         individualParameter.Info.ReadOnly = true;
         var formula = createTableFormulaFrom(baseIndividualParameter, allDistributionsForParameter);
         updateParameterToTableFormula(individualParameter, formula, individualBuildingBlock);
      }

      private IReadOnlyList<ParameterDistributionMetaData> allDistributionsWithAgeStrictBiggerThanOriginData(
         IReadOnlyList<ParameterDistributionMetaData> distributionsForMale,
         IReadOnlyList<ParameterDistributionMetaData> distributionsForFemale, OriginData originData)
      {
         return allDistributionsFor(distributionsForMale, distributionsForFemale, originData, x => x.Age > originData.Age.Value);
      }

      private IReadOnlyList<ParameterDistributionMetaData> allDistributionsFor(
         IReadOnlyList<ParameterDistributionMetaData> distributionsForMale,
         IReadOnlyList<ParameterDistributionMetaData> distributionsForFemale,
         OriginData originData,
         Func<ParameterDistributionMetaData, bool> criteriaFunc = null)
      {
         var criteria = criteriaFunc ?? (x => true);

         var distributions = distributionsForMale;
         if (originData.Gender.Name == CoreConstants.Gender.FEMALE)
            distributions = distributionsForFemale;

         return distributions.Where(criteria).DefinedFor(originData);
      }

      private TableFormula createTableFormulaFrom(IDistributedParameter individualParameter, IReadOnlyList<ParameterDistributionMetaData> allDistributionsWithAgeStrictBiggerThanOriginData)
      {
         var parameter = new TableFormulaParameter<IDistributedParameter>
         {
            OriginData = _baseOriginData,
            Parameter = individualParameter,
            Value = individualParameter.Value,
            Percentile = individualParameter.Percentile
         };

         return createTableFormulaFrom(parameter, allDistributionsWithAgeStrictBiggerThanOriginData);
      }

      private TableFormula createTableFormulaFrom(TableFormulaParameter<IDistributedParameter> parameter, IReadOnlyList<ParameterDistributionMetaData> allDistributionsWithAgeStrictBiggerThanOriginData)
      {
         if (allDistributionsWithAgeStrictBiggerThanOriginData.Count == 0)
            return null;

         var tableFormula = _formulaFactory.CreateDistributedTableFormula();
         updateTableFormulaFrom(tableFormula, parameter.Parameter);
         tableFormula.Percentile = parameter.Percentile;

         if (parameter.PercentileIsInvalid)
            throw new PKSimException(PKSimConstants.Error.CannotCreateAgingSimulationWithInvalidPercentile(_fullPathDisplayResolver.FullPathFor(parameter.Parameter), parameter.Percentile));

         //0 because of the offset with age
         tableFormula.AddPoint(0, parameter.Value, DistributionMetaData.From(parameter.Parameter));

         foreach (var scaledDistribution in scaleDistributions(parameter, allDistributionsWithAgeStrictBiggerThanOriginData))
         {
            var age = ageWithOffsetInMin(scaledDistribution.Age, parameter.OriginData.Age.Value);
            var value = valueFrom(scaledDistribution, parameter.Percentile);
            tableFormula.AddPoint(age, value, DistributionMetaData.From(scaledDistribution));
         }

         return tableFormula;
      }

      private (double meanHeight, double currentHeight, double currentPercentile, Func<OriginData, (double mean, double deviation)> distributionSamples) heightDistributionsFor(OriginData originData)
      {
         //retrieve the height distribution for the original individual 
         var heightDistributions = allDistributionsFor(_allHeightDistributionMaleParameters, _allHeightDistributionFemaleParameters, originData);

         var distributionSamples = distributionSamplesFor(heightDistributions);
         var (meanHeight, deviation) = distributionSamples(originData);
         var heightDistributionFormula = createDistributionFrom(DistributionType.Normal, meanHeight, deviation);

         double currentHeight = originData.Height.Value;
         double currentPercentile = heightDistributionFormula.CalculatePercentileForValue(currentHeight).CorrectedPercentileValue();

         return (meanHeight, currentHeight, currentPercentile, distributionSamples);
      }

      private IReadOnlyList<ParameterDistributionMetaData> scaleDistributions(TableFormulaParameter<IDistributedParameter> parameter, IReadOnlyList<ParameterDistributionMetaData> distributionsToScale)
      {
         //Retrieve the mean height parameter for the given origin data
         var originData = parameter.OriginData;
         var individualParameter = parameter.Parameter;

         bool needHeightScaling = needScaling(originData, individualParameter);
         if (!needHeightScaling)
            return distributionsToScale;

         var (meanHeight, currentHeight, currentPercentile, heightDistributionSamples) = heightDistributionsFor(originData);

         //same height, not need to scale
         if (ValueComparer.AreValuesEqual(meanHeight, currentHeight))
            return distributionsToScale;

         //is used in order to retrieve the percentile 
         double alpha = individualParameter.ParentContainer.Parameter(CoreConstants.Parameters.ALLOMETRIC_SCALE_FACTOR).Value;

         var currentOriginData = originData.Clone();
         var scaledParameterDistributionMetaData = new List<ParameterDistributionMetaData>();
         foreach (var originDistributionMetaData in distributionsToScale)
         {
            var distributionMetaData = ParameterDistributionMetaData.From(originDistributionMetaData);
            currentOriginData.Age.Value = originDistributionMetaData.Age;

            var (mean, deviation) = heightDistributionSamples(currentOriginData);
            double heightAtPercentile = valueFrom(DistributionType.Normal, mean, deviation, currentPercentile);
            double hrelForAge = heightAtPercentile / mean;

            scaleDistributionMetaData(distributionMetaData, hrelForAge, alpha);
            scaledParameterDistributionMetaData.Add(distributionMetaData);
         }

         return scaledParameterDistributionMetaData;
      }

      private Func<OriginData, (double mean, double deviation)> distributionSamplesFor(IReadOnlyList<ParameterDistributionMetaData> distributions)
      {
         var knownSamples = from distribution in distributions
            select new
            {
               Mean = new Sample(distribution.Age, distribution.Mean),
               Std = new Sample(distribution.Age, distribution.Deviation),
            };

         knownSamples = knownSamples.ToList();

         return (originData) =>
         {
            double mean = _interpolation.Interpolate(knownSamples.Select(item => item.Mean), originData.Age.Value);
            double deviation = _interpolation.Interpolate(knownSamples.Select(item => item.Std), originData.Age.Value);

            return (mean, deviation);
         };
      }

      private static bool needScaling(OriginData originData, IParameter individualParameter)
      {
         if (!originData.Population.IsHeightDependent)
            return false;

         if (!individualParameter.IsNamed(Constants.Parameters.VOLUME))
            return false;

         if (!individualParameter.ParentContainer.IsAnImplementationOf<Organ>())
            return false;

         //Volume in organ
         return true;
      }

      private IDistribution createDistributionFrom(DistributionType distributionType, double mean, double deviation)
      {
         if (distributionType == DistributionType.LogNormal)
            return new LogNormalDistribution(Math.Log(mean), Math.Log(deviation));

         if (distributionType == DistributionType.Normal)
            return new NormalDistribution(mean, deviation);

         return new UniformDistribution(mean, mean);
      }

      private double valueFrom(ParameterDistributionMetaData distributionMetaData, double percentile)
      {
         return valueFrom(distributionMetaData.Distribution, distributionMetaData.Mean, distributionMetaData.Deviation, percentile);
      }

      private double valueFrom(DistributionType distributionType, double mean, double deviation, double percentile)
      {
         return createDistributionFrom(distributionType, mean, deviation).CalculateValueFromPercentile(percentile);
      }

      private void scaleDistributionMetaData(ParameterDistributionMetaData parameterDistributionMeta, double hrel, double alpha)
      {
         var scale = Math.Pow(hrel, alpha);
         parameterDistributionMeta.Mean *= scale;
         if (parameterDistributionMeta.Distribution == DistributionType.Normal)
         {
            parameterDistributionMeta.Deviation *= scale;
         }
      }

      private void updateTableFormulaFrom(TableFormula tableFormula, IParameter parameter)
      {
         tableFormula.Name = _fullPathDisplayResolver.FullPathFor(parameter);
         tableFormula.InitializedWith(PKSimConstants.UI.SimulationTime, parameter.Name, _dimensionRepository.Time, parameter.Dimension);
         tableFormula.XDisplayUnit = _yearUnit;
      }

      private double ageWithOffsetInMin(double ageInYears, double originDataAgeInYears)
      {
         var futureAge = ageInYears - originDataAgeInYears;
         return _timeDimension.UnitValueToBaseUnitValue(_yearUnit, futureAge);
      }

      private IndividualParameter createIndividualParameter(IParameter parameter, IndividualBuildingBlock individual) => _individualBuildingBlockMapper.MapParameter(parameter, _baseIndividual, individual.FormulaCache);

      private IndividualParameter getOrCreateIndividualParameter(IParameter parameter, IndividualBuildingBlock individual)
      {
         var path = _entityPathResolver.ObjectPathFor(parameter);
         var individualParameter = individual[path];
         if (individualParameter == null)
         {
            individualParameter = createIndividualParameter(parameter, individual);
            individual.Add(individualParameter);
         }

         return individualParameter;
      }

      private void updateParameterToTableFormula(IndividualParameter individualParameter, IFormula formula, IndividualBuildingBlock individualBuildingBlock)
      {
         //set the distribution to null so that the parameter is not created as a distributed parameter anymore
         individualParameter.DistributionType = null;
         individualParameter.Formula = formula;
         //Ensures that value is null so that we do not override the formula
         individualParameter.Value = null;
         individualBuildingBlock.AddFormula(formula);
      }

      /// <summary>
      ///    Help class used to collect parameters required to create a table parameters
      /// </summary>
      private class TableFormulaParameter<TParameter> where TParameter : IParameter
      {
         /// <summary>
         ///    Origin Data for which the table should be created. This is not necessarily the origin data from the individual
         ///    when used in a population
         /// </summary>
         public OriginData OriginData { get; set; }

         /// <summary>
         ///    The actual parameter for which a table should be generated
         /// </summary>
         public TParameter Parameter { get; set; }

         /// <summary>
         ///    The value of the parameter. This is not necessarily the value of the parameter when used in a population
         /// </summary>
         public double Value { get; set; }

         /// <summary>
         ///    The percentile in the distribution for the Value; This is not necessarily the percentile of the parameter when used
         ///    in a population
         ///    This is also only used if the parameter is distributed
         /// </summary>
         public double Percentile { get; set; }

         public bool PercentileIsInvalid => !Percentile.IsValidPercentile();
      }
   }
}
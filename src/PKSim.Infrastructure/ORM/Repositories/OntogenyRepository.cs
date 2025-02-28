﻿using System;
using System.Collections.Generic;
using System.Linq;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Data;
using OSPSuite.Core.Domain.Formulas;
using OSPSuite.Core.Extensions;
using OSPSuite.Core.Maths.Interpolations;
using OSPSuite.Core.Maths.Random;
using OSPSuite.Core.Services;
using OSPSuite.Utility.Collections;
using OSPSuite.Utility.Extensions;
using PKSim.Assets;
using PKSim.Core;
using PKSim.Core.Model;
using PKSim.Core.Repositories;
using PKSim.Core.Services;
using static PKSim.Core.CoreConstants.Groups;
using static PKSim.Core.CoreConstants.Parameters;
using IFormulaFactory = PKSim.Core.Model.IFormulaFactory;

namespace PKSim.Infrastructure.ORM.Repositories
{
   public class OntogenyRepository : StartableRepository<Ontogeny>, IOntogenyRepository
   {
      private readonly IFlatOntogenyRepository _flatOntogenyRepository;
      private readonly IInterpolation _interpolation;
      private readonly IObjectBaseFactory _objectBaseFactory;
      private readonly IDimensionRepository _dimensionRepository;
      private readonly List<Ontogeny> _allOntogenies;
      private readonly ICache<CompositeKey, IReadOnlyList<OntogenyMetaData>> _ontogenyValues;
      public IReadOnlyList<SupportedProtein> SupportedProteins { get; }
      private readonly IDisplayUnitRetriever _displayUnitRetriever;
      private readonly IFormulaFactory _formulaFactory;
      private readonly IGroupRepository _groupRepository;
      private readonly ICloner _cloner;

      public OntogenyRepository(
         IFlatOntogenyRepository flatOntogenyRepository,
         IInterpolation interpolation,
         IObjectBaseFactory objectBaseFactory,
         IDimensionRepository dimensionRepository,
         IDisplayUnitRetriever displayUnitRetriever,
         IFormulaFactory formulaFactory,
         IGroupRepository groupRepository,
         ICloner cloner)
      {
         _flatOntogenyRepository = flatOntogenyRepository;
         _interpolation = interpolation;
         _objectBaseFactory = objectBaseFactory;
         _dimensionRepository = dimensionRepository;
         _displayUnitRetriever = displayUnitRetriever;
         _formulaFactory = formulaFactory;
         _groupRepository = groupRepository;
         _cloner = cloner;
         _allOntogenies = new List<Ontogeny>();
         _ontogenyValues = new Cache<CompositeKey, IReadOnlyList<OntogenyMetaData>>(onMissingKey: x => new List<OntogenyMetaData>());
         
         SupportedProteins = new List<SupportedProtein>
         {
            new SupportedProtein(CoreConstants.Molecule.AGP, ONTOGENY_FACTOR_AGP, ONTOGENY_FACTOR_AGP_TABLE),
            new SupportedProtein(CoreConstants.Molecule.ALBUMIN, ONTOGENY_FACTOR_ALBUMIN, ONTOGENY_FACTOR_ALBUMIN_TABLE),
         };
      }

      public override IEnumerable<Ontogeny> All()
      {
         Start();
         return _allOntogenies;
      }

      protected override void DoStart()
      {
         var moleculeWithDefinedOntogenies = (from flatOntogeny in _flatOntogenyRepository.All()
               select new
               {
                  flatOntogeny.MoleculeName,
                  flatOntogeny.SpeciesName,
                  flatOntogeny.DisplayName,
               }
            ).Distinct();

         foreach (var moleculeWithOntogeny in moleculeWithDefinedOntogenies)
         {
            var newOntogeny = _objectBaseFactory.Create<DatabaseOntogeny>().WithName(moleculeWithOntogeny.MoleculeName);
            newOntogeny.SpeciesName = moleculeWithOntogeny.SpeciesName;
            newOntogeny.DisplayName = moleculeWithOntogeny.DisplayName;
            _allOntogenies.Add(newOntogeny);

            //cache ontogeny values using {Enzyme name, species name} as key
            var ontogeny = moleculeWithOntogeny;

            var allOntogenies = from flatOntogeny in _flatOntogenyRepository.All()
               where flatOntogeny.MoleculeName.Equals(ontogeny.MoleculeName)
               where flatOntogeny.SpeciesName.Equals(ontogeny.SpeciesName)
               select flatOntogeny;

            _ontogenyValues.Add(ontogenyKey(ontogeny.MoleculeName, ontogeny.SpeciesName), allOntogenies.ToList());
         }
      }

      public IReadOnlyList<Ontogeny> AllFor(string speciesName)
      {
         return allFor(speciesName).Where(x => !x.NameIsOneOf(SupportedProteins.AllNames())).ToList();
      }

      private IReadOnlyList<Ontogeny> allFor(string speciesName)
      {
         return All().Where(o => o.SpeciesName.Equals(speciesName)).ToList();
      }

      public double OntogenyFactorFor(Ontogeny ontogeny, string containerName, OriginData originData, RandomGenerator randomGenerator = null)
      {
         return OntogenyFactorFor(ontogeny, containerName, originData.Age?.Value, originData.GestationalAge?.Value, randomGenerator);
      }

      public double OntogenyFactorFor(Ontogeny ontogeny, string containerName, double? age, double? gestationalAge, RandomGenerator randomGenerator)
      {
         var allOntogenies = AllValuesFor(ontogeny, containerName);
         if (!allOntogenies.Any())
            return CoreConstants.DEFAULT_ONTOGENY_FACTOR;

         var factorRetriever = ontogenyFactorRetriever(randomGenerator, allOntogenies);
         return _interpolation.Interpolate(allOntogenies.Select(x => new Sample(x.PostmenstrualAge, factorRetriever(x))), postmenstrualAgeInYearsFor(age, gestationalAge));
      }

      public DistributedTableFormula DataRepositoryToDistributedTableFormula(DataRepository ontogenyDataRepository)
      {
         var baseGrid = ontogenyDataRepository.BaseGrid;
         var valueColumns = ontogenyDataRepository.AllButBaseGrid().ToList();
         DataColumn meanColumn, deviationColumn;

         if (valueColumns.Count == 1)
         {
            meanColumn = valueColumns[0];
            //dummy deviation filled with 1 since this was not defined in the import action
            deviationColumn = new DataColumn(Constants.Distribution.DEVIATION, _dimensionRepository.NoDimension, baseGrid)
            {
               Values = new float[baseGrid.Count].InitializeWith(1f)
            };
         }
         else
         {
            //the main column is the one that has related column
            var mainColumn = valueColumns.Single(x => x.RelatedColumns.Any());
            var relatedColumn = mainColumn.RelatedColumns.Single();
            var auxiliaryType = relatedColumn.DataInfo.AuxiliaryType;

            //this is the way data repository are created when coming from the PKSim DB
            if (auxiliaryType == AuxiliaryType.GeometricMeanPop)
            {
               meanColumn = relatedColumn;
               deviationColumn = mainColumn;
            }
            else
            {
               //this the way the column are created when coming from an excel file
               meanColumn = mainColumn;
               deviationColumn = relatedColumn;
            }
         }

         var formula = _formulaFactory.CreateDistributedTableFormula().WithName(ontogenyDataRepository.Name);
         formula.InitializedWith(PMA, ontogenyDataRepository.Name, baseGrid.Dimension, meanColumn.Dimension);
         formula.XDisplayUnit = baseGrid.Dimension.Unit(baseGrid.DataInfo.DisplayUnitName);
         formula.YDisplayUnit = meanColumn.Dimension.Unit(meanColumn.DataInfo.DisplayUnitName);

         foreach (var ageValue in baseGrid.Values)
         {
            var mean = meanColumn.GetValue(ageValue).ToDouble();
            var pma = ageValue.ToDouble();
            var deviation = deviationColumn.GetValue(ageValue).ToDouble();
            var distribution = new DistributionMetaData {Mean = mean, Deviation = deviation, Distribution = DistributionType.LogNormal};
            formula.AddPoint(pma, mean, distribution);
         }

         return formula;
      }

      public DistributedTableFormula OntogenyToDistributedTableFormula(Ontogeny ontogeny, string containerName)
      {
         //We use a clone in this case to ensure that we have two distinct references to the same table 
         //This would lead otherwise to the same table being referenced by a parameter and the ontogeny, and an issue will arise when loading
         if (ontogeny is UserDefinedOntogeny userDefinedOntogeny)
            return _cloner.Clone(userDefinedOntogeny.Table);

         if (ontogeny is NullOntogeny)
            return null;

         var dataRepository = OntogenyToRepository(ontogeny, containerName);
         return DataRepositoryToDistributedTableFormula(dataRepository);
      }

      public DistributedTableFormula PlasmaProteinOntogenyTableFormula(string protein, OriginData originData)
      {
         var ontogeny = forMolecule(originData, protein);
         return OntogenyToDistributedTableFormula(ontogeny, ONTOGENY_PLASMA);
      }

      public double PlasmaProteinOntogenyValueFor(string protein, double? age, double? gestationalAge, string species, RandomGenerator randomGenerator)
      {
         var ontogeny = allFor(species).FindByName(protein) ?? new NullOntogeny();
         return OntogenyFactorFor(ontogeny, ONTOGENY_PLASMA, age, gestationalAge, randomGenerator);
      }

      private Ontogeny forMolecule(OriginData originData, string molecule)
      {
         return allFor(originData.Species.Name).FindByName(molecule) ?? new NullOntogeny();
      }

      public DataRepository OntogenyToRepository(Ontogeny ontogeny, string containerName)
      {
         var group = _groupRepository.GroupByName(containerName);
         var dataRepository = new DataRepository {Name = PKSimConstants.UI.OntogenyFor(ontogeny.DisplayName, group.DisplayName)};
         var xDimension = _dimensionRepository.AgeInYears;
         var yDimension = _dimensionRepository.NoDimension;
         var xUnit = _displayUnitRetriever.PreferredUnitFor(xDimension);
         var yUnit = _displayUnitRetriever.PreferredUnitFor(yDimension);

         var pma = new BaseGrid(PKSimConstants.UI.PostMenstrualAge, xDimension) {DisplayUnit = xUnit};
         var mean = new DataColumn(dataRepository.Name, yDimension, pma) {DisplayUnit = yUnit};
         var geoSD = new DataColumn(PKSimConstants.UI.GeometricStandardDeviation, yDimension, pma) {DisplayUnit = yUnit};
         //note:The main column for a range plot is the range area. The related column is the mean value. Hence the mean
         //becomes the related column of the geoSD
         mean.DataInfo.AuxiliaryType = AuxiliaryType.GeometricMeanPop;
         geoSD.AddRelatedColumn(mean);
         dataRepository.Add(mean);
         dataRepository.Add(geoSD);

         var allOntogenies = AllValuesFor(ontogeny, containerName).OrderBy(x => x.PostmenstrualAge).ToList();
         pma.Values = values(allOntogenies, x => x.PostmenstrualAge);
         mean.Values = values(allOntogenies, x => x.OntogenyFactor);
         geoSD.Values = values(allOntogenies, x => x.Deviation);
         return dataRepository;
      }

      private float[] values(IEnumerable<OntogenyMetaData> allOntogenies, Func<OntogenyMetaData, double> valueFunc) => allOntogenies.Select(valueFunc).ToFloatArray();

      public IReadOnlyList<Sample> AllOntogenyFactorForStrictBiggerThanPMA(Ontogeny ontogeny, OriginData originData, string containerName, RandomGenerator randomGenerator = null)
      {
         Start();

         if (ontogeny.IsUndefined())
            return new List<Sample>();

         var pma = postmenstrualAgeInYearsFor(originData);
         var gaInYears = inYears(originData.GestationalAge?.Value ?? CoreConstants.NOT_PRETERM_GESTATIONAL_AGE_IN_WEEKS);

         var ontogenies = AllValuesFor(ontogeny, containerName)
            .Where(x => x.PostmenstrualAge > pma)
            .OrderBy(x => x.PostmenstrualAge).ToList();

         if (!ontogenies.Any())
            return new List<Sample>();

         var factorRetriever = ontogenyFactorRetriever(randomGenerator, ontogenies);
         return ontogenies.Select(x => new Sample(x.PostmenstrualAge - gaInYears, factorRetriever(x))).ToList();
      }

      public (double mean, double std, DistributionType distributionType) OntogenyParameterDistributionFor(Ontogeny ontogeny, OriginData originData, string containerName)
      {
         var distributions = AllValuesFor(ontogeny, containerName).OrderBy(x => x.PostmenstrualAge).ToList();
         var pma = postmenstrualAgeInYearsFor(originData);

         var knownSamples = distributions.Select(x => new
         {
            Mean = new Sample(x.PostmenstrualAge, x.OntogenyFactor),
            Std = new Sample(x.PostmenstrualAge, x.Deviation)
         }).ToList();

         var mean = _interpolation.Interpolate(knownSamples.Select(item => item.Mean), pma);
         var std = _interpolation.Interpolate(knownSamples.Select(item => item.Std), pma);

         return (mean, std, DistributionType.LogNormal);
      }

      public IReadOnlyList<Sample> AllPlasmaProteinOntogenyFactorForStrictBiggerThanPMA(string parameterName, OriginData originData, RandomGenerator randomGenerator = null)
      {
         var molecule = SupportedProteins.Single(x => string.Equals(x.ParameterName, parameterName));
         return AllOntogenyFactorForStrictBiggerThanPMA(forMolecule(originData, molecule.Name), originData, ONTOGENY_PLASMA, randomGenerator);
      }

      private Func<OntogenyMetaData, double> ontogenyFactorRetriever(RandomGenerator randomGenerator, IReadOnlyList<OntogenyMetaData> allOntogenies)
      {
         if (randomGenerator == null || !allOntogenies.Any())
            return x => x.OntogenyFactor;

         var percentile = allOntogenies.Select(x => x.RandomizedPercentile(randomGenerator))
            .FirstOrDefault(p => !double.IsNaN(p));

         return x => x.RandomizedFactor(percentile);
      }

      private double postmenstrualAgeInYearsFor(OriginData originData)
      {
         return postmenstrualAgeInYearsFor(originData.Age?.Value, originData.GestationalAge?.Value);
      }

      private double postmenstrualAgeInYearsFor(double? ageInYears, double? gestationalAge)
      {
         if (!ageInYears.HasValue)
            return double.NaN;

         //get interpolated value for age. Age is given in year. 
         var gaOffset = gestationalAge.GetValueOrDefault(CoreConstants.NOT_PRETERM_GESTATIONAL_AGE_IN_WEEKS);
         return ageInYears.Value + inYears(gaOffset);
      }

      private double inYears(double valueInWeeks)
      {
         var ageInWeeksDimension = _dimensionRepository.AgeInWeeks;
         return ageInWeeksDimension.BaseUnitValueToUnitValue(ageInWeeksDimension.Unit(CoreConstants.Units.Years), valueInWeeks);
      }

      private CompositeKey ontogenyKey(string moleculeName, string speciesName)
      {
         return new CompositeKey(moleculeName, speciesName);
      }

      public IReadOnlyList<OntogenyMetaData> AllValuesFor(Ontogeny ontogeny)
      {
         Start();

         if (ontogeny.IsUndefined())
            return new List<OntogenyMetaData>();

         if (ontogeny.IsUserDefined())
            return userDefinedOntogenyValues(ontogeny);

         return _ontogenyValues[ontogenyKey(ontogeny.Name, ontogeny.SpeciesName)];
      }

      private IReadOnlyList<OntogenyMetaData> userDefinedOntogenyValues(Ontogeny ontogeny)
      {
         var userDefinedOntogeny = ontogeny.DowncastTo<UserDefinedOntogeny>();
         var pmas = userDefinedOntogeny.PostmenstrualAges();
         var ontogenyFactors = userDefinedOntogeny.OntogenyFactors();
         var deviations = userDefinedOntogeny.Deviations();

         return pmas.Select((v, i) => new OntogenyMetaData
         {
            PostmenstrualAge = v,
            OntogenyFactor = ontogenyFactors[i],
            Deviation = deviations[i],
            GroupName = Constants.Groups.UNDEFINED
         }).ToList();
      }

      public IReadOnlyList<OntogenyMetaData> AllValuesFor(Ontogeny ontogeny, string containerName)
      {
         var allValues = AllValuesFor(ontogeny);
         var allGroups = allValues.Select(x => x.GroupName).Distinct().ToList();

         var groupName = ontogenyGroupFor(containerName, allGroups);
         return allValues.Where(x => x.GroupName == groupName).ToList();
      }

      private static string ontogenyGroupFor(string containerName, List<string> allGroups)
      {
         if (allGroups.Count == 1)
            return allGroups[0];

         if (allGroups.Contains(containerName))
            return containerName;

         return ONTOGENY_LIVER;
      }
   }
}

﻿using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using OSPSuite.BDDHelper;
using OSPSuite.BDDHelper.Extensions;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Services;
using PKSim.Assets;
using PKSim.Core.Model;
using PKSim.Core.Snapshots;
using PKSim.Core.Snapshots.Mappers;
using IdentificationParameter = OSPSuite.Core.Domain.ParameterIdentifications.IdentificationParameter;
using ModelParameterIdentification = OSPSuite.Core.Domain.ParameterIdentifications.ParameterIdentification;
using OutputMapping = OSPSuite.Core.Domain.OutputMapping;
using Simulation = PKSim.Core.Model.Simulation;
using SnapshotParameterIdentification = PKSim.Core.Snapshots.ParameterIdentification;

namespace PKSim.Core
{
   public abstract class concern_for_ParameterIdentificationMapper : ContextSpecificationAsync<ParameterIdentificationMapper>
   {
      protected SnapshotParameterIdentification _snapshot;
      protected ModelParameterIdentification _parameterIdentification;
      protected PKSimProject _project;
      protected Simulation _simulation;
      protected ParameterIdentificationConfigurationMapper _parameterIdentificationConfigurationMapper;
      protected ParameterIdentificationConfiguration _snapshotParameterIdentificationConfiguration;
      protected OutputMappingMapper _outputMappingMapper;
      protected OutputMapping _outputMapping;
      protected Snapshots.OutputMapping _snapshotOutputMapping;
      protected IdentificationParameterMapper _identificationParameterMapper;
      protected IdentificationParameter _identificationParameter;
      protected Snapshots.IdentificationParameter _snapshotIdentificationParameter;
      protected ParameterIdentificationAnalysisMapper _parameterIdentificationAnalysisMapper;
      protected ISimulationAnalysis _parameterIdentificationAnalysis;
      protected ParameterIdentificationAnalysis _snapshotParameterIdentificationAnalysis;
      protected IObjectBaseFactory _objectBaseFactory;
      protected IOSPSuiteLogger _logger;
      protected SnapshotContext _snapshotContext;

      protected override Task Context()
      {
         _parameterIdentificationConfigurationMapper = A.Fake<ParameterIdentificationConfigurationMapper>();
         _outputMappingMapper = A.Fake<OutputMappingMapper>();
         _identificationParameterMapper = A.Fake<IdentificationParameterMapper>();
         _parameterIdentificationAnalysisMapper = A.Fake<ParameterIdentificationAnalysisMapper>();
         _objectBaseFactory = A.Fake<IObjectBaseFactory>();
         _logger = A.Fake<IOSPSuiteLogger>();

         _project = new PKSimProject();
         _snapshotContext = new SnapshotContext(_project, ProjectVersions.Current);
         _simulation = new IndividualSimulation().WithName("S1");
         _project.AddBuildingBlock(_simulation);

         _parameterIdentification = new ModelParameterIdentification();
         _snapshotParameterIdentificationConfiguration = new ParameterIdentificationConfiguration();
         _snapshotOutputMapping = new Snapshots.OutputMapping();
         _outputMapping = new OutputMapping();
         _parameterIdentification.AddSimulation(_simulation);
         _parameterIdentification.AddOutputMapping(_outputMapping);

         _identificationParameter = new IdentificationParameter {Name = "IP"};
         _parameterIdentification.AddIdentificationParameter(_identificationParameter);

         _snapshotIdentificationParameter = new Snapshots.IdentificationParameter();
         _snapshotParameterIdentificationAnalysis = new ParameterIdentificationAnalysis();
         _parameterIdentificationAnalysis = A.Fake<ISimulationAnalysis>();
         _parameterIdentification.AddAnalysis(_parameterIdentificationAnalysis);


         sut = new ParameterIdentificationMapper(
            _parameterIdentificationConfigurationMapper,
            _outputMappingMapper,
            _identificationParameterMapper,
            _parameterIdentificationAnalysisMapper,
            _objectBaseFactory,
            _logger
         );


         A.CallTo(() => _parameterIdentificationConfigurationMapper.MapToSnapshot(_parameterIdentification.Configuration)).Returns(_snapshotParameterIdentificationConfiguration);
         A.CallTo(() => _outputMappingMapper.MapToSnapshot(_outputMapping)).Returns(_snapshotOutputMapping);
         A.CallTo(() => _identificationParameterMapper.MapToSnapshot(_identificationParameter)).Returns(_snapshotIdentificationParameter);
         A.CallTo(() => _parameterIdentificationAnalysisMapper.MapToSnapshot(_parameterIdentificationAnalysis)).Returns(_snapshotParameterIdentificationAnalysis);

         return _completed;
      }
   }

   public class When_mapping_a_parameter_identification_to_snapshot : concern_for_ParameterIdentificationMapper
   {
      protected override async Task Because()
      {
         _snapshot = await sut.MapToSnapshot(_parameterIdentification);
      }

      [Observation]
      public void should_return_a_snapshot_referencing_the_optimized_simulations_from_project()
      {
         _snapshot.Simulations.ShouldContain(_simulation.Name);
      }

      [Observation]
      public void should_have_mapped_the_parameter_identification_configuration()
      {
         _snapshot.Configuration.ShouldBeEqualTo(_snapshotParameterIdentificationConfiguration);
      }

      [Observation]
      public void should_have_mapped_the_output_mappings()
      {
         _snapshot.OutputMappings.ShouldContain(_snapshotOutputMapping);
      }

      [Observation]
      public void should_have_mapped_the_identification_parameters()
      {
         _snapshot.IdentificationParameters.ShouldContain(_snapshotIdentificationParameter);
      }

      [Observation]
      public void should_have_mapped_the_parameter_identification_analysis()
      {
         _snapshot.Analyses.ShouldContain(_snapshotParameterIdentificationAnalysis);
      }
   }

   public class When_mapping_a_parameter_identification_snapshot_to_parameter_identification : concern_for_ParameterIdentificationMapper
   {
      private ModelParameterIdentification _newParameterIdentification;
      private SnapshotContext _context;

      protected override async Task Context()
      {
         await base.Context();
         _snapshot = await sut.MapToSnapshot(_parameterIdentification);
         A.CallTo(() => _outputMappingMapper.MapToModel(_snapshotOutputMapping, A<ParameterIdentificationContext>._)).Returns(_outputMapping);
         A.CallTo(() => _identificationParameterMapper.MapToModel(_snapshotIdentificationParameter, A<ParameterIdentificationContext>._)).Returns(_identificationParameter);
         A.CallTo(() => _parameterIdentificationAnalysisMapper.MapToModel(_snapshotParameterIdentificationAnalysis, A<ParameterIdentificationContext>._)).Returns(_parameterIdentificationAnalysis);

         A.CallTo(() => _objectBaseFactory.Create<ModelParameterIdentification>()).Returns(new ModelParameterIdentification());

         _context = new SnapshotContext(_project, 1);
      }

      protected override async Task Because()
      {
         _newParameterIdentification = await sut.MapToModel(_snapshot, _context);
      }

      [Observation]
      public void should_reference_the_used_simulation_defined_in_the_project()
      {
         _newParameterIdentification.AllSimulations.ShouldContain(_simulation);
      }

      [Observation]
      public void should_have_mapped_the_parameter_identification_configuration()
      {
         A.CallTo(() => _parameterIdentificationConfigurationMapper.MapToModel(_snapshot.Configuration, A<ParameterIdentificationContext>.That.Matches(x => x.ParameterIdentification == _newParameterIdentification))).MustHaveHappened();
      }

      [Observation]
      public void should_have_mapped_the_output_mappings()
      {
         _newParameterIdentification.OutputMappings.All.ShouldContain(_outputMapping);
      }

      [Observation]
      public void should_have_mapped_the_identification_parameters()
      {
         _newParameterIdentification.AllIdentificationParameters.ShouldContain(_identificationParameter);
      }

      [Observation]
      public void should_have_mapped_the_parameter_identification_analysis()
      {
         _newParameterIdentification.Analyses.ShouldContain(_parameterIdentificationAnalysis);
      }
   }

   public class When_mapping_a_parameter_identification_snapshot_referencing_an_unknown_simulation_to_parameter_identification : concern_for_ParameterIdentificationMapper
   {
      private string _unknownSimulation;

      protected override async Task Context()
      {
         await base.Context();
         _snapshot = await sut.MapToSnapshot(_parameterIdentification);
         _unknownSimulation = "UNKOWN";
         _snapshot.Simulations = new[] {_unknownSimulation};
      }

      protected override Task Because()
      {
         return sut.MapToModel(_snapshot, _snapshotContext);
      }

      [Observation]
      public void should_log_the_fact_that_a_simulation_is_unknown()
      {
         A.CallTo(() => _logger.AddToLog(PKSimConstants.Error.CouldNotFindSimulation(_unknownSimulation), LogLevel.Warning, A<string>._)).MustHaveHappened();
      }
   }
}
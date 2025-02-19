using OSPSuite.Core;
using OSPSuite.Core.Commands;
using OSPSuite.Core.Comparison;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Mappers;
using OSPSuite.Core.Domain.Services;
using OSPSuite.Core.Domain.Services.ParameterIdentifications;
using OSPSuite.Core.Domain.UnitSystem;
using OSPSuite.Core.Maths.Interpolations;
using OSPSuite.Utility.Container;
using OSPSuite.Utility.Data;
using OSPSuite.Infrastructure.Import.Services;
using PKSim.Core.Comparison;
using PKSim.Core.Mappers;
using PKSim.Core.Model;
using PKSim.Core.Services;
using PKSim.Core.Snapshots.Mappers;
using IContainer = OSPSuite.Utility.Container.IContainer;

namespace PKSim.Core
{
   public class CoreRegister : Register
   {
      public override void RegisterInContainer(IContainer container)
      {
         container.AddRegister(x => x.FromInstance(new OSPSuite.Core.CoreRegister {RegisterParameter = false}));

         //Register PKSim.Core 
         container.AddScanner(scan =>
         {
            scan.AssemblyContainingType<CoreRegister>();

            //Exclude type that should be register as singleton because of caching 
            scan.ExcludeType<FormulationValuesRetriever>();
            scan.ExcludeType<ObjectTypeResolver>();
            scan.ExcludeType<PKSimDimensionFactory>();
            scan.ExcludeType<PKSimObjectBaseFactory>();
            scan.ExcludeType<DistributionFormulaFactory>();
            scan.ExcludeType<ApplicationSettings>();
            scan.ExcludeType<ProjectChangedNotifier>();
            scan.ExcludeType<IndividualSimulationEngine>();
            scan.ExcludeType<DefaultIndividualRetriever>();
            scan.ExcludeType<IPopulationSimulationEngine>();
            
            //Do not register the InteractiveSimulationRunner as it should be registered only if needed
            scan.ExcludeType<InteractiveSimulationRunner>();
            scan.ExcludeType<SnapshotMapper>();

            scan.ExcludeNamespaceContainingType<IndividualDiffBuilder>();
            scan.WithConvention<PKSimRegistrationConvention>();
         });

         //Register singletons explicitly
         container.AddScanner(scan =>
         {
            scan.AssemblyContainingType<CoreRegister>();
            scan.IncludeType<FormulationValuesRetriever>();
            scan.IncludeType<ObjectTypeResolver>();
            scan.IncludeType<DistributionFormulaFactory>();
            scan.IncludeType<ProjectChangedNotifier>();
            scan.IncludeType<SnapshotMapper>();
            scan.IncludeType<DefaultIndividualRetriever>();

            scan.RegisterAs(LifeStyle.Singleton);
            scan.WithConvention<PKSimRegistrationConvention>();
         });
         
         container.Register<ICoreSimulationFactory, SimulationFactory>();
         container.Register<ISetParameterTask,  ParameterTask>(LifeStyle.Transient);
         container.Register<ITransferOptimizedParametersToSimulationsTask, TransferOptimizedParametersToSimulationsTask<IExecutionContext>>();
         container.Register<IIndividualSimulationEngine, ISimulationEngine<IndividualSimulation, SimulationRunResults>, IndividualSimulationEngine>(LifeStyle.Transient);
         container.Register<IPopulationSimulationEngine, ISimulationEngine<PopulationSimulation, PopulationRunResults>, PopulationSimulationEngine>(LifeStyle.Transient);

         //other singleton external to application
         container.Register<ICloneManager, Cloner>(LifeStyle.Singleton);

         //Register special type for parameters so that core methods in the context of pksim creates a PKSimParameter
         container.Register<IParameter, PKSimParameter>();
         container.Register<IDistributedParameter, PKSimDistributedParameter>();

         //specific PKSim Implementations
         container.Register<IPathToPathElementsMapper, PKSimPathToPathElementsMapper>();
         container.Register<IDataColumnToPathElementsMapper, PKSimDataColumnToPathElementsMapper>();
         container.Register<IQuantityPathToQuantityDisplayPathMapper, PKSimQuantityPathToQuantityDisplayPathMapper>();

         //Register Factories
         container.RegisterFactory<ISimulationEngineFactory>();
         container.RegisterFactory<IChartDataToTableMapperFactory>();

         container.Register<IPKSimObjectBaseFactory, IObjectBaseFactory, PKSimObjectBaseFactory>(LifeStyle.Singleton);
         container.Register<IPKSimDimensionFactory, IDimensionFactory, PKSimDimensionFactory>(LifeStyle.Singleton);
         container.Register<IApplicationSettings, OSPSuite.Core.IApplicationSettings, ApplicationSettings>(LifeStyle.Singleton);

         //Register opened types generics
         container.Register(typeof(IMoleculeExpressionTask<>), typeof(MoleculeExpressionTask<>));

         container.Register<IInterpolation, LinearInterpolation>();
         container.Register<IPivoter, Pivoter>();
         container.Register<ISimulationSubject, Individual>();
         container.Register<Protocol, SimpleProtocol>();
         container.Register<Simulation, IndividualSimulation>();
         container.Register<Population, RandomPopulation>();
         container.Register<SchemaItem, SchemaItem>();

         //generic command registration
         container.Register<IOSPSuiteExecutionContext, ExecutionContext>();

         registerMoleculeFactories(container);
         registerComparers(container);
      }

      private void registerMoleculeFactories(IContainer container)
      {
         container.Register<IIndividualMoleculeFactory, IndividualEnzymeFactory>();
         container.Register<IIndividualMoleculeFactory, IndividualTransporterFactory>();
         container.Register<IIndividualMoleculeFactory, IndividualOtherProteinFactory>();
      }

      private static void registerComparers(IContainer container)
      {
         container.AddScanner(scan =>
         {
            scan.AssemblyContainingType<CoreRegister>();
            scan.IncludeNamespaceContainingType<IndividualDiffBuilder>();
            scan.WithConvention<RegisterTypeConvention<IDiffBuilder>>();
         });

         ParameterDiffBuilder.ShouldCompareParametersIn = PKSimParameterDiffBuilder.ShouldCompareParametersIn;
      }
   }
}
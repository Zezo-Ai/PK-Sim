using System.Collections.Generic;
using System.Linq;
using OSPSuite.Assets;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Builder;
using OSPSuite.Core.Domain.Descriptors;
using OSPSuite.Core.Domain.Services;
using OSPSuite.Utility.Extensions;
using PKSim.Core.Mappers;
using PKSim.Core.Model;
using PKSim.Core.Repositories;

namespace PKSim.Core.Services
{
   public interface IEventBuildingBlockCreator
   {
      /// <summary>
      ///    return the event building block built based on the given protocol and the associated formulation.
      ///    Special simulation event such as eat,sport etc.. should be managed in this class as well
      /// </summary>
      EventGroupBuildingBlock CreateFor(Simulation simulation);
   }

   public class EventBuildingBlockCreator : IEventBuildingBlockCreator
   {
      private readonly IObjectBaseFactory _objectBaseFactory;
      private readonly IProtocolToSchemaItemsMapper _schemaItemsMapper;
      private readonly IApplicationFactory _applicationFactory;
      private readonly IFormulationFromMappingRetriever _formulationFromMappingRetriever;
      private readonly ICloneManagerForBuildingBlock _cloneManagerForBuildingBlock;
      private readonly IParameterIdUpdater _parameterIdUpdater;
      private readonly IParameterSetUpdater _parameterSetUpdater;
      private readonly IEventGroupRepository _eventGroupRepository;
      private Simulation _simulation;
      private EventGroupBuildingBlock _eventGroupBuildingBlock;
      private readonly IParameterDefaultStateUpdater _parameterDefaultStateUpdater;

      public EventBuildingBlockCreator(IObjectBaseFactory objectBaseFactory,
         IProtocolToSchemaItemsMapper schemaItemsMapper,
         IApplicationFactory applicationFactory,
         IFormulationFromMappingRetriever formulationFromMappingRetriever,
         ICloneManagerForBuildingBlock cloneManagerForBuildingBlock,
         IParameterIdUpdater parameterIdUpdater,
         IParameterSetUpdater parameterSetUpdater,
         IEventGroupRepository eventGroupRepository,
         IParameterDefaultStateUpdater parameterDefaultStateUpdater)
      {
         _objectBaseFactory = objectBaseFactory;
         _schemaItemsMapper = schemaItemsMapper;
         _applicationFactory = applicationFactory;
         _formulationFromMappingRetriever = formulationFromMappingRetriever;
         _cloneManagerForBuildingBlock = cloneManagerForBuildingBlock;
         _parameterIdUpdater = parameterIdUpdater;
         _parameterSetUpdater = parameterSetUpdater;
         _eventGroupRepository = eventGroupRepository;
         _parameterDefaultStateUpdater = parameterDefaultStateUpdater;
      }

      public EventGroupBuildingBlock CreateFor(Simulation simulation)
      {
         try
         {
            _simulation = simulation;
            _eventGroupBuildingBlock = _objectBaseFactory.Create<EventGroupBuildingBlock>().WithName(DefaultNames.EventBuildingBlock);
            _cloneManagerForBuildingBlock.FormulaCache = _eventGroupBuildingBlock.FormulaCache;

            createApplications(_simulation.CompoundPropertiesList);

            createNonApplicationEvents();

            _parameterDefaultStateUpdater.UpdateDefaultFor(_eventGroupBuildingBlock);

            return _eventGroupBuildingBlock;
         }
         finally
         {
            _simulation = null;
            _eventGroupBuildingBlock = null;
         }
      }

      private void createNonApplicationEvents()
      {
         // group events by the event-building block they are using
         var eventBuildingBlockInfos = (from eventMapping in _simulation.EventProperties.EventMappings
               let usedBuildingBlock = _simulation.UsedBuildingBlockByTemplateId(eventMapping.TemplateEventId)
               let eventBuildingBlock = usedBuildingBlock.BuildingBlock.DowncastTo<PKSimEvent>()
               select new { eventBuildingBlock.Id, eventBuildingBlock.TemplateName, eventBuildingBlock.Name })
            .Distinct();

         // create event groups for each used event-building block
         foreach (var eventBuildingBlockInfo in eventBuildingBlockInfos)
         {
            // get event group template
            var templateEventGroup = _eventGroupRepository.FindByName(eventBuildingBlockInfo.TemplateName);

            // create new event group
            var eventGroup = _cloneManagerForBuildingBlock.Clone(templateEventGroup);
            eventGroup.Name = eventBuildingBlockInfo.Name;
            eventGroup.RemoveChild(eventGroup.MainSubContainer());

            // get building block and eventgroup-template to be used
            var eventBuildingBlock = _simulation.UsedBuildingBlockById(eventBuildingBlockInfo.Id);
            var eventTemplate = eventBuildingBlock.BuildingBlock.DowncastTo<PKSimEvent>();

            // set event group parameter
            _parameterSetUpdater.UpdateValuesByName(eventTemplate, eventGroup);

            // create subcontainers (event groups) for all events of the same type
            int eventIndex = 0; //used for naming of event subcontainers only

            foreach (var eventMapping in _simulation.EventProperties.EventMappings.OrderBy(em => em.StartTime.Value))
            {
               if (!eventMapping.TemplateEventId.Equals(eventBuildingBlock.TemplateId))
                  continue; //event from different template

               // clone main event subcontainer and set its start time
               var mainSubContainer = _cloneManagerForBuildingBlock.Clone(templateEventGroup.MainSubContainer());

               eventIndex += 1;
               mainSubContainer.Name = $"{eventBuildingBlockInfo.Name}_{eventIndex}";

               _parameterSetUpdater.UpdateValue(eventMapping.StartTime, mainSubContainer.StartTime());

               eventGroup.Add(mainSubContainer);
            }

            // update building block ids
            _parameterIdUpdater.UpdateBuildingBlockId(eventGroup, eventTemplate);

            _eventGroupBuildingBlock.Add(eventGroup);
         }
      }

      private void createApplications(IReadOnlyList<CompoundProperties> compoundPropertiesList)
      {
         compoundPropertiesList.Each(addProtocol);
      }

      private void addProtocol(CompoundProperties compoundProperties)
      {
         var protocol = compoundProperties.ProtocolProperties.Protocol;
         if (protocol == null)
            return;

         var eventGroup = _objectBaseFactory.Create<EventGroupBuilder>()
            .WithName(protocol.Name);

         eventGroup.SourceCriteria.Add(new MatchTagCondition(CoreConstants.Tags.EVENTS));

         _schemaItemsMapper.MapFrom(protocol).Each((schemaItem, index) =>
         {
            //+1 to start at 1 for the nomenclature
            var applicationName = $"{CoreConstants.APPLICATION_NAME_TEMPLATE}{index + 1}";
            addApplication(eventGroup, schemaItem, applicationName, compoundProperties, protocol);
         });

         _parameterIdUpdater.UpdateBuildingBlockId(eventGroup, protocol);

         _eventGroupBuildingBlock.Add(eventGroup);
      }

      private void addApplication(EventGroupBuilder protocolGroupBuilder, ISchemaItem schemaItem, string applicationName, CompoundProperties compoundProperties, Protocol protocol)
      {
         IContainer applicationParentContainer;
         string formulationType;

         IEnumerable<IParameter> formulationParameters;

         if (schemaItem.NeedsFormulation)
         {
            var formulation = _formulationFromMappingRetriever.FormulationUsedBy(_simulation, compoundProperties.ProtocolProperties.MappingWith(schemaItem.FormulationKey));
            if (formulation == null)
               throw new NoFormulationFoundForRouteException(protocol, schemaItem.ApplicationType);

            //check if used formulation container is already created and create if needed
            if (protocolGroupBuilder.ContainsName(formulation.Name))
               applicationParentContainer = protocolGroupBuilder.GetSingleChildByName<EventGroupBuilder>(formulation.Name);
            else
               applicationParentContainer = createFormulationAsEventGroupBuilderFrom(formulation);

            protocolGroupBuilder.Add(applicationParentContainer);
            formulationType = formulation.FormulationType;
            formulationParameters = applicationParentContainer.GetChildren<IParameter>();
         }
         else
         {
            applicationParentContainer = protocolGroupBuilder;
            formulationType = CoreConstants.Formulation.EMPTY_FORMULATION;
            formulationParameters = new List<IParameter>();
         }

         applicationParentContainer.Add(
            _applicationFactory.CreateFor(schemaItem, formulationType, applicationName, compoundProperties.Compound.Name, formulationParameters, _eventGroupBuildingBlock.FormulaCache));
      }

      private EventGroupBuilder createFormulationAsEventGroupBuilderFrom(Formulation formulation)
      {
         var formulationBuilder = _objectBaseFactory.Create<EventGroupBuilder>();

         formulationBuilder.UpdatePropertiesFrom(formulation, _cloneManagerForBuildingBlock);
         foreach (var parameter in formulation.AllParameters())
         {
            var builderParameter = _cloneManagerForBuildingBlock.Clone(parameter);
            //reset the origin in any case since it might have been set by clone
            builderParameter.Origin.ParameterId = string.Empty;
            formulationBuilder.Add(builderParameter);
         }

         _parameterIdUpdater.UpdateBuildingBlockId(formulationBuilder, formulation);
         _parameterIdUpdater.UpdateParameterIds(formulation, formulationBuilder);

         if (formulation.FormulationType.Equals(CoreConstants.Formulation.PARTICLES))
            setParticleRadiusDistributionParametersToLockedAndInvisible(formulationBuilder);

         return formulationBuilder;
      }

      private static void setParticleRadiusDistributionParametersToLockedAndInvisible(IContainer formulationBuilder)
      {
         // first, set all parameters responsible for particle size distribution to locked
         CoreConstants.Parameters.ParticleDistributionStructuralParameters.Each(paramName => formulationBuilder.Parameter(paramName).Editable = false);

         // second, set some parameters to not visible depending on settings
         var parameterNamesToBeInvisible = new List<string> { Constants.Parameters.PARTICLE_DISPERSE_SYSTEM };

         var numberOfBins = (int)formulationBuilder.Parameter(Constants.Parameters.NUMBER_OF_BINS).Value;

         if (numberOfBins == 1)
            parameterNamesToBeInvisible.AddRange(CoreConstants.Parameters.HiddenParameterForMonodisperse);
         else
         {
            var particlesDistributionType = (int)formulationBuilder.Parameter(Constants.Parameters.PARTICLE_SIZE_DISTRIBUTION).Value;

            if (particlesDistributionType == CoreConstants.Parameters.PARTICLE_SIZE_DISTRIBUTION_NORMAL)
               parameterNamesToBeInvisible.AddRange(CoreConstants.Parameters.HiddenParameterForPolydisperseNormal);
            else
               parameterNamesToBeInvisible.AddRange(CoreConstants.Parameters.HiddenParameterForPolydisperseLogNormal);
         }

         parameterNamesToBeInvisible.Each(paramName => formulationBuilder.Parameter(paramName).Visible = false);
      }
   }
}
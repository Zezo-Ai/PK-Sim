using System.Collections.Generic;
using System.Linq;
using OSPSuite.Assets;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Data;
using OSPSuite.Core.Domain.Services;
using OSPSuite.Core.Events;
using OSPSuite.Presentation.Core;
using OSPSuite.Presentation.Extensions;
using OSPSuite.Presentation.MenuAndBars;
using OSPSuite.Presentation.Nodes;
using OSPSuite.Presentation.Presenters;
using OSPSuite.Presentation.Presenters.Classifications;
using OSPSuite.Presentation.Presenters.ContextMenus;
using OSPSuite.Presentation.Presenters.Nodes;
using OSPSuite.Presentation.Regions;
using OSPSuite.Presentation.Repositories;
using OSPSuite.Presentation.Services;
using OSPSuite.Presentation.Views;
using OSPSuite.Utility.Events;
using OSPSuite.Utility.Extensions;
using PKSim.Core;
using PKSim.Core.Events;
using PKSim.Core.Model;
using PKSim.Core.Services;
using PKSim.Presentation.Nodes;
using PKSim.Presentation.Presenters.ContextMenus;
using PKSim.Presentation.Regions;
using PKSim.Presentation.Services;
using PKSim.Presentation.Views.Main;
using static PKSim.Assets.PKSimConstants;
using ITreeNodeFactory = PKSim.Presentation.Nodes.ITreeNodeFactory;

namespace PKSim.Presentation.Presenters.Main
{
   public interface ISimulationExplorerPresenter : IExplorerPresenter,
      IListener<SimulationStatusChangedEvent>,
      IListener<SimulationComparisonCreatedEvent>,
      IListener<SimulationComparisonDeletedEvent>,
      IListener<RenamedEvent>,
      IListener<SwapBuildingBlockEvent>

   {
   }

   public class SimulationExplorerPresenter : ExplorerPresenter<ISimulationExplorerView, ISimulationExplorerPresenter>, ISimulationExplorerPresenter
   {
      private readonly IBuildingBlockInProjectManager _buildingBlockInProjectManager;
      private readonly IParameterAnalysablesInExplorerPresenter _parameterAnalysablesInExplorerPresenter;
      private readonly IObservedDataInSimulationManager _observedDataInSimulationManager;
      private readonly ISimulationComparisonTask _simulationComparisonTask;
      private readonly IExecutionContext _executionContext;
      private readonly IMenuBarItemRepository _menuBarItemRepository;

      public SimulationExplorerPresenter(
         ISimulationExplorerView view,
         ITreeNodeFactory treeNodeFactory,
         ITreeNodeContextMenuFactory treeNodeContextMenuFactory,
         IMultipleTreeNodeContextMenuFactory multipleTreeNodeContextMenuFactory,
         IBuildingBlockIconRetriever buildingBlockIconRetriever,
         IRegionResolver regionResolver,
         IBuildingBlockTask buildingBlockTask,
         IBuildingBlockInProjectManager buildingBlockInProjectManager,
         IToolTipPartCreator toolTipPartCreator,
         IProjectRetriever projectRetriever,
         IClassificationPresenter classificationPresenter,
         IParameterAnalysablesInExplorerPresenter parameterAnalysablesInExplorerPresenter,
         IObservedDataInSimulationManager observedDataInSimulationManager,
         ISimulationComparisonTask simulationComparisonTask,
         IExecutionContext executionContext,
         IMenuBarItemRepository menuBarItemRepository) :
         base(view, treeNodeFactory, treeNodeContextMenuFactory, multipleTreeNodeContextMenuFactory, buildingBlockIconRetriever, regionResolver,
            buildingBlockTask, RegionNames.SimulationExplorer, projectRetriever, classificationPresenter, toolTipPartCreator)
      {
         _buildingBlockInProjectManager = buildingBlockInProjectManager;
         _parameterAnalysablesInExplorerPresenter = parameterAnalysablesInExplorerPresenter;
         _observedDataInSimulationManager = observedDataInSimulationManager;
         _simulationComparisonTask = simulationComparisonTask;
         _executionContext = executionContext;
         _menuBarItemRepository = menuBarItemRepository;
         _parameterAnalysablesInExplorerPresenter.InitializeWith(this, classificationPresenter);
      }

      public override bool CopyAllowed() => false;

      public override bool CanDrag(ITreeNode node)
      {
         if (node == null)
            return false;

         return node.IsAnImplementationOf<SimulationNode>() ||
                node.IsAnImplementationOf<ComparisonNode>() ||
                node.IsAnImplementationOf<ClassificationNode>() ||
                _parameterAnalysablesInExplorerPresenter.CanDrag(node);
      }

      public override bool RemoveDataUnderClassification(ITreeNode<IClassification> classificationNode)
      {
         if (classificationNode.Tag.ClassificationType == ClassificationType.Simulation)
         {
            var allSimulations = classificationNode.AllNodes<SimulationNode>().Select(x => x.Tag.Simulation).ToList();
            return _buildingBlockTask.Delete(allSimulations);
         }

         if (classificationNode.Tag.ClassificationType == ClassificationType.Comparison)
         {
            var allComparisons = classificationNode.AllNodes<ComparisonNode>().Select(x => x.Tag.Comparison).ToList();
            return _simulationComparisonTask.Delete(allComparisons);
         }


         return _parameterAnalysablesInExplorerPresenter.RemoveDataUnderClassification(classificationNode);
      }

      protected override void AddProjectToTree(IProject project)
      {
         using (new BatchUpdate(_view))
         {
            _view.DestroyNodes();

            //root nodes
            _view.AddNode(_treeNodeFactory.CreateFor(RootNodeTypes.SimulationFolder));
            _view.AddNode(_treeNodeFactory.CreateFor(RootNodeTypes.ComparisonFolder));
            _view.AddNode(_treeNodeFactory.CreateFor(RootNodeTypes.ParameterIdentificationFolder));
            _view.AddNode(_treeNodeFactory.CreateFor(RootNodeTypes.SensitivityAnalysisFolder));

            //classifications
            _classificationPresenter.AddClassificationsToTree(project.AllClassificationsByType(ClassificationType.Simulation));
            _classificationPresenter.AddClassificationsToTree(project.AllClassificationsByType(ClassificationType.Comparison));
            _classificationPresenter.AddClassificationsToTree(project.AllClassificationsByType(ClassificationType.QualificationPlan));

            project.AllClassifiablesByType<ClassifiableSimulation>().Each(x => addClassifiableSimulationToFolder(x));
            project.AllClassifiablesByType<ClassifiableComparison>().Each(x => addClassifiableComparisonToFolder(x));

            _parameterAnalysablesInExplorerPresenter.AddParameterAnalysablesToTree(project);
         }
      }

      protected override ITreeNode AddBuildingBlockToTree(IPKSimBuildingBlock buildingBlock)
      {
         if (buildingBlock.BuildingBlockType != PKSimBuildingBlockType.Simulation)
            return null;

         return addSimulationToTree(buildingBlock.DowncastTo<Simulation>());
      }

      private ITreeNode addSimulationToTree(Simulation simulation)
      {
         return AddSubjectToClassifyToTree<Simulation, ClassifiableSimulation>(simulation, addClassifiableSimulationToFolder);
      }

      private ITreeNode addSimulationComparisonToTree(ISimulationComparison simulationComparison) =>
         AddSubjectToClassifyToTree<ISimulationComparison, ClassifiableComparison>(simulationComparison, addClassifiableComparisonToFolder);

      private ITreeNode addClassifiableSimulationToFolder(ClassifiableSimulation classifiableSimulation) =>
         AddClassifiableToTree(classifiableSimulation, RootNodeTypes.SimulationFolder, addClassifiableSimulationToTree);

      private ITreeNode addClassifiableComparisonToFolder(ClassifiableComparison classifiableComparison) =>
         AddClassifiableToTree(classifiableComparison, RootNodeTypes.ComparisonFolder, addClassifiableComparisonToTree);

      private ITreeNode addClassifiableComparisonToTree(ITreeNode<IClassification> classificationNode, ClassifiableComparison classifiableComparison)
      {
         var simulationComparisonNode = _treeNodeFactory.CreateFor(classifiableComparison)
            .WithIcon(_buildingBlockIconRetriever.IconFor(classifiableComparison.Comparison));

         AddClassifiableNodeToView(simulationComparisonNode, classificationNode);
         return simulationComparisonNode;
      }

      private ITreeNode addClassifiableSimulationToTree(ITreeNode<IClassification> classificationNode, ClassifiableSimulation classifiableSimulation)
      {
         var simulation = classifiableSimulation.Simulation;
         var simulationNode = _treeNodeFactory.CreateFor(classifiableSimulation)
            .WithIcon(_buildingBlockIconRetriever.IconFor(simulation));

         addUsedBuildingBlockNodes(simulation, simulationNode);


         AddClassifiableNodeToView(simulationNode, classificationNode);

         updateObservedDataFor(simulationNode, simulation);

         return simulationNode;
      }

      private void addUsedBuildingBlockNodes(Simulation simulation, ITreeNode simulationNode)
      {
         if (simulation.IsImported) return;

         _treeNodeFactory.CreateFor(simulation.ModelProperties)
            .WithIcon(ApplicationIcons.ModelStructure)
            .Under(simulationNode);

         addUsedBuildingBlock(simulation, simulationNode, PKSimBuildingBlockType.SimulationSubject);
         //Used for debug purposes for now   addUsedBuildingBlock(simulation, simulationNode, PKSimBuildingBlockType.ExpressionProfile);
         addUsedBuildingBlock(simulation, simulationNode, PKSimBuildingBlockType.Compound);
         addUsedBuildingBlock(simulation, simulationNode, PKSimBuildingBlockType.Protocol);
         addUsedBuildingBlock(simulation, simulationNode, PKSimBuildingBlockType.Formulation);
         addUsedBuildingBlock(simulation, simulationNode, PKSimBuildingBlockType.Event);
         addUsedBuildingBlock(simulation, simulationNode, PKSimBuildingBlockType.ObserverSet);
      }

      private void addUsedBuildingBlock(Simulation simulation, ITreeNode simulationNode, PKSimBuildingBlockType buildingBlockType)
      {
         foreach (var usedBuildingBlock in simulation.UsedBuildingBlocksInSimulation(buildingBlockType))
         {
            _treeNodeFactory.CreateFor(simulation, usedBuildingBlock)
               .WithIcon(_buildingBlockIconRetriever.IconFor(usedBuildingBlock))
               .WithText(usedBuildingBlock.Name)
               .Under(simulationNode);
         }
      }

      private void updateObservedDataFor(ITreeNode simulationNode, Simulation simulation)
      {
         var project = _projectRetriever.CurrentProject;
         //remove all available observed data node
         var observedDataNode = simulationNode.Children.Where(n => n.IsAnImplementationOf<UsedObservedDataNode>()).ToList();
         observedDataNode.Each(_view.DestroyNode);

         foreach (var usedObservedData in simulation.UsedObservedData)
         {
            if (project.ObservedDataBy(usedObservedData.Id) == null)
               continue;

            _view.AddNode(_treeNodeFactory.CreateFor(usedObservedData)
               .Under(simulationNode));
         }
      }

      public void Handle(SimulationStatusChangedEvent eventToHandle)
      {
         updateSimulationNode(eventToHandle.Simulation as Simulation);
      }

      public void Handle(SimulationComparisonCreatedEvent eventToHandle)
      {
         var node = addSimulationComparisonToTree(eventToHandle.SimulationComparison);
         EnsureNodeVisible(node);
      }

      public void Handle(SimulationComparisonDeletedEvent eventToHandle)
      {
         RemoveNodeFor(eventToHandle.Chart);
      }

      public void Handle(RenamedEvent renamedEvent)
      {
         handleRenamedBuildingBlock(renamedEvent);
         handleRenamedObservedData(renamedEvent);
      }

      private void handleRenamedObservedData(RenamedEvent renamedEvent)
      {
         var observedData = renamedEvent.RenamedObject as DataRepository;
         if (observedData == null)
            return;

         _observedDataInSimulationManager.SimulationsUsing(observedData).Each(updateObservedDataForSimulation);
         RefreshTreeAfterRename();
      }

      private void updateObservedDataForSimulation(Simulation simulationUsingObservedData)
      {
         updateObservedDataFor(NodeFor(simulationUsingObservedData), simulationUsingObservedData);
      }

      private void handleRenamedBuildingBlock(RenamedEvent renamedEvent)
      {
         var buildingBlock = renamedEvent.RenamedObject as IPKSimBuildingBlock;
         if (buildingBlock == null)
            return;

         if (buildingBlock.IsAnImplementationOf<Simulation>())
            updateSimulationNode(buildingBlock.DowncastTo<Simulation>());
         else
            _buildingBlockInProjectManager.SimulationsUsing(buildingBlock).Each(updateSimulationNode);

         RefreshTreeAfterRename();
      }

      private void updateSimulationNode(Simulation simulation)
      {
         var simulationNode = NodeFor(simulation);
         if (simulationNode == null) return;

         simulationNode.WithIcon(_buildingBlockIconRetriever.IconFor(simulation));

         //Update building blocks icons
         foreach (var usedBuildingBlock in simulation.UsedBuildingBlocks)
         {
            var usedBuildingBlockNode = _view.NodeById(usedBuildingBlock.Id);
            if (usedBuildingBlockNode == null)
               continue;

            usedBuildingBlockNode.WithIcon(_buildingBlockIconRetriever.IconFor(usedBuildingBlock));
            usedBuildingBlockNode.WithText(usedBuildingBlock.Name);
            usedBuildingBlockNode.ToolTip = _toolTipPartCreator.ToolTipFor(usedBuildingBlock);
         }

         //update observed data
         updateObservedDataFor(simulationNode, simulation);
      }

      public override void NodeDoubleClicked(ITreeNode node)
      {
         var tag = node.TagAsObject;
         var classifiable = tag as ClassifiableSimulation;
         if (classifiable != null)
         {
            EditBuildingBlock(classifiable.Simulation);
            return;
         }

         if (shouldIgnoreDoubleClick(tag))
            return;

         base.NodeDoubleClicked(node);
      }

      private static bool shouldIgnoreDoubleClick(object tag)
      {
         return tag.IsAnImplementationOf<UsedBuildingBlock>() || tag.IsAnImplementationOf<UsedObservedData>();
      }

      public override IEnumerable<IMenuBarItem> AllCustomMenuItemsFor(ClassificationNode classificationNode)
      {
         //this method ic called when a right click is made on a custom subfolder. 
         //we save this subfolder so that we can potentially add the newly created items to it later
         var activeClassification = classificationNode.Tag;
         SetActiveClassification(activeClassification);
         var allCustomMenuItems = new List<IMenuBarItem>();
         switch (activeClassification.ClassificationType)
         {
            case ClassificationType.Comparison:
               allCustomMenuItems.AddRange(ComparisonFolderTreeNodeContextMenu.AddComparisonMenuItems(_executionContext.Container));
               break;
            case ClassificationType.Simulation:
               allCustomMenuItems.AddRange(SimulationFolderTreeNodeContextMenu.AddSimulationMenuItems(_menuBarItemRepository));
               break;
            case ClassificationType.ParameterIdentification:
               allCustomMenuItems.Add(ParameterIdentificationContextMenuItems.CreateParameterIdentification(_executionContext.Container));
               break;
            case ClassificationType.SensitiviyAnalysis:
               allCustomMenuItems.Add(SensitivityAnalysisContextMenuItems.CreateSensitivityAnalysis(_executionContext.Container));
               break;
         }

         return allCustomMenuItems;
      }

      public override IEnumerable<ClassificationTemplate> AvailableClassificationCategories(ITreeNode<IClassification> parentClassificationNode)
      {
         var classification = parentClassificationNode.Tag;
         if (classification.ClassificationType != ClassificationType.Simulation)
            return Enumerable.Empty<ClassificationTemplate>();

         return new []
         {
            new ClassificationTemplate(Classifications.Compound, ApplicationIcons.Compound),
            new ClassificationTemplate(Classifications.AdministrationProtocol, ApplicationIcons.Protocol),
            new ClassificationTemplate(Classifications.Individual, ApplicationIcons.Individual),
            new ClassificationTemplate(Classifications.Population, ApplicationIcons.Population),
            new ClassificationTemplate(Classifications.SimulationType, ApplicationIcons.Simulation)
         };
      }

      public override void AddToClassificationTree(ITreeNode<IClassification> parentNode, string category)
      {
         _classificationPresenter.GroupClassificationsByCategory<ClassifiableSimulation>(parentNode,
            category, s => retrieveCategoryValue(s, category));
      }

      private string retrieveCategoryValue(ClassifiableSimulation classifiableSimulation, string category)
      {
         var simulation = classifiableSimulation.Simulation;
         if (string.Equals(Classifications.Compound, category))
            return simulation.BuildingBlockName(PKSimBuildingBlockType.Compound);

         if (string.Equals(Classifications.AdministrationProtocol, category))
            return simulation.BuildingBlockName(PKSimBuildingBlockType.Protocol);

         if (string.Equals(Classifications.Individual, category))
            return simulation.BuildingBlockName(PKSimBuildingBlockType.Individual);

         if (string.Equals(Classifications.Population, category))
            return simulation.BuildingBlockName(PKSimBuildingBlockType.Population);

         if (string.Equals(Classifications.SimulationType, category))
            return displayTypeFor(simulation);

         return string.Empty;
      }

      private string displayTypeFor(Simulation simulation)
      {
         return simulation.IsAnImplementationOf<IndividualSimulation>()
            ? UI.IndividualSimulation
            : UI.PopulationSimulation;
      }

      public void Handle(SwapBuildingBlockEvent eventToHandle)
      {
         //we need to swap all template building blocks stored in the used building block nodes
         var simulationRootNode = _view.NodeByType(RootNodeTypes.SimulationFolder);
         var allNodesToUpdate = simulationRootNode.AllLeafNodes.OfType<UsedBuildingBlockInSimulationNode>()
            .Where(n => Equals(n.TemplateBuildingBlock, eventToHandle.OldBuildingBlock))
            .ToList();

         allNodesToUpdate.Each(x => x.TemplateBuildingBlock = eventToHandle.NewBuildingBlock);
      }
   }
}
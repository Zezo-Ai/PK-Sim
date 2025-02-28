using System.Collections.Generic;
using System.Linq;
using OSPSuite.Assets;
using OSPSuite.Presentation.Core;
using OSPSuite.Presentation.MenuAndBars;
using OSPSuite.Presentation.Nodes;
using OSPSuite.Presentation.Presenters;
using OSPSuite.Presentation.Presenters.ContextMenus;
using OSPSuite.Utility.Container;
using PKSim.Assets;
using PKSim.Core.Model;
using PKSim.Presentation.UICommands;

namespace PKSim.Presentation.Presenters.ContextMenus
{
   public class IndividualContextMenu : BuildingBlockContextMenu<Individual>
   {
      public IndividualContextMenu(Individual individual, IContainer container) : base(individual, container)
      {
      }

      protected override IEnumerable<IMenuBarItem> ExportContextMenusFor(Individual individual)
      {
         return base.ExportContextMenusFor(individual).Concat(new[]
         {
            exportToPkml(individual)
         });
      }

      protected override IEnumerable<IMenuBarItem> AllMenuItemsFor(Individual individual)
      {
         var allMenuItems = new List<IMenuBarItem>();
         allMenuItems.AddRange(EditContextMenusFor<EditIndividualCommand>(individual));
         allMenuItems.Add(scaleIndividualMenuFor(individual));
         allMenuItems.Add(newPopulationMenuFor(individual));
         allMenuItems.AddRange(ExportAndDeleteContextMenusFor(individual));

         return allMenuItems;
      }
      
      private IMenuBarItem exportToPkml(Individual individual)
      {
         return CreateMenuButton.WithCaption(PKSimConstants.MenuNames.ExportToPKML)
            .WithCommandFor<ExportIndividualToPKMLCommand, Individual>(individual, _container)
            .WithIcon(ApplicationIcons.PKMLSave);
      }

      private IMenuBarButton newPopulationMenuFor(Individual individual)
      {
         return CreateMenuButton.WithCaption(PKSimConstants.MenuNames.NewPopulation)
            .WithIcon(ApplicationIcons.Population)
            .WithCommandFor<CreatePopulationBasedOnIndividualCommand, Individual>(individual, _container);
      }

      private IMenuBarButton scaleIndividualMenuFor(Individual individual)
      {
         return CreateMenuButton.WithCaption(PKSimConstants.MenuNames.Scale)
            .WithIcon(ApplicationIcons.ScaleIndividual)
            .WithCommandFor<ScaleIndividualCommand, Individual>(individual, _container)
            .AsGroupStarter();
      }
   }

   public class IndividualTreeNodeContextMenuFactory : NodeContextMenuFactory<Individual>
   {
      private readonly IContainer _container;

      public IndividualTreeNodeContextMenuFactory(IContainer container)
      {
         _container = container;
      }

      public override IContextMenu CreateFor(Individual individual, IPresenterWithContextMenu<ITreeNode> presenter)
      {
         return new IndividualContextMenu(individual, _container);
      }
   }
}
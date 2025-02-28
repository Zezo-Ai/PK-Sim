﻿using System.Drawing;
using System.Windows.Forms;
using PKSim.Assets;
using OSPSuite.Assets;
using OSPSuite.Utility.Extensions;
using PKSim.Presentation.Presenters.Simulations;
using PKSim.Presentation.Views.Simulations;
using PKSim.UI.Views.Core;
using static PKSim.UI.UIConstants.Size;

namespace PKSim.UI.Views.Simulations
{
   public partial class CreateSimulationView : BuildingBlockWizardView, ICreateSimulationView
   {
      public CreateSimulationView(Shell shell) : base(shell)
      {
         InitializeComponent();
         ClientSize = new Size(SIMULATION_VIEW_WIDTH, CREATE_SIMULATION_VIEW_HEIGHT);
      }

      public void AttachPresenter(ISimulationWizardPresenter presenter)
      {
         WizardPresenter = presenter;
      }

      public override void InitializeResources()
      {
         base.InitializeResources();
         ApplicationIcon = ApplicationIcons.Simulation;
         Caption = PKSimConstants.UI.CreateSimulation;
         btnOk.DialogResult = DialogResult.None;
      }

      public override void InitializeBinding()
      {
         base.InitializeBinding();
         btnOk.Click += (o, e) => OnEvent(simulationWizardPresenter.CreateSimulation);
      }
      private ISimulationWizardPresenter simulationWizardPresenter => WizardPresenter.DowncastTo<ISimulationWizardPresenter>();
   }
}
﻿using System;
using System.Collections.Generic;
using DevExpress.XtraLayout;
using OSPSuite.Presentation.Views;
using OSPSuite.UI.Controls;
using OSPSuite.UI.Services;
using OSPSuite.Utility.Extensions;
using PKSim.Presentation.Presenters.Simulations;
using PKSim.Presentation.Views.Simulations;

namespace PKSim.UI.Views.Simulations
{
   public partial class SimulationCompoundProcessSummaryView : BaseContainerUserControl, ISimulationCompoundProcessSummaryView
   {
      private readonly IList<IResizableView> _subViews;
      public event EventHandler<ViewResizedEventArgs> HeightChanged = delegate { };

      public SimulationCompoundProcessSummaryView(IImageListRetriever imageListRetriever)
      {
         _subViews = new List<IResizableView>();
         InitializeComponent();
         layoutControl.Images = imageListRetriever.AllImages16x16;
      }

      public void AttachPresenter(ISimulationCompoundProcessSummaryPresenter presenter)
      {
         /*nothing to do*/
      }

      public void AddProcessView(IResizableView view)
      {
         _subViews.Add(view);
         AddViewTo(layoutMainGroup, layoutControl, view);
      }

      protected override void AdjustLayoutItemSize(LayoutControlItem layoutControlItem, LayoutControl layoutControl, IResizableView view, int height)
      {
         base.AdjustLayoutItemSize(layoutControlItem, layoutControl, view, height);
         HeightChanged(this, new ViewResizedEventArgs(OptimalHeight));
      }

      public int OptimalHeight => layoutMainGroup.Height;

      public void AdjustHeight()
      {
         _subViews.Each(view => view.AdjustHeight());
      }

      public void Repaint()
      {
         _subViews.Each(view => view.Repaint());
      }

      public int DefaultHeight => UIConstants.Size.SIMULATION_COMPOUND_PROCESS_DEFAULT_HEIGHT;
   }
}
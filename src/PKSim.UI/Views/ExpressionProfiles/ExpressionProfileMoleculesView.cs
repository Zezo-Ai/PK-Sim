﻿using OSPSuite.Assets;
using OSPSuite.DataBinding;
using OSPSuite.DataBinding.DevExpress;
using OSPSuite.Presentation.Extensions;
using OSPSuite.Presentation.Views;
using OSPSuite.UI.Controls;
using OSPSuite.UI.Extensions;
using OSPSuite.UI.Services;
using PKSim.Assets;
using PKSim.Presentation.DTO.ExpressionProfiles;
using PKSim.Presentation.Presenters.ExpressionProfiles;
using PKSim.Presentation.Views.ExpressionProfiles;
using PKSim.UI.Extensions;

namespace PKSim.UI.Views.ExpressionProfiles
{
   public partial class ExpressionProfileMoleculesView : BaseUserControl, IExpressionProfileMoleculesView
   {
      private readonly IImageListRetriever _imageListRetriever;
      private readonly ScreenBinder<ExpressionProfileDTO> _screenBinder = new ScreenBinder<ExpressionProfileDTO>();
      private IExpressionProfileMoleculesPresenter _presenter;

      public ExpressionProfileMoleculesView(IImageListRetriever imageListRetriever)
      {
         _imageListRetriever = imageListRetriever;
         InitializeComponent();
      }

      public void AttachPresenter(IExpressionProfileMoleculesPresenter presenter)
      {
         _presenter = presenter;
      }

      public void BindTo(ExpressionProfileDTO expressionProfileDTO)
      {
         ActiveControl = cbMoleculeName;
         cbMoleculeName.FillWith(expressionProfileDTO.AllMolecules);
         layoutItemMoleculeName.Text = expressionProfileDTO.MoleculeType.FormatForLabel();
         _screenBinder.BindToSource(expressionProfileDTO);
         NotifyViewChanged();
      }

      public void AddExpressionView(IView view)
      {
         panelExpression.FillWith(view);
      }

      public void DisableSettings()
      {
         layoutItemSpecies.Enabled = false;
         cbSpecies.Enabled = false;
         layoutItemCategory.Enabled = false;
         tbCategory.Enabled = false;
         layoutItemMoleculeName.Enabled = false;
         cbMoleculeName.Enabled = false;
      }

      public override void InitializeBinding()
      {
         base.InitializeBinding();

         _screenBinder.Bind(dto => dto.Species)
            .To(cbSpecies)
            .WithImages(species => _imageListRetriever.ImageIndex(species.Icon))
            .WithValues(dto => dto.AllSpecies)
            .AndDisplays(species => species.DisplayName);

         _screenBinder.Bind(dto => dto.MoleculeName)
            .To(cbMoleculeName);

         _screenBinder.Bind(dto => dto.Category)
            .To(tbCategory);


         btnLoadFromDatabase.Click += (ot, e) => OnEvent(_presenter.LoadExpressionFromDatabaseQuery);
         RegisterValidationFor(_screenBinder, statusChangingNotify: NotifyViewChanged);
      }

      public override void InitializeResources()
      {
         base.InitializeResources();
         layoutItemSpecies.Text = PKSimConstants.UI.Species.FormatForLabel();
         layoutItemCategory.Text = PKSimConstants.UI.ExpressionProfileCategory.FormatForLabel();
         btnLoadFromDatabase.InitWithImage(ApplicationIcons.ExpressionProfile, PKSimConstants.UI.LoadExpressionFromDatabase);
         tablePanel.AdjustLongButtonWidth(btnLoadFromDatabase);
         //Make size of buttons the size of the combo box
         btnLoadFromDatabase.Margin = cbMoleculeName.Margin;
         btnLoadFromDatabase.Height = cbMoleculeName.Height;
         DisableSettings();
      }

      public override bool HasError => _screenBinder.HasError || base.HasError;
   }
}
﻿using PKSim.UI.Views.Core;
using OSPSuite.UI.Views;
using OSPSuite.UI.Controls;

namespace PKSim.UI.Views
{
   partial class TemplateView
   {
      /// <summary>
      /// Required designer variable.
      /// </summary>
      private System.ComponentModel.IContainer components = null;

      /// <summary>
      /// Clean up any resources being used.
      /// </summary>
      /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
      protected override void Dispose(bool disposing)
      {
         if (disposing && (components != null))
         {
            components.Dispose();
         }
         _gridViewBinder.Dispose();
         _screenBinder.Dispose();
         base.Dispose(disposing);
      }

      #region Windows Form Designer generated code

      /// <summary>
      /// Required method for Designer support - do not modify
      /// the contents of this method with the code editor.
      /// </summary>
      private void InitializeComponent()
      {
         this.components = new System.ComponentModel.Container();
         this.layoutControl = new OSPSuite.UI.Controls.UxLayoutControl();
         this.lblDescription = new DevExpress.XtraEditors.LabelControl();
         this.gridControl = new OSPSuite.UI.Controls.UxGridControl();
         this.gridView = new PKSim.UI.Views.Core.UxGridView();
         this.layoutMainGroup = new DevExpress.XtraLayout.LayoutControlGroup();
         this.layoutControlItem1 = new DevExpress.XtraLayout.LayoutControlItem();
         this.layoutItemDescription = new DevExpress.XtraLayout.LayoutControlItem();
         this.toolTipController = new DevExpress.Utils.ToolTipController(this.components);
         this.chkShowQualifiedTemplate = new DevExpress.XtraEditors.CheckEdit();
         this.layoutItemShowQualifiedTemplate = new DevExpress.XtraLayout.LayoutControlItem();
         ((System.ComponentModel.ISupportInitialize)(this.layoutControlBase)).BeginInit();
         this.layoutControlBase.SuspendLayout();
         ((System.ComponentModel.ISupportInitialize)(this.layoutControlGroupBase)).BeginInit();
         ((System.ComponentModel.ISupportInitialize)(this.layoutItemOK)).BeginInit();
         ((System.ComponentModel.ISupportInitialize)(this.layoutItemCancel)).BeginInit();
         ((System.ComponentModel.ISupportInitialize)(this.emptySpaceItemBase)).BeginInit();
         ((System.ComponentModel.ISupportInitialize)(this.layoutItemExtra)).BeginInit();
         ((System.ComponentModel.ISupportInitialize)(this._errorProvider)).BeginInit();
         ((System.ComponentModel.ISupportInitialize)(this.layoutControl)).BeginInit();
         this.layoutControl.SuspendLayout();
         ((System.ComponentModel.ISupportInitialize)(this.gridControl)).BeginInit();
         ((System.ComponentModel.ISupportInitialize)(this.gridView)).BeginInit();
         ((System.ComponentModel.ISupportInitialize)(this.layoutMainGroup)).BeginInit();
         ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem1)).BeginInit();
         ((System.ComponentModel.ISupportInitialize)(this.layoutItemDescription)).BeginInit();
         ((System.ComponentModel.ISupportInitialize)(this.chkShowQualifiedTemplate.Properties)).BeginInit();
         ((System.ComponentModel.ISupportInitialize)(this.layoutItemShowQualifiedTemplate)).BeginInit();
         this.SuspendLayout();
         // 
         // btnCancel
         // 
         this.btnCancel.Location = new System.Drawing.Point(832, 14);
         this.btnCancel.Size = new System.Drawing.Size(166, 27);
         // 
         // btnOk
         // 
         this.btnOk.Location = new System.Drawing.Point(632, 14);
         this.btnOk.Size = new System.Drawing.Size(196, 27);
         // 
         // layoutControlBase
         // 
         this.layoutControlBase.Controls.Add(this.chkShowQualifiedTemplate);
         this.layoutControlBase.Location = new System.Drawing.Point(0, 887);
         this.layoutControlBase.Size = new System.Drawing.Size(1011, 57);
         this.layoutControlBase.Controls.SetChildIndex(this.btnCancel, 0);
         this.layoutControlBase.Controls.SetChildIndex(this.btnOk, 0);
         this.layoutControlBase.Controls.SetChildIndex(this.btnExtra, 0);
         this.layoutControlBase.Controls.SetChildIndex(this.chkShowQualifiedTemplate, 0);
         // 
         // btnExtra
         // 
         this.btnExtra.Location = new System.Drawing.Point(200, 14);
         this.btnExtra.Size = new System.Drawing.Size(140, 27);
         // 
         // layoutControlGroupBase
         // 
         this.layoutControlGroupBase.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[] {
            this.layoutItemShowQualifiedTemplate});
         this.layoutControlGroupBase.Size = new System.Drawing.Size(1011, 57);
         // 
         // layoutItemOK
         // 
         this.layoutItemOK.Location = new System.Drawing.Point(619, 0);
         this.layoutItemOK.Size = new System.Drawing.Size(200, 33);
         // 
         // layoutItemCancel
         // 
         this.layoutItemCancel.Location = new System.Drawing.Point(819, 0);
         this.layoutItemCancel.Size = new System.Drawing.Size(170, 33);
         // 
         // emptySpaceItemBase
         // 
         this.emptySpaceItemBase.Location = new System.Drawing.Point(331, 0);
         this.emptySpaceItemBase.Size = new System.Drawing.Size(288, 33);
         // 
         // layoutItemExtra
         // 
         this.layoutItemExtra.Location = new System.Drawing.Point(187, 0);
         this.layoutItemExtra.Size = new System.Drawing.Size(144, 33);
         // 
         // layoutControl
         // 
         this.layoutControl.AllowCustomization = false;
         this.layoutControl.Controls.Add(this.lblDescription);
         this.layoutControl.Controls.Add(this.gridControl);
         this.layoutControl.Dock = System.Windows.Forms.DockStyle.Fill;
         this.layoutControl.Location = new System.Drawing.Point(0, 0);
         this.layoutControl.Margin = new System.Windows.Forms.Padding(4);
         this.layoutControl.Name = "layoutControl";
         this.layoutControl.Root = this.layoutMainGroup;
         this.layoutControl.Size = new System.Drawing.Size(1011, 887);
         this.layoutControl.TabIndex = 34;
         this.layoutControl.Text = "layoutControl1";
         // 
         // lblDescription
         // 
         this.lblDescription.Location = new System.Drawing.Point(12, 12);
         this.lblDescription.Margin = new System.Windows.Forms.Padding(4);
         this.lblDescription.Name = "lblDescription";
         this.lblDescription.Size = new System.Drawing.Size(76, 16);
         this.lblDescription.StyleController = this.layoutControl;
         this.lblDescription.TabIndex = 5;
         this.lblDescription.Text = "lblDescription";
         // 
         // gridControl
         // 
         this.gridControl.EmbeddedNavigator.Margin = new System.Windows.Forms.Padding(5);
         this.gridControl.Location = new System.Drawing.Point(12, 32);
         this.gridControl.MainView = this.gridView;
         this.gridControl.Margin = new System.Windows.Forms.Padding(4);
         this.gridControl.Name = "gridControl";
         this.gridControl.Size = new System.Drawing.Size(987, 843);
         this.gridControl.TabIndex = 4;
         this.gridControl.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.gridView});
         // 
         // gridView
         // 
         this.gridView.AllowsFiltering = true;
         this.gridView.DetailHeight = 431;
         this.gridView.EnableColumnContextMenu = true;
         this.gridView.GridControl = this.gridControl;
         this.gridView.MultiSelect = false;
         this.gridView.Name = "gridView";
         this.gridView.OptionsSelection.EnableAppearanceFocusedRow = false;
         // 
         // layoutMainGroup
         // 
         this.layoutMainGroup.CustomizationFormText = "layoutMainGroup";
         this.layoutMainGroup.EnableIndentsWithoutBorders = DevExpress.Utils.DefaultBoolean.True;
         this.layoutMainGroup.GroupBordersVisible = false;
         this.layoutMainGroup.Items.AddRange(new DevExpress.XtraLayout.BaseLayoutItem[] {
            this.layoutControlItem1,
            this.layoutItemDescription});
         this.layoutMainGroup.Name = "layoutMainGroup";
         this.layoutMainGroup.Size = new System.Drawing.Size(1011, 887);
         this.layoutMainGroup.TextVisible = false;
         // 
         // layoutControlItem1
         // 
         this.layoutControlItem1.Control = this.gridControl;
         this.layoutControlItem1.Location = new System.Drawing.Point(0, 20);
         this.layoutControlItem1.Name = "layoutControlItem1";
         this.layoutControlItem1.Size = new System.Drawing.Size(991, 847);
         this.layoutControlItem1.TextSize = new System.Drawing.Size(0, 0);
         this.layoutControlItem1.TextVisible = false;
         // 
         // layoutItemDescription
         // 
         this.layoutItemDescription.Control = this.lblDescription;
         this.layoutItemDescription.Location = new System.Drawing.Point(0, 0);
         this.layoutItemDescription.Name = "layoutItemDescription";
         this.layoutItemDescription.Size = new System.Drawing.Size(991, 20);
         this.layoutItemDescription.TextSize = new System.Drawing.Size(0, 0);
         this.layoutItemDescription.TextVisible = false;
         // 
         // chkShowQualifiedTemplate
         // 
         this.chkShowQualifiedTemplate.Location = new System.Drawing.Point(13, 16);
         this.chkShowQualifiedTemplate.Name = "chkShowQualifiedTemplate";
         this.chkShowQualifiedTemplate.Properties.Caption = "chkShowQualifiedTemplate";
         this.chkShowQualifiedTemplate.Size = new System.Drawing.Size(183, 24);
         this.chkShowQualifiedTemplate.StyleController = this.layoutControlBase;
         this.chkShowQualifiedTemplate.TabIndex = 33;
         // 
         // layoutItemShowQualifiedTemplate
         // 
         this.layoutItemShowQualifiedTemplate.ContentVertAlignment = DevExpress.Utils.VertAlignment.Center;
         this.layoutItemShowQualifiedTemplate.Control = this.chkShowQualifiedTemplate;
         this.layoutItemShowQualifiedTemplate.Location = new System.Drawing.Point(0, 0);
         this.layoutItemShowQualifiedTemplate.Name = "layoutItemShowQualifiedTemplate";
         this.layoutItemShowQualifiedTemplate.Size = new System.Drawing.Size(187, 33);
         this.layoutItemShowQualifiedTemplate.TextSize = new System.Drawing.Size(0, 0);
         this.layoutItemShowQualifiedTemplate.TextVisible = false;
         // 
         // TemplateView
         // 
         this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
         this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
         this.Caption = "BuildingBlockFromTemplateView";
         this.ClientSize = new System.Drawing.Size(1011, 944);
         this.Controls.Add(this.layoutControl);
         this.Margin = new System.Windows.Forms.Padding(6);
         this.Name = "TemplateView";
         this.Text = "BuildingBlockFromTemplateView";
         this.Controls.SetChildIndex(this.layoutControlBase, 0);
         this.Controls.SetChildIndex(this.layoutControl, 0);
         ((System.ComponentModel.ISupportInitialize)(this.layoutControlBase)).EndInit();
         this.layoutControlBase.ResumeLayout(false);
         ((System.ComponentModel.ISupportInitialize)(this.layoutControlGroupBase)).EndInit();
         ((System.ComponentModel.ISupportInitialize)(this.layoutItemOK)).EndInit();
         ((System.ComponentModel.ISupportInitialize)(this.layoutItemCancel)).EndInit();
         ((System.ComponentModel.ISupportInitialize)(this.emptySpaceItemBase)).EndInit();
         ((System.ComponentModel.ISupportInitialize)(this.layoutItemExtra)).EndInit();
         ((System.ComponentModel.ISupportInitialize)(this._errorProvider)).EndInit();
         ((System.ComponentModel.ISupportInitialize)(this.layoutControl)).EndInit();
         this.layoutControl.ResumeLayout(false);
         ((System.ComponentModel.ISupportInitialize)(this.gridControl)).EndInit();
         ((System.ComponentModel.ISupportInitialize)(this.gridView)).EndInit();
         ((System.ComponentModel.ISupportInitialize)(this.layoutMainGroup)).EndInit();
         ((System.ComponentModel.ISupportInitialize)(this.layoutControlItem1)).EndInit();
         ((System.ComponentModel.ISupportInitialize)(this.layoutItemDescription)).EndInit();
         ((System.ComponentModel.ISupportInitialize)(this.chkShowQualifiedTemplate.Properties)).EndInit();
         ((System.ComponentModel.ISupportInitialize)(this.layoutItemShowQualifiedTemplate)).EndInit();
         this.ResumeLayout(false);
         this.PerformLayout();

      }

      #endregion

      private OSPSuite.UI.Controls.UxLayoutControl layoutControl;
      private DevExpress.XtraLayout.LayoutControlGroup layoutMainGroup;
      private DevExpress.Utils.ToolTipController toolTipController;
      private OSPSuite.UI.Controls.UxGridControl gridControl;
      private PKSim.UI.Views.Core.UxGridView gridView;
      private DevExpress.XtraLayout.LayoutControlItem layoutControlItem1;
      private DevExpress.XtraEditors.LabelControl lblDescription;
      private DevExpress.XtraLayout.LayoutControlItem layoutItemDescription;
      private DevExpress.XtraEditors.CheckEdit chkShowQualifiedTemplate;
      private DevExpress.XtraLayout.LayoutControlItem layoutItemShowQualifiedTemplate;
   }
}
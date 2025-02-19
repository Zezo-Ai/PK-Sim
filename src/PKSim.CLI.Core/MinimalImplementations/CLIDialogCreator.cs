﻿using System;
using System.Collections.Generic;
using OSPSuite.Core.Services;

namespace PKSim.CLI.Core.MinimalImplementations
{
   public class CLIDialogCreator : IDialogCreator
   {
      private readonly IOSPSuiteLogger _logger;

      public CLIDialogCreator(IOSPSuiteLogger logger)
      {
         _logger = logger;
      }

      public void MessageBoxError(string message)
      {
         _logger.AddError(message);
      }

      public ViewResult MessageBoxConfirm(string message, Action doNotShowAgain, ViewResult defaultButton = ViewResult.Yes)
      {
         throw new NotSupportedException();
      }

      public ViewResult MessageBoxYesNo(string message, string yes, string no, ViewResult defaultButton)
      {
         throw new NotSupportedException();
      }

      public void MessageBoxInfo(string message)
      {
         _logger.AddInfo(message);
      }

      public ViewResult MessageBoxYesNoCancel(string message)
      {
         throw new NotSupportedException();
      }

      public ViewResult MessageBoxYesNoCancel(string message, ViewResult defaultButton)
      {
         throw new NotSupportedException();
      }

      public ViewResult MessageBoxYesNoCancel(string message, string yes, string no, string cancel)
      {
         throw new NotSupportedException();
      }

      public ViewResult MessageBoxYesNoCancel(string message, string yes, string no, string cancel, ViewResult defaultButton)
      {
         throw new NotSupportedException();
      }

      public ViewResult MessageBoxYesNo(string message)
      {
         throw new NotSupportedException();
      }

      public ViewResult MessageBoxYesNo(string message, ViewResult defaultButton)
      {
         throw new NotSupportedException();
      }

      public ViewResult MessageBoxYesNo(string message, string yes, string no)
      {
         throw new NotSupportedException();
      }

      public string AskForFileToOpen(string title, string filter, string directoryKey, string defaultFileName = null, string defaultDirectory = null)
      {
         throw new NotSupportedException();
      }

      public string AskForFileToSave(string title, string filter, string directoryKey, string defaultFileName = null, string defaultDirectory = null)
      {
         throw new NotSupportedException();
      }

      public string AskForFolder(string title, string directoryKey, string defaultDirectory = null)
      {
         throw new NotSupportedException();
      }

      public string AskForInput(string caption, string text, string defaultValue = null, IEnumerable<string> forbiddenValues = null, IEnumerable<string> predefinedValues = null, string iconName = null)
      {
         throw new NotSupportedException();
      }
   }
}
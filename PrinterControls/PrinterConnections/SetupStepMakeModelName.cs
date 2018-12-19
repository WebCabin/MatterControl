﻿using System;
using System.Collections.Generic;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{   
    //Normally step one of the setup process
    public class SetupStepMakeModelName : SetupConnectionWidgetBase
    {        
        FlowLayoutWidget printerModelContainer;
        FlowLayoutWidget printerMakeContainer;
        FlowLayoutWidget printerNameContainer;

        MHTextEditWidget printerNameInput;

        List<CustomCommands> printerCustomCommands;

        TextWidget printerNameError;
        TextWidget printerModelError;
        TextWidget printerMakeError;
        
        string driverFile;

        Button nextButton;

        public SetupStepMakeModelName(ConnectionWindow windowController, GuiWidget containerWindowToClose, PrinterSetupStatus setupPrinter = null)
            : base(windowController, containerWindowToClose, setupPrinter)
        {
            //Construct inputs
            printerNameContainer = createPrinterNameContainer();
            printerMakeContainer = createPrinterMakeContainer();
            printerModelContainer = createPrinterModelContainer();

            //Add inputs to main container
            contentRow.AddChild(printerNameContainer);
            contentRow.AddChild(printerMakeContainer);
            contentRow.AddChild(printerModelContainer);

            //Construct buttons
			nextButton = textImageButtonFactory.Generate(LocalizedString.Get("Save & Continue"));
            nextButton.Click += new ButtonBase.ButtonEventHandler(NextButton_Click);

            GuiWidget hSpacer = new GuiWidget();
            hSpacer.HAnchor = HAnchor.ParentLeftRight;

            //Add buttons to buttonContainer
            footerRow.AddChild(nextButton);
            footerRow.AddChild(hSpacer);
            footerRow.AddChild(cancelButton);

            SetElementState();
        }

        private void SetElementState()
        {
            printerModelContainer.Visible = (this.ActivePrinter.Make != null);
            nextButton.Visible = (this.ActivePrinter.Model != null && this.ActivePrinter.Make !=null);
        }

        private FlowLayoutWidget createPrinterNameContainer()
        {
            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.Margin = new BorderDouble(0, 5);
            BorderDouble elementMargin = new BorderDouble(top: 3);

			string printerNameLabelTxt = LocalizedString.Get("Printer Name");
			string printerNameLabelTxtFull = string.Format ("{0}:", printerNameLabelTxt);
			TextWidget printerNameLabel = new TextWidget(printerNameLabelTxtFull, 0, 0, 12);
            printerNameLabel.TextColor = this.defaultTextColor;
            printerNameLabel.HAnchor = HAnchor.ParentLeftRight;
            printerNameLabel.Margin = new BorderDouble(0, 0, 0, 1);

            printerNameInput = new MHTextEditWidget(this.ActivePrinter.Name);
            printerNameInput.HAnchor = HAnchor.ParentLeftRight;

			printerNameError = new TextWidget(LocalizedString.Get("Give your printer a name."), 0, 0, 10);
			printerNameError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            printerNameError.HAnchor = HAnchor.ParentLeftRight;
            printerNameError.Margin = elementMargin;

            container.AddChild(printerNameLabel);
            container.AddChild(printerNameInput);
            container.AddChild(printerNameError);
            container.HAnchor = HAnchor.ParentLeftRight;
            return container;
        }

        private FlowLayoutWidget createPrinterMakeContainer()
        {
            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.Margin = new BorderDouble(0, 5);
            BorderDouble elementMargin = new BorderDouble(top: 3);

			string printerManufacturerLabelTxt = LocalizedString.Get("Select Make");
			string printerManufacturerLabelTxtFull = string.Format("{0}:", printerManufacturerLabelTxt);
			TextWidget printerManufacturerLabel = new TextWidget(printerManufacturerLabelTxtFull, 0, 0, 12);
            printerManufacturerLabel.TextColor = this.defaultTextColor;
            printerManufacturerLabel.HAnchor = HAnchor.ParentLeftRight;
            printerManufacturerLabel.Margin = elementMargin;

            PrinterChooser printerManufacturerSelector = new PrinterChooser();
            printerManufacturerSelector.HAnchor = HAnchor.ParentLeftRight;
            printerManufacturerSelector.Margin = elementMargin;
            printerManufacturerSelector.ManufacturerDropList.SelectionChanged += new EventHandler(ManufacturerDropList_SelectionChanged);

			printerMakeError = new TextWidget(LocalizedString.Get("Select the printer manufacturer"), 0, 0, 10);
			printerMakeError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            printerMakeError.HAnchor = HAnchor.ParentLeftRight;
            printerMakeError.Margin = elementMargin;

            container.AddChild(printerManufacturerLabel);
            container.AddChild(printerManufacturerSelector);
            container.AddChild(printerMakeError);

            container.HAnchor = HAnchor.ParentLeftRight;
            return container;
        }

		private FlowLayoutWidget createPrinterModelContainer(string make = "Other")
        {
            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.Margin = new BorderDouble(0, 5);
            BorderDouble elementMargin = new BorderDouble(top: 3);

			string printerModelLabelTxt = LocalizedString.Get("Select Model");
			string printerModelLabelTxtFull = string.Format ("{0}:", printerModelLabelTxt);
			TextWidget printerModelLabel = new TextWidget(printerModelLabelTxtFull, 0, 0, 12);
            printerModelLabel.TextColor = this.defaultTextColor;
            printerModelLabel.HAnchor = HAnchor.ParentLeftRight;
            printerModelLabel.Margin = elementMargin;

            ModelChooser printerModelSelector = new ModelChooser(make);
            printerModelSelector.HAnchor = HAnchor.ParentLeftRight;
            printerModelSelector.Margin = elementMargin;
            printerModelSelector.ModelDropList.SelectionChanged += new EventHandler(ModelDropList_SelectionChanged);

			printerModelError = new TextWidget(LocalizedString.Get("Select the printer model"), 0, 0, 10);
			printerModelError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            printerModelError.HAnchor = HAnchor.ParentLeftRight;
            printerModelError.Margin = elementMargin;

            container.AddChild(printerModelLabel);
            container.AddChild(printerModelSelector);
            container.AddChild(printerModelError);

            container.HAnchor = HAnchor.ParentLeftRight;
            return container;
        }

        private void ManufacturerDropList_SelectionChanged(object sender, EventArgs e)
        {
            ActivePrinter.Make = ((DropDownList)sender).SelectedLabel;
            ActivePrinter.Model = null;

            //reconstruct model selector
            int currentIndex = contentRow.GetChildIndex(printerModelContainer);
            contentRow.RemoveChild(printerModelContainer);

            printerModelContainer = createPrinterModelContainer(ActivePrinter.Make);
            contentRow.AddChild(printerModelContainer, currentIndex);
            contentRow.Invalidate();

            printerMakeError.Visible = false;
            SetElementState();
        }

        private void ModelDropList_SelectionChanged(object sender, EventArgs e)
        {
            ActivePrinter.Model = ((DropDownList)sender).SelectedLabel;
            LoadSetupSettings(ActivePrinter.Make, ActivePrinter.Model);            
            printerModelError.Visible = false;
            SetElementState();
        }

        string defaultMaterialPreset;
        string defaultQualityPreset;
        private void LoadSetupSettings(string make, string model)
        {
            Dictionary<string, string> settingsDict = LoadPrinterSetupFromFile(make, model);
            Dictionary<string, string> macroDict = new Dictionary<string, string>();
            macroDict["Lights On"] = "M42 P6 S255";
            macroDict["Lights Off"] = "M42 P6 S0";
            
            //Determine if baud rate is needed and show controls if required
            string baudRate;
            if (settingsDict.TryGetValue("baud_rate", out baudRate))
            {
                ActivePrinter.BaudRate = baudRate;
            }

            // Check if we need to run the print level wizard before printing
            PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(ActivePrinter);
            string needsPrintLeveling;
            if (settingsDict.TryGetValue("needs_print_leveling", out needsPrintLeveling))
            {
                levelingData.needsPrintLeveling = true;
            }

            string printLevelingType;
            if (settingsDict.TryGetValue("print_leveling_type", out printLevelingType))
            {
                levelingData.levelingSystem = PrintLevelingData.LevelingSystem.Probe2Points;
            }

            string defaultSliceEngine;
            if (settingsDict.TryGetValue("default_slice_engine", out defaultSliceEngine))
            {
                if (Enum.IsDefined(typeof(ActivePrinterProfile.SlicingEngineTypes), defaultSliceEngine))
                {
                    ActivePrinter.CurrentSlicingEngine = defaultSliceEngine;
                }
            }

            settingsDict.TryGetValue("default_material_presets", out defaultMaterialPreset);
            settingsDict.TryGetValue("default_quality_preset", out defaultQualityPreset);

            string defaultMacros;
            printerCustomCommands = new List<CustomCommands>();
            if (settingsDict.TryGetValue("default_macros", out defaultMacros))
            {
                string[] macroList = defaultMacros.Split(',');
                foreach (string macroName in macroList)
                {
                    string macroValue;
                    if (macroDict.TryGetValue(macroName.Trim(), out macroValue))
                    {
                        CustomCommands customMacro = new CustomCommands();
                        customMacro.Name = macroName.Trim();
                        customMacro.Value = macroValue;

                        printerCustomCommands.Add(customMacro);
                        
                    }
                }
            }

            //Determine what if any drivers are needed
            if (settingsDict.TryGetValue("windows_driver", out driverFile))
            {
                string infPathAndFileToInstall = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "Drivers", driverFile);
                switch (OsInformation.OperatingSystem)
                {
                    case OSType.Windows:
                        if (File.Exists(infPathAndFileToInstall))
                        {
                            PrinterSetupStatus.DriverNeedsToBeInstalled = true;
                            PrinterSetupStatus.DriverFilePath = infPathAndFileToInstall;
                        }
                        break;

                    default:
                        break;
                }
            }
        }

        private Dictionary<string, string> LoadPrinterSetupFromFile(string make, string model)
        {
            string setupSettingsPathAndFile = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "PrinterSettings", make, model, "setup.ini");
            Dictionary<string, string> settingsDict = new Dictionary<string, string>();

            if (System.IO.File.Exists(setupSettingsPathAndFile))
            {
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(setupSettingsPathAndFile);
                    foreach (string line in lines)
                    {
                        //Ignore commented lines
                        if (!line.StartsWith("#"))
                        {
                            string[] settingLine = line.Split('=');
                            string keyName = settingLine[0].Trim();
                            string settingDefaultValue = settingLine[1].Trim();

                            settingsDict.Add(keyName, settingDefaultValue);
                        }
                    }
                }
                catch
                {

                }
            }
            return settingsDict;
        }

        private SliceSettingsCollection LoadDefaultSliceSettings(string make, string model)
        {
            SliceSettingsCollection collection = null;
            Dictionary<string, string> settingsDict = LoadSliceSettingsFromFile(GetDefaultPrinterSlicePath(make, model));
            
            if (settingsDict.Count > 0)
            {
                collection = new DataStorage.SliceSettingsCollection();
                collection.Name = this.ActivePrinter.Name;
                collection.Commit();

                this.ActivePrinter.DefaultSettingsCollectionId = collection.Id;

                CommitSliceSettings(settingsDict, collection.Id);
            }
            return collection;
        }

        private void LoadSlicePresets(string make, string model, string tag)
        {
            string[] slicePresetPaths = GetSlicePresets(make, model, tag);

            foreach (string filePath in slicePresetPaths)
            {
                SliceSettingsCollection collection = null;
                Dictionary<string, string> settingsDict = LoadSliceSettingsFromFile(filePath);

                if (settingsDict.Count > 0)
                {
                    collection = new DataStorage.SliceSettingsCollection();
                    collection.Name = Path.GetFileNameWithoutExtension(filePath);
                    collection.PrinterId = ActivePrinter.Id;
                    collection.Tag = tag;
                    collection.Commit();

                    if (tag == "material" && defaultMaterialPreset != null && collection.Name == defaultMaterialPreset)
                    {
                        ActivePrinter.MaterialCollectionIds = collection.Id.ToString();
                        ActivePrinter.Commit();
                    }
                    else if (tag == "quality"  && defaultQualityPreset != null && collection.Name == defaultQualityPreset)
                    {
                        ActivePrinter.QualityCollectionId = collection.Id;
                        ActivePrinter.Commit();
                    }
                    CommitSliceSettings(settingsDict, collection.Id);
                }
            }
        }

        private void CommitSliceSettings(Dictionary<string, string> settingsDict, int collectionId)
        {
            foreach (KeyValuePair<string, string> item in settingsDict)
            {
                DataStorage.SliceSetting sliceSetting = new DataStorage.SliceSetting();
                sliceSetting.Name = item.Key;
                sliceSetting.Value = item.Value;
                sliceSetting.SettingsCollectionId = collectionId;
                sliceSetting.Commit();
            }
        }

        private string[] GetSlicePresets(string make, string model, string tag)
        {
            string[] presetPaths = new string[]{};
            string folderPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "PrinterSettings", make, model, tag);
            if (Directory.Exists(folderPath))
            {
                presetPaths = Directory.GetFiles(folderPath);
            }
            return presetPaths;
        }

        private string GetDefaultPrinterSlicePath(string make, string model)
        {
            return Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "PrinterSettings", make, model, "config.ini");
        }

        private Dictionary<string, string> LoadSliceSettingsFromFile(string setupSettingsPathAndFile)
        {            
            Dictionary<string, string> settingsDict = new Dictionary<string, string>();
            if (System.IO.File.Exists(setupSettingsPathAndFile))
            {
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(setupSettingsPathAndFile);
                    foreach (string line in lines)
                    {
                        //Ignore commented lines
                        if (!line.StartsWith("#"))
                        {
                            string[] settingLine = line.Split('=');
                            string keyName = settingLine[0].Trim();
                            string settingDefaultValue = settingLine[1].Trim();

                            settingsDict.Add(keyName, settingDefaultValue);
                        }
                    }
                }
                catch
                {

                }
            }
            return settingsDict;
        }


        void MoveToNextWidget(object state)
        {
            // you can call this like this
            //             AfterUiEvents.AddAction(new AfterUIAction(MoveToNextWidget));
            if (this.ActivePrinter.BaudRate == null)
            {
                Parent.AddChild(new SetupStepBaudRate((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
                Parent.RemoveChild(this);
            }
            else if (this.PrinterSetupStatus.DriverNeedsToBeInstalled)
            {
                Parent.AddChild(new SetupStepInstallDriver((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
                Parent.RemoveChild(this);
            }
            else
            {
                Parent.AddChild(new SetupStepComPortOne((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
                Parent.RemoveChild(this);
            }
        }

        void NextButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            bool canContinue = this.OnSave();
            if (canContinue)
            {
                this.PrinterSetupStatus.LoadCalibrationPrints();
                UiThread.RunOnIdle(MoveToNextWidget);
            }
        }

        void LoadSlicePresets()
        {

        }

        bool OnSave()
        {
            if (printerNameInput.Text != "")
            {
                this.ActivePrinter.Name = printerNameInput.Text;
                if (this.ActivePrinter.Make == null || this.ActivePrinter.Model == null)
                {
                    return false;
                }
                else
                {
                    //Load the default slice settings for the make and model combination - if they exist
                    SliceSettingsCollection collection = LoadDefaultSliceSettings(this.ActivePrinter.Make, this.ActivePrinter.Model);

                    //Ordering matters - need to get Id for printer prior to loading slice presets
                    this.ActivePrinter.AutoConnectFlag = true;
                    this.ActivePrinter.Commit();

                    LoadSlicePresets(this.ActivePrinter.Make, this.ActivePrinter.Model, "material");
                    LoadSlicePresets(this.ActivePrinter.Make, this.ActivePrinter.Model, "quality");

                    

                    foreach (CustomCommands customCommand in printerCustomCommands)
                    {
                        customCommand.PrinterId = ActivePrinter.Id;
                        customCommand.Commit();
                    }

                    return true;
                }
            }
            else
            {
                this.printerNameError.TextColor = RGBA_Bytes.Red;
				this.printerNameError.Text = "Printer name cannot be blank";
                this.printerNameError.Visible = true;
                return false;
            }
        }
    }
}

﻿/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met: 

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer. 
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution. 

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies, 
either expressed or implied, of the FreeBSD Project.
*/
using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{    
    public class OutputScrollWindow : SystemWindow
    {
        Button sendCommand;
		CheckBox filterOutput;
        CheckBox autoUppercase;
        CheckBox monitorPrinterTemperature;
        MHTextEditWidget manualCommandTextEdit;
        OutputScroll outputScrollWidget;
        RGBA_Bytes backgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
        RGBA_Bytes textColor = ActiveTheme.Instance.PrimaryTextColor;
        TextImageButtonFactory controlButtonFactory = new TextImageButtonFactory();

        public static void HookupPrinterOutput()
        {
            //throw new NotImplementedException();
        }

        static OutputScrollWindow connectionWindow = null;
        static bool terminalWindowIsOpen = false;
        public static void Show()
        {
            if (terminalWindowIsOpen == false)
            {
                connectionWindow = new OutputScrollWindow();
                terminalWindowIsOpen = true;
                connectionWindow.Closed += (parentSender, e) =>
                {
                    terminalWindowIsOpen = false;
                    connectionWindow = null;
                };
            }
            else
            {
                if (connectionWindow != null)
                {
                    connectionWindow.BringToFront();
                }
            }
        }

        // private as you can't make one
        private OutputScrollWindow()
            : base(400, 300)
        {
            this.BackgroundColor = backgroundColor;
            this.Padding = new BorderDouble(5);

            FlowLayoutWidget topLeftToRightLayout = new FlowLayoutWidget();
            topLeftToRightLayout.AnchorAll();

            {
                FlowLayoutWidget manualEntryTopToBottomLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
                manualEntryTopToBottomLayout.VAnchor |= Agg.UI.VAnchor.ParentTop;
                manualEntryTopToBottomLayout.Padding = new BorderDouble(5);

                {
                    FlowLayoutWidget OutputWindowsLayout = new FlowLayoutWidget(FlowDirection.LeftToRight);
                    OutputWindowsLayout.HAnchor |= HAnchor.ParentLeft;

					string filterOutputChkTxt = LocalizedString.Get("Filter Output");

					filterOutput = new CheckBox(filterOutputChkTxt);
                    filterOutput.Margin = new BorderDouble(5, 5, 5, 2);
                    filterOutput.Checked = false;
                    filterOutput.TextColor = this.textColor;
                    filterOutput.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(SetCorrectFilterOutputBehavior);
                    OutputWindowsLayout.AddChild(filterOutput);

					string autoUpperCaseChkTxt = LocalizedString.Get("Auto Uppercase");

					autoUppercase = new CheckBox(autoUpperCaseChkTxt);
                    autoUppercase.Margin = new BorderDouble(5, 5, 5, 2);
                    autoUppercase.Checked = true;
                    autoUppercase.TextColor = this.textColor;
                    OutputWindowsLayout.AddChild(autoUppercase);

                    monitorPrinterTemperature = new CheckBox("Monitor Temperature");
                    monitorPrinterTemperature.Margin = new BorderDouble(5, 5, 5, 2);
                    monitorPrinterTemperature.Checked = PrinterConnectionAndCommunication.Instance.MonitorPrinterTemperature;
                    monitorPrinterTemperature.TextColor = this.textColor;
                    monitorPrinterTemperature.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(monitorPrinterTemperature_CheckedStateChanged);

                    manualEntryTopToBottomLayout.AddChild(OutputWindowsLayout);
                }

                {
                    outputScrollWidget = new OutputScroll();
                    outputScrollWidget.Height = 100;
                    outputScrollWidget.BackgroundColor = RGBA_Bytes.White;
                    outputScrollWidget.HAnchor = HAnchor.ParentLeftRight;
                    outputScrollWidget.VAnchor = VAnchor.ParentBottomTop;
                    outputScrollWidget.Margin = new BorderDouble(0, 5);

                    manualEntryTopToBottomLayout.AddChild(outputScrollWidget);
                }

                FlowLayoutWidget manualEntryLayout = new FlowLayoutWidget(FlowDirection.LeftToRight);
                manualEntryLayout.BackgroundColor = this.backgroundColor;
                manualEntryLayout.HAnchor = HAnchor.ParentLeftRight;
                {
                    manualCommandTextEdit = new MHTextEditWidget("");
                    manualCommandTextEdit.BackgroundColor = RGBA_Bytes.White;
                    manualCommandTextEdit.HAnchor = HAnchor.ParentLeftRight;
                    manualCommandTextEdit.VAnchor = VAnchor.ParentCenter;
                    manualCommandTextEdit.ActualTextEditWidget.EnterPressed += new KeyEventHandler(manualCommandTextEdit_EnterPressed);
                    manualCommandTextEdit.ActualTextEditWidget.KeyDown += new KeyEventHandler(manualCommandTextEdit_KeyDown);
                    manualEntryLayout.AddChild(manualCommandTextEdit);

					sendCommand = controlButtonFactory.Generate(LocalizedString.Get("Send"));
                    sendCommand.Margin = new BorderDouble(5, 0);
                    sendCommand.Click += new ButtonBase.ButtonEventHandler(sendManualCommandToPrinter_Click);
                    manualEntryLayout.AddChild(sendCommand);
                }

                manualEntryTopToBottomLayout.AddChild(manualEntryLayout);
                manualEntryTopToBottomLayout.AnchorAll();

                topLeftToRightLayout.AddChild(manualEntryTopToBottomLayout);
            }

            AddHandlers();

            AddChild(topLeftToRightLayout);
            SetCorrectFilterOutputBehavior(this, null);
            this.AnchorAll();

			Title = LocalizedString.Get("MatterControl - Terminal");
            this.ShowAsSystemWindow();
            MinimumSize = new Vector2(Width, Height);
        }

        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            PrinterConnectionAndCommunication.Instance.ConnectionFailed.RegisterEvent(Instance_ConnectionFailed, ref unregisterEvents);
        }

        public override void OnClosed(EventArgs e)
        {
            // make sure we are not holding onto this window (keeping a pointer that can't be garbage collected).
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        void monitorPrinterTemperature_CheckedStateChanged(object sender, EventArgs e)
        {
            PrinterConnectionAndCommunication.Instance.MonitorPrinterTemperature = ((CheckBox)sender).Checked;
        }

        List<string> commandHistory = new List<string>();
        int commandHistoryIndex = 0;
        void manualCommandTextEdit_KeyDown(object sender, KeyEventArgs keyEvent)
        {
            bool changeToHistory = false;
            if (keyEvent.KeyCode == Keys.Up)
            {
                commandHistoryIndex--;
                if (commandHistoryIndex < 0)
                {
                    commandHistoryIndex = 0;
                }
                changeToHistory = true;
            }
            else if (keyEvent.KeyCode == Keys.Down)
            {
                commandHistoryIndex++;
                if (commandHistoryIndex > commandHistory.Count - 1)
                {
                    commandHistoryIndex = commandHistory.Count - 1;
                }
                else
                {
                    changeToHistory = true;
                }
            }
            else if (keyEvent.KeyCode == Keys.Escape)
            {
                manualCommandTextEdit.Text = "";
            }

            if (changeToHistory && commandHistory.Count > 0)
            {
                manualCommandTextEdit.Text = commandHistory[commandHistoryIndex];
            }
        }

        void manualCommandTextEdit_EnterPressed(object sender, KeyEventArgs keyEvent)
        {
            sendManualCommandToPrinter_Click(null, null);
        }

        void sendManualCommandToPrinter_Click(object sender, MouseEventArgs mouseEvent)
        {
            string textToSend = manualCommandTextEdit.Text.Trim();
            if (autoUppercase.Checked)
            {
                textToSend = textToSend.ToUpper();
            }
            commandHistory.Add(textToSend);
            commandHistoryIndex = commandHistory.Count;
            PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow(textToSend);
            if (!filterOutput.Checked)
            {
                outputScrollWidget.WriteLine(this, new StringEventArgs(textToSend));
            }
            manualCommandTextEdit.Text = "";
        }

        void SetCorrectFilterOutputBehavior(object sender, EventArgs e)
        {
            if (filterOutput.Checked)
            {
                PrinterConnectionAndCommunication.Instance.CommunicationUnconditionalFromPrinter.UnregisterEvent(FromPrinter, ref unregisterEvents);
                PrinterConnectionAndCommunication.Instance.CommunicationUnconditionalToPrinter.UnregisterEvent(ToPrinter, ref unregisterEvents);
                PrinterConnectionAndCommunication.Instance.ReadLine.RegisterEvent(outputScrollWidget.WriteLine, ref unregisterEvents);
            }
            else
            {
                PrinterConnectionAndCommunication.Instance.CommunicationUnconditionalFromPrinter.RegisterEvent(FromPrinter, ref unregisterEvents);
                PrinterConnectionAndCommunication.Instance.CommunicationUnconditionalToPrinter.RegisterEvent(ToPrinter, ref unregisterEvents);
                PrinterConnectionAndCommunication.Instance.ReadLine.UnregisterEvent(outputScrollWidget.WriteLine, ref unregisterEvents);
            }
        }

        void FromPrinter(Object sender, EventArgs e)
        {
            StringEventArgs lineString = e as StringEventArgs;
            outputScrollWidget.WriteLine(sender, new StringEventArgs("<-" + lineString.Data));
        }

        void ToPrinter(Object sender, EventArgs e)
        {
            StringEventArgs lineString = e as StringEventArgs;
            outputScrollWidget.WriteLine(sender, new StringEventArgs("->" + lineString.Data));
        }

        void Instance_ConnectionFailed(object sender, EventArgs e)
        {
            outputScrollWidget.WriteLine(sender, new StringEventArgs("Lost connection to printer."));
        }
    }
}

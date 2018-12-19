﻿/*
Copyright (c) 2012, Lars Brubaker
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
using System.Linq;
using System.Text;

using MatterHackers.VectorMath;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Image;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl
{
    
    
    public class JogControls : GuiWidget
    {
        MoveButton xPlusControl;
        MoveButton xMinusControl;

        MoveButton yPlusControl;
        MoveButton yMinusControl;

        MoveButton zPlusControl;
        MoveButton zMinusControl;

        MoveButton ePlusControl;
        MoveButton eMinusControl;

        MoveButtonFactory moveButtonFactory = new MoveButtonFactory();

        public JogControls(XYZColors colors)
        {
            moveButtonFactory.normalTextColor = RGBA_Bytes.Black;

            

			double distanceBetweenControls = 12; 
            double buttonSeparationDistance = 10;

            FlowLayoutWidget allControlsTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);

            allControlsTopToBottom.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;

            {
                FlowLayoutWidget allControlsLeftToRight = new FlowLayoutWidget();

                FlowLayoutWidget xYZWithDistance = new FlowLayoutWidget(FlowDirection.TopToBottom);
                {
                    FlowLayoutWidget xYZControls = new FlowLayoutWidget();
                    {
                        GuiWidget xyGrid = CreateXYGridControl(colors, distanceBetweenControls, buttonSeparationDistance);
                        xYZControls.AddChild(xyGrid);

                        FlowLayoutWidget zButtons = CreateZButtons(XYZColors.zColor, buttonSeparationDistance, out zPlusControl, out zMinusControl);
                        zButtons.VAnchor = Agg.UI.VAnchor.ParentBottom;
                        xYZControls.AddChild(zButtons);
                        xYZWithDistance.AddChild(xYZControls);
                    }

                    // add in some movement radio buttons
                    FlowLayoutWidget setMoveDistanceControl = new FlowLayoutWidget();
                    TextWidget buttonsLabel = new TextWidget("Distance:", textColor: RGBA_Bytes.White);
                    buttonsLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
                    //setMoveDistanceControl.AddChild(buttonsLabel);

                    {
                        TextImageButtonFactory buttonFactory = new TextImageButtonFactory();
                        buttonFactory.FixedHeight = 20;
                        buttonFactory.FixedWidth = 30;
                        buttonFactory.fontSize = 10;
                        buttonFactory.Margin = new BorderDouble(0);

                        FlowLayoutWidget moveRadioButtons = new FlowLayoutWidget();
                        
                        RadioButton pointOneButton = buttonFactory.GenerateRadioButton(".1");
                        pointOneButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
                        pointOneButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(.1); };
                        moveRadioButtons.AddChild(pointOneButton);
                        
                        RadioButton oneButton = buttonFactory.GenerateRadioButton("1");
                        oneButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
                        oneButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(1); };
                        moveRadioButtons.AddChild(oneButton);

                        RadioButton tenButton = buttonFactory.GenerateRadioButton("10");
                        tenButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
                        tenButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(10); };
                        moveRadioButtons.AddChild(tenButton);

                        RadioButton oneHundredButton = buttonFactory.GenerateRadioButton("100");
                        oneHundredButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
                        oneHundredButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetXYZMoveAmount(100); };
                        moveRadioButtons.AddChild(oneHundredButton);

                        tenButton.Checked = true;
                        moveRadioButtons.Margin = new BorderDouble(0,3);
                        setMoveDistanceControl.AddChild(moveRadioButtons);
                    }

					TextWidget mmLabel = new TextWidget("mm", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize:10);
                    mmLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
                    setMoveDistanceControl.AddChild(mmLabel);
                    setMoveDistanceControl.HAnchor = Agg.UI.HAnchor.ParentLeft;
                    xYZWithDistance.AddChild(setMoveDistanceControl);
                }

                allControlsLeftToRight.AddChild(xYZWithDistance);

                GuiWidget barBetweenZAndE = new GuiWidget(2, 2);
                barBetweenZAndE.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
                barBetweenZAndE.BackgroundColor = RGBA_Bytes.White;
                barBetweenZAndE.Margin = new BorderDouble(distanceBetweenControls, 5);
                allControlsLeftToRight.AddChild(barBetweenZAndE);

                moveButtonFactory.normalFillColor = XYZColors.eColor;

                FlowLayoutWidget eButtons = CreateEButtons(buttonSeparationDistance);
                eButtons.VAnchor |= Agg.UI.VAnchor.ParentTop;
                allControlsLeftToRight.AddChild(eButtons);

                allControlsTopToBottom.AddChild(allControlsLeftToRight);
            }

            this.AddChild(allControlsTopToBottom);
            HAnchor = HAnchor.FitToChildren;
            VAnchor = VAnchor.FitToChildren;

            Margin = new BorderDouble(3);

            this.HAnchor |= HAnchor.ParentLeftRight;
        }

        private void SetEMoveAmount(int moveAmount)
        {
            ePlusControl.MoveAmount = moveAmount;
            eMinusControl.MoveAmount = -moveAmount;
        }

        private void SetXYZMoveAmount(double moveAmount)
        {
            xPlusControl.MoveAmount = moveAmount;
            xMinusControl.MoveAmount = -moveAmount;

            yPlusControl.MoveAmount = moveAmount;
            yMinusControl.MoveAmount = -moveAmount;

            zPlusControl.MoveAmount = moveAmount;
            zMinusControl.MoveAmount = -moveAmount;
        }

        private FlowLayoutWidget CreateEButtons(double buttonSeparationDistance)
        {
            FlowLayoutWidget eButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
            {
                FlowLayoutWidget eMinusButtonAndText = new FlowLayoutWidget();
                eMinusControl = moveButtonFactory.Generate("E-", PrinterConnectionAndCommunication.Axis.E, ManualPrinterControls.EFeedRate(0));
                eMinusControl.Margin = new BorderDouble(0, 0, 5, 0);
                eMinusButtonAndText.AddChild(eMinusControl);
				TextWidget eMinusControlLabel = new TextWidget(LocalizedString.Get("Retract"), pointSize: 11);
                eMinusControlLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                eMinusControlLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
                eMinusButtonAndText.AddChild(eMinusControlLabel);
                eMinusButtonAndText.HAnchor = Agg.UI.HAnchor.ParentLeft;
                eButtons.AddChild(eMinusButtonAndText);

                eMinusButtonAndText.HAnchor = HAnchor.FitToChildren;
                eMinusButtonAndText.VAnchor = VAnchor.FitToChildren;

                GuiWidget eSpacer = new GuiWidget(2, buttonSeparationDistance);
                eSpacer.HAnchor = Agg.UI.HAnchor.ParentLeft;
                eSpacer.Margin = new BorderDouble(eMinusControl.Width / 2, 0, 0, 0);
                eSpacer.BackgroundColor = XYZColors.eColor;
                eButtons.AddChild(eSpacer);

                FlowLayoutWidget ePlusButtonAndText = new FlowLayoutWidget();
                ePlusControl = moveButtonFactory.Generate("E+", PrinterConnectionAndCommunication.Axis.E, ManualPrinterControls.EFeedRate(0));
                ePlusControl.Margin = new BorderDouble(0, 0, 5, 0);
                ePlusButtonAndText.AddChild(ePlusControl);
				TextWidget ePlusControlLabel = new TextWidget(LocalizedString.Get("Extrude"), pointSize: 11);
                ePlusControlLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                ePlusControlLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
                ePlusButtonAndText.AddChild(ePlusControlLabel);
                ePlusButtonAndText.HAnchor = Agg.UI.HAnchor.ParentLeft;
                eButtons.AddChild(ePlusButtonAndText);
                ePlusButtonAndText.HAnchor = HAnchor.FitToChildren;
                ePlusButtonAndText.VAnchor = VAnchor.FitToChildren;
            }

            eButtons.AddChild(new GuiWidget(10, 6));

            // add in some movement radio buttons
            FlowLayoutWidget setMoveDistanceControl = new FlowLayoutWidget();
            TextWidget buttonsLabel = new TextWidget("Distance:", textColor: RGBA_Bytes.White);
            buttonsLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
            //setMoveDistanceControl.AddChild(buttonsLabel);

            {
                TextImageButtonFactory buttonFactory = new TextImageButtonFactory();
                buttonFactory.FixedHeight = 20;
                buttonFactory.FixedWidth = 30;
                buttonFactory.fontSize = 10;
                buttonFactory.Margin = new BorderDouble(0);

                FlowLayoutWidget moveRadioButtons = new FlowLayoutWidget();
                RadioButton oneButton = buttonFactory.GenerateRadioButton("1");
                oneButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
                oneButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetEMoveAmount(1); };
                moveRadioButtons.AddChild(oneButton);
                RadioButton tenButton = buttonFactory.GenerateRadioButton("10");
                tenButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
                tenButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetEMoveAmount(10); };
                moveRadioButtons.AddChild(tenButton);
                RadioButton oneHundredButton = buttonFactory.GenerateRadioButton("100");
                oneHundredButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
                oneHundredButton.CheckedStateChanged += (sender, e) => { if (((RadioButton)sender).Checked) SetEMoveAmount(100); };
                moveRadioButtons.AddChild(oneHundredButton);
                tenButton.Checked = true;
                moveRadioButtons.Margin = new BorderDouble(0,3);
                setMoveDistanceControl.AddChild(moveRadioButtons);
            }

			TextWidget mmLabel = new TextWidget("mm", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize:10);
            mmLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
            setMoveDistanceControl.AddChild(mmLabel);
            setMoveDistanceControl.HAnchor = Agg.UI.HAnchor.ParentLeft;
            eButtons.AddChild(setMoveDistanceControl);

            eButtons.HAnchor = HAnchor.Max_FitToChildren_ParentWidth;
            eButtons.VAnchor = VAnchor.FitToChildren | VAnchor.ParentBottom;

            return eButtons;
        }

        public static FlowLayoutWidget CreateZButtons(RGBA_Bytes color, double buttonSeparationDistance,
            out MoveButton zPlusControl, out MoveButton zMinusControl)
        {
            FlowLayoutWidget zButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
            {
                MoveButtonFactory moveButtonFactory = new MoveButtonFactory();
                moveButtonFactory.normalFillColor = color;
                zPlusControl = moveButtonFactory.Generate("Z+", PrinterConnectionAndCommunication.Axis.Z, ManualPrinterControls.ZSpeed);
                zButtons.AddChild(zPlusControl);

                GuiWidget spacer = new GuiWidget(2, buttonSeparationDistance);
                spacer.HAnchor = Agg.UI.HAnchor.ParentCenter;
                spacer.BackgroundColor = XYZColors.zColor;
                zButtons.AddChild(spacer);

                zMinusControl = moveButtonFactory.Generate("Z-", PrinterConnectionAndCommunication.Axis.Z, ManualPrinterControls.ZSpeed);
                zButtons.AddChild(zMinusControl);
            }
            zButtons.Margin = new BorderDouble(0, 5);
            return zButtons;
        }

        private GuiWidget CreateXYGridControl(XYZColors colors, double distanceBetweenControls, double buttonSeparationDistance)
        {
            GuiWidget xyGrid = new GuiWidget();
            {
                FlowLayoutWidget xButtons = new FlowLayoutWidget();
                {
                    moveButtonFactory.normalFillColor = XYZColors.xColor;
                    xButtons.HAnchor |= Agg.UI.HAnchor.ParentCenter;
                    xButtons.VAnchor |= Agg.UI.VAnchor.ParentCenter;
                    xMinusControl = moveButtonFactory.Generate("X-", PrinterConnectionAndCommunication.Axis.X, ManualPrinterControls.XSpeed);
                    xButtons.AddChild(xMinusControl);

                    GuiWidget spacer = new GuiWidget(xMinusControl.Width + buttonSeparationDistance * 2, 2);
                    spacer.VAnchor = Agg.UI.VAnchor.ParentCenter;
                    spacer.BackgroundColor = XYZColors.xColor;
                    xButtons.AddChild(spacer);

                    xPlusControl = moveButtonFactory.Generate("X+", PrinterConnectionAndCommunication.Axis.X, ManualPrinterControls.XSpeed);
                    xButtons.AddChild(xPlusControl);
                }
                xyGrid.AddChild(xButtons);

                FlowLayoutWidget yButtons = new FlowLayoutWidget(FlowDirection.TopToBottom);
                {
                    moveButtonFactory.normalFillColor = XYZColors.yColor;
                    yButtons.HAnchor |= Agg.UI.HAnchor.ParentCenter;
                    yButtons.VAnchor |= Agg.UI.VAnchor.ParentCenter;
                    yPlusControl = moveButtonFactory.Generate("Y+", PrinterConnectionAndCommunication.Axis.Y, ManualPrinterControls.YSpeed);
                    yButtons.AddChild(yPlusControl);

                    GuiWidget spacer = new GuiWidget(2, buttonSeparationDistance);
                    spacer.HAnchor = Agg.UI.HAnchor.ParentCenter;
                    spacer.BackgroundColor = XYZColors.yColor;
                    yButtons.AddChild(spacer);

                    yMinusControl = moveButtonFactory.Generate("Y-", PrinterConnectionAndCommunication.Axis.Y, ManualPrinterControls.YSpeed);
                    yButtons.AddChild(yMinusControl);
                }
                xyGrid.AddChild(yButtons);
            }
            xyGrid.HAnchor = HAnchor.FitToChildren;
            xyGrid.VAnchor = VAnchor.FitToChildren;
            xyGrid.VAnchor = Agg.UI.VAnchor.ParentBottom;
            xyGrid.Margin = new BorderDouble(0, 5, distanceBetweenControls, 5);
            return xyGrid;
        }

        public class MoveButton : Button
        {
            PrinterConnectionAndCommunication.Axis moveAxis;

            //Amounts in millimeters
            public double MoveAmount = 10;
            private double movementFeedRate;

            public MoveButton(double x, double y, GuiWidget buttonView, PrinterConnectionAndCommunication.Axis axis, double movementFeedRate)
                : base(x, y, buttonView)
            {
                this.moveAxis = axis;
                this.movementFeedRate = movementFeedRate;

                this.Click += new ButtonBase.ButtonEventHandler(moveAxis_Click);
            }

            void moveAxis_Click(object sender, MouseEventArgs mouseEvent)
            {
                MoveButton moveButton = (MoveButton)sender;

                //Add more fancy movement here
                PrinterConnectionAndCommunication.Instance.MoveRelative(this.moveAxis, this.MoveAmount, movementFeedRate);
            }
        }

        public class MoveButtonWidget : GuiWidget
        {
            protected int fontSize = 12;
            protected double borderWidth = 0;
            protected double borderRadius = 0;

            public MoveButtonWidget(string label, RGBA_Bytes fillColor, RGBA_Bytes textColor)
                : base()
            {              
                
                this.BackgroundColor = fillColor;
                this.Margin = new BorderDouble(0);
                this.Padding = new BorderDouble(0);

                if (label != "")
                {
                    TextWidget textWidget = new TextWidget(label, pointSize: fontSize);
                    textWidget.VAnchor = VAnchor.ParentCenter;
                    textWidget.HAnchor = HAnchor.ParentCenter;
                    textWidget.TextColor = textColor;
                    textWidget.Padding = new BorderDouble(3, 0);
                    this.AddChild(textWidget);
                }
                this.Height = 40;
                this.Width = 40;
            }
        }

        public class MoveButtonFactory
        {
            public BorderDouble Padding;
            public BorderDouble Margin;
            public RGBA_Bytes normalFillColor = RGBA_Bytes.White;
            public RGBA_Bytes hoverFillColor = new RGBA_Bytes(0, 0, 0, 50);
            public RGBA_Bytes pressedFillColor = new RGBA_Bytes(0, 0, 0, 0);
            public RGBA_Bytes disabledFillColor = new RGBA_Bytes(255, 255, 255, 50);
            public RGBA_Bytes normalBorderColor = new RGBA_Bytes(255, 255, 255, 0);
            public RGBA_Bytes hoverBorderColor = new RGBA_Bytes(0, 0, 0, 0);
            public RGBA_Bytes pressedBorderColor = new RGBA_Bytes(0, 0, 0, 0);
            public RGBA_Bytes disabledBorderColor = new RGBA_Bytes(0, 0, 0, 0);
            public RGBA_Bytes normalTextColor = RGBA_Bytes.Black;
            public RGBA_Bytes hoverTextColor = RGBA_Bytes.White;
            public RGBA_Bytes pressedTextColor = RGBA_Bytes.White;
            public RGBA_Bytes disabledTextColor = RGBA_Bytes.White;

            public MoveButton Generate(string label, PrinterConnectionAndCommunication.Axis axis, double movementFeedRate)
            {
                //Create button based on view container widget
                ButtonViewStates buttonViewWidget = getButtonView(label);
                MoveButton textImageButton = new MoveButton(0, 0, buttonViewWidget, axis, movementFeedRate);
                textImageButton.Margin = new BorderDouble(0);
                textImageButton.Padding = new BorderDouble(0);
                return textImageButton;
            }

            private ButtonViewStates getButtonView(string label)
            {                
                //Create the multi-state button view
                ButtonViewStates buttonViewWidget = new ButtonViewStates(
                    new MoveButtonWidget(label, normalFillColor, normalTextColor),
                    new MoveButtonWidget(label, hoverFillColor, hoverTextColor),
                    new MoveButtonWidget(label, pressedFillColor, pressedTextColor),
                    new MoveButtonWidget(label, disabledFillColor, disabledTextColor)
                );
                return buttonViewWidget;
            }
        }
    }
}

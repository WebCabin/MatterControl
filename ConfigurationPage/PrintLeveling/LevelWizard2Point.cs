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
using System.Linq;
using System.Text;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Font;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
    public class LevelWizard2Point : LevelWizardBase
    {
        static Vector2 probeFrontLeft = new Vector2(0, 0);
        static Vector2 probeFrontRight = new Vector2(220, 0);
        static Vector2 probeBackLeft = new Vector2(0, 210);
        static double probeStartZHeight = 10;

        string pageOneStepText = "Print Leveling Overview".Localize();
        string pageOneInstructionsTextOne = LocalizedString.Get("Welcome to the print leveling wizard. Here is a quick overview on what we are going to do.");
        string pageOneInstructionsTextTwo = LocalizedString.Get("'Home' the printer");
        string pageOneInstructionsTextThree = LocalizedString.Get("Sample the bed at two points");
        string pageOneInstructionsTextFour = LocalizedString.Get("Turn auto leveling on");
        string pageOneInstructionsText5 = LocalizedString.Get("You should be done in about 2 minutes.");
        string pageOneInstructionsText6 = LocalizedString.Get("Note: Be sure the tip of the extrude is clean.");
        string pageOneInstructionsText7 = LocalizedString.Get("Click 'Next' to continue.");

        public LevelWizard2Point(LevelWizardBase.RuningState runningState)
            : base(500, 370, 6)
        {
            string printLevelWizardTitle = LocalizedString.Get("MatterControl");
            string printLevelWizardTitleFull = LocalizedString.Get("Print Leveling Wizard");
            Title = string.Format("{0} - {1}", printLevelWizardTitle, printLevelWizardTitleFull);
            ProbePosition[] probePositions = new ProbePosition[5];
            probePositions[0] = new ProbePosition();
            probePositions[1] = new ProbePosition();
            probePositions[2] = new ProbePosition();
            probePositions[3] = new ProbePosition();
            probePositions[4] = new ProbePosition();

            printLevelWizard = new WizardControl();
            AddChild(printLevelWizard);

            if (runningState == LevelWizardBase.RuningState.InitialStartupCalibration)
            {
                string requiredPageInstructions = "{0}\n\n{1}".FormatWith(requiredPageInstructions1, requiredPageInstructions2);
                printLevelWizard.AddPage(new FirstPageInstructions(initialPrinterSetupStepText, requiredPageInstructions));
            }

            string pageOneInstructions = string.Format("{0}\n\n\t• {1}\n\t• {2}\n\t• {3}\n\n{4}\n\n{5}\n\n{6}", pageOneInstructionsTextOne, pageOneInstructionsTextTwo, pageOneInstructionsTextThree, pageOneInstructionsTextFour, pageOneInstructionsText5, pageOneInstructionsText6, pageOneInstructionsText7);
            printLevelWizard.AddPage(new FirstPageInstructions(pageOneStepText, pageOneInstructions));

            string homingPageInstructions = string.Format("{0}:\n\n\t• {1}\n\n{2}", homingPageInstructionsTextOne, homingPageInstructionsTextTwo, homingPageInstructionsTextThree);
            printLevelWizard.AddPage(new HomePrinterPage(homingPageStepText, homingPageInstructions));

            string positionLabelTwo = LocalizedString.Get("Position");
            string lowPrecisionTwoLabel = LocalizedString.Get("Low Precision");
            string medPrecisionTwoLabel = LocalizedString.Get("Medium Precision");
            string highPrecisionTwoLabel = LocalizedString.Get("High Precision");
            printLevelWizard.AddPage(new GetCoarseBedHeightProbeFirst(printLevelWizard, new Vector3(probeFrontLeft, probeStartZHeight), string.Format("{0} {1} 2 - {2}", GetStepString(), positionLabelTwo, lowPrecisionTwoLabel), probePositions[0], probePositions[1], true));
            printLevelWizard.AddPage(new GetFineBedHeight(string.Format("{0} {1} 2 - {2}", GetStepString(), positionLabelTwo, medPrecisionTwoLabel), probePositions[0], true));
            printLevelWizard.AddPage(new GetUltraFineBedHeight(string.Format("{0} {1} 2 - {2}", GetStepString(), positionLabelTwo, highPrecisionTwoLabel), probePositions[0], true));

            string positionLabelThree = LocalizedString.Get("Position");
            string lowPrecisionLabelThree = LocalizedString.Get("Low Precision");
            string medPrecisionLabelThree = LocalizedString.Get("Medium Precision");
            string highPrecisionLabelThree = LocalizedString.Get("High Precision");
            printLevelWizard.AddPage(new GetCoarseBedHeightProbeFirst(printLevelWizard, new Vector3(probeFrontRight, probeStartZHeight), string.Format("{0} {1} 3 - {2}", GetStepString(), positionLabelThree, lowPrecisionLabelThree), probePositions[2], probePositions[3], true));
            printLevelWizard.AddPage(new GetFineBedHeight(string.Format("{0} {1} 3 - {2}", GetStepString(), positionLabelThree, medPrecisionLabelThree), probePositions[2], true));
            printLevelWizard.AddPage(new GetUltraFineBedHeight(string.Format("{0} {1} 3 - {2}", GetStepString(), positionLabelThree, highPrecisionLabelThree), probePositions[2], true));

            string retrievingFinalPosition = "Getting the third point.";
            printLevelWizard.AddPage(new GettingThirdPointFor2PointCalibration(printLevelWizard, "Collecting Data", new Vector3(probeBackLeft, probeStartZHeight), retrievingFinalPosition, probePositions[4]));

            string doneInstructions = string.Format("{0}\n\n\t• {1}\n\n{2}", doneInstructionsText, doneInstructionsTextTwo, doneInstructionsTextThree);
            printLevelWizard.AddPage(new LastPage2PointInstructions("Done".Localize(), doneInstructions, probePositions));
        }

        static event EventHandler unregisterEvents;
        static int probeIndex = 0;
        static Vector3 probeRead0;
        static Vector3 probeRead1;
        static Vector3 probeRead2;
        public static List<string> ProcessCommand(string lineBeingSent)
        {
            int commentIndex = lineBeingSent.IndexOf(';');
            if (commentIndex > 0) // there is content in front of the ;
            {
                lineBeingSent = lineBeingSent.Substring(0, commentIndex).Trim();
            }
            List<string> lines = new List<string>();
            if (lineBeingSent == "G28")
            {
                lines.Add("G28 X0");
                lines.Add("G1 X1");
                lines.Add("G28 Y0");
                lines.Add("G1 Y1");
                lines.Add("G28 Z0");
                lines.Add("M114");
            }
            else if (lineBeingSent == "G29")
            {
                // first make sure we don't have any leftover reading.
                PrinterConnectionAndCommunication.Instance.ReadLine.UnregisterEvent(FinishedProbe, ref unregisterEvents);

                if (unregisterEvents != null)
                {
                    unregisterEvents(null, null);
                }
                if (PrinterConnectionAndCommunication.Instance.CommunicationState == PrinterConnectionAndCommunication.CommunicationStates.Printing)
                {
                    ActivePrinterProfile.Instance.DoPrintLeveling = false;
                }

                probeIndex = 0;
                PrinterConnectionAndCommunication.Instance.ReadLine.RegisterEvent(FinishedProbe, ref unregisterEvents);

                StringBuilder commands = new StringBuilder();

                double zFeedRate = InstructionsPage.ManualControlsFeedRate().z;
                double xyFeedRate = InstructionsPage.ManualControlsFeedRate().x;

                // make sure the probe offset is set to 0
                lines.Add("M565 Z0");

                // probe position 0
                probeRead0 = new Vector3(probeFrontLeft, probeStartZHeight);
                // up in z
                lines.Add("G1 F{0}".FormatWith(zFeedRate));
                lines.Add("G1 {0}{1}".FormatWith("Z", probeStartZHeight));
                // move to xy
                lines.Add("G1 F{0}".FormatWith(xyFeedRate));
                lines.Add("G1 X{0}Y{1}Z{2}".FormatWith(probeFrontLeft.x, probeFrontLeft.y, probeStartZHeight));
                // probe
                lines.Add("G30");

                // probe position 1
                probeRead1 = new Vector3(probeFrontRight, probeStartZHeight);
                // up in z
                lines.Add("G1 F{0}".FormatWith(zFeedRate));
                lines.Add("G1 {0}{1}".FormatWith("Z", probeStartZHeight));
                // move to xy
                lines.Add("G1 F{0}".FormatWith(xyFeedRate));
                lines.Add("G1 X{0}Y{1}Z{2}".FormatWith(probeFrontRight.x, probeFrontRight.y, probeStartZHeight));
                // probe
                lines.Add("G30");

                // probe position 2
                probeRead2 = new Vector3(probeBackLeft, probeStartZHeight);
                // up in z
                lines.Add("G1 F{0}".FormatWith(zFeedRate));
                lines.Add("G1 {0}{1}".FormatWith("Z", probeStartZHeight));
                // move to xy
                lines.Add("G1 F{0}".FormatWith(xyFeedRate));
                lines.Add("G1 X{0}Y{1}Z{2}".FormatWith(probeBackLeft.x, probeBackLeft.y, probeStartZHeight));
                // probe
                lines.Add("G30");
                lines.Add("M114");
                lines.Add("G1 Z1 F300");
            }
            else
            {
                lines.Add(lineBeingSent);
            }

            return lines;
        }

        static void FinishedProbe(object sender, EventArgs e)
        {
            StringEventArgs currentEvent = e as StringEventArgs;
            if (currentEvent != null)
            {
                if (currentEvent.Data.Contains("endstops hit"))
                {
                    int zStringPos = currentEvent.Data.LastIndexOf("Z:");
                    if (zStringPos != -1)
                    {
                        string zProbeHeight = currentEvent.Data.Substring(zStringPos + 2);
                        // store the position that the limit swich fires
                        switch (probeIndex++)
                        {
                            case 0:
                                probeRead0.z = double.Parse(zProbeHeight);
                                break;

                            case 1:
                                probeRead1.z = double.Parse(zProbeHeight);
                                break;

                            case 2:
                                probeRead2.z = double.Parse(zProbeHeight);
                                if (unregisterEvents != null)
                                {
                                    unregisterEvents(null, null);
                                }
                                SetEquations();
                                break;
                        }
                    }
                }
            }
        }

        static void SetEquations()
        {
            PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(ActivePrinterProfile.Instance.ActivePrinter);

            // position 0 does not change as it is the distance from the switch trigger to the extruder tip.
            //levelingData.sampledPosition0 = levelingData.sampledPosition0;
            levelingData.sampledPosition1 = levelingData.sampledPosition0 + probeRead1;
            levelingData.sampledPosition2 = levelingData.sampledPosition0 + probeRead2;

            ActivePrinterProfile.Instance.DoPrintLeveling = true;
        }
    }
}

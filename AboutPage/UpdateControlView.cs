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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.VersionManagement;

namespace MatterHackers.MatterControl
{
    public class UpdateControlView : FlowLayoutWidget
    {
        Button downloadUpdateLink;
        Button checkUpdateLink;
        Button installUpdateLink;
        TextWidget updateStatusText;

        event EventHandler unregisterEvents;
        RGBA_Bytes offWhite = new RGBA_Bytes(245, 245, 245);
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        public UpdateControlView()
        {
            textImageButtonFactory.normalFillColor = RGBA_Bytes.Gray;
            textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;

            HAnchor = HAnchor.ParentLeftRight;
            BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
            Padding = new BorderDouble(6, 5);
            {
                updateStatusText = new TextWidget(string.Format(""), textColor: ActiveTheme.Instance.PrimaryTextColor);
                updateStatusText.AutoExpandBoundsToText = true;
                updateStatusText.VAnchor = VAnchor.ParentCenter;

                GuiWidget horizontalSpacer = new GuiWidget();
                horizontalSpacer.HAnchor = HAnchor.ParentLeftRight;

                checkUpdateLink = textImageButtonFactory.Generate("Check for Update".Localize());
                checkUpdateLink.VAnchor = VAnchor.ParentCenter;
                checkUpdateLink.Click += CheckForUpdate;
                checkUpdateLink.Visible = false;

                downloadUpdateLink = textImageButtonFactory.Generate("Download Update".Localize());
                downloadUpdateLink.VAnchor = VAnchor.ParentCenter;
                downloadUpdateLink.Click += DownloadUpdate;
                downloadUpdateLink.Visible = false;

                installUpdateLink = textImageButtonFactory.Generate("Install Update".Localize());
                installUpdateLink.VAnchor = VAnchor.ParentCenter;
                installUpdateLink.Click += InstallUpdate;
                installUpdateLink.Visible = false;

                AddChild(updateStatusText);
                AddChild(horizontalSpacer);
                AddChild(checkUpdateLink);
                AddChild(downloadUpdateLink);
                AddChild(installUpdateLink);
            }

            UpdateControlData.Instance.UpdateStatusChanged.RegisterEvent(UpdateStatusChanged, ref unregisterEvents);

            MinimumSize = new VectorMath.Vector2(0, 50);

            UpdateStatusChanged(null, null);
        }

        public void CheckForUpdate(object sender, MouseEventArgs e)
        {
            UpdateControlData.Instance.CheckForUpdateUserRequested();
        }

        public void InstallUpdate(object sender, MouseEventArgs e)
        {
            try
            {
                if (!UpdateControlData.Instance.InstallUpdate(this))
                {
                    installUpdateLink.Visible = false;
                    updateStatusText.Text = string.Format("Oops! Unable to install update.".Localize());
                }
            }
            catch
            {
                installUpdateLink.Visible = false;
                updateStatusText.Text = string.Format("Oops! Unable to install update.".Localize());
            }
        }


        public void DownloadUpdate(object sender, MouseEventArgs e)
        {
            downloadUpdateLink.Visible = false;
            updateStatusText.Text = string.Format("Retrieving download info...".Localize());

            UpdateControlData.Instance.InitiateUpdateDownload();
        }

        void UpdateStatusChanged(object sender, EventArgs e)
        {
            switch (UpdateControlData.Instance.UpdateStatus)
            {
                case UpdateControlData.UpdateStatusStates.MayBeAvailable:
                    updateStatusText.Text = string.Format("New updates may be available.".Localize());
                    checkUpdateLink.Visible = true;
                    break;

                case UpdateControlData.UpdateStatusStates.CheckingForUpdate:
                    updateStatusText.Text = "Checking for updates...".Localize();
                    checkUpdateLink.Visible = false;
                    break;

                case UpdateControlData.UpdateStatusStates.UpdateAvailable:
                    updateStatusText.Text = string.Format("There is a recommended update available.".Localize());
                    downloadUpdateLink.Visible = true;
                    installUpdateLink.Visible = false;
                    checkUpdateLink.Visible = false;
                    break;

                case UpdateControlData.UpdateStatusStates.UpdateDownloading:
                    string newText = "Downloading updates...".Localize();
                    newText = "{0} {1}%".FormatWith(newText, UpdateControlData.Instance.DownloadPercent);
                    updateStatusText.Text = newText;
                    break;

                case UpdateControlData.UpdateStatusStates.ReadyToInstall:
                    updateStatusText.Text = string.Format("New updates are ready to install.".Localize());
                    downloadUpdateLink.Visible = false;
                    installUpdateLink.Visible = true;
                    checkUpdateLink.Visible = false;
                    break;

                case UpdateControlData.UpdateStatusStates.UpToDate:
                    updateStatusText.Text = string.Format("Your application is up-to-date.".Localize());
                    downloadUpdateLink.Visible = false;
                    installUpdateLink.Visible = false;
                    checkUpdateLink.Visible = true;
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}

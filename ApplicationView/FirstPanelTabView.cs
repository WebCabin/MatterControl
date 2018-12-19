﻿/*
Copyright (c) 2014, Kevin Pope
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

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;
using MatterHackers.VectorMath;

using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintHistory;

namespace MatterHackers.MatterControl
{
    class FirstPanelTabView : TabControl
    {
        public static int firstPanelCurrentTab = 0;

        TabPage QueueTabPage;
        TabPage LibraryTabPage;
        TabPage HistoryTabPage;
        TabPage AboutTabPage;
        SimpleTextTabWidget AboutTabView;
        RGBA_Bytes unselectedTextColor = ActiveTheme.Instance.TabLabelUnselected;
        GuiWidget addedUpdateMark = null;
        QueueDataView queueDataView;
        event EventHandler unregisterEvents;

        public FirstPanelTabView(QueueDataView queueDataView)
        {
            this.queueDataView = queueDataView;
            this.TabBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            this.TabBar.BorderColor = new RGBA_Bytes(0, 0, 0, 0);
            this.TabBar.Margin = new BorderDouble(0, 0);
            this.TabBar.Padding = new BorderDouble(0, 2);

            this.Margin = new BorderDouble(top: 4);

            QueueTabPage = new TabPage(new QueueBottomToolbar(queueDataView), LocalizedString.Get("Queue").ToUpper());
            this.AddTab(new SimpleTextTabWidget(QueueTabPage, "Queue Tab", 15,
                    ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            LibraryTabPage = new TabPage(new PrintLibraryWidget(), LocalizedString.Get("Library").ToUpper());
            this.AddTab(new SimpleTextTabWidget(LibraryTabPage, "Library Tab", 15,
                    ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            HistoryTabPage = new TabPage(new PrintHistoryWidget(), LocalizedString.Get("History").ToUpper());
            this.AddTab(new SimpleTextTabWidget(HistoryTabPage, "History Tab", 15,
                    ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes()));

            AboutTabPage = new TabPage(new AboutPage(), LocalizedString.Get("About").ToUpper());
            AboutTabView = new SimpleTextTabWidget(AboutTabPage, "About Tab", 15,
                        ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), unselectedTextColor, new RGBA_Bytes());
            this.AddTab(AboutTabView);

            NumQueueItemsChanged(this, null);
            SetUpdateNotification(this, null);

            QueueData.Instance.ItemAdded.RegisterEvent(NumQueueItemsChanged, ref unregisterEvents);
            QueueData.Instance.ItemRemoved.RegisterEvent(NumQueueItemsChanged, ref unregisterEvents);
            UpdateControlData.Instance.UpdateStatusChanged.RegisterEvent(SetUpdateNotification, ref unregisterEvents);

            WidescreenPanel.PreChangePannels.RegisterEvent(SaveCurrentTab, ref unregisterEvents);

            SelectedTabIndex = firstPanelCurrentTab;
        }

        void NumQueueItemsChanged(object sender, EventArgs widgetEvent)
        {
            string queueStringBeg = LocalizedString.Get("Queue").ToUpper();
            string queueString = string.Format("{1} ({0})", QueueData.Instance.Count, queueStringBeg);
            QueueTabPage.Text = string.Format(queueString, QueueData.Instance.Count);
        }

        void SaveCurrentTab(object sender, EventArgs e)
        {
            firstPanelCurrentTab = SelectedTabIndex;
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
        }

        public void SetUpdateNotification(object sender, EventArgs widgetEvent)
        {
            switch (UpdateControlData.Instance.UpdateStatus)
            {
                case UpdateControlData.UpdateStatusStates.MayBeAvailable:
                case UpdateControlData.UpdateStatusStates.ReadyToInstall:
                case UpdateControlData.UpdateStatusStates.UpdateAvailable:
                case UpdateControlData.UpdateStatusStates.UpdateDownloading:
                    if (addedUpdateMark == null)
                    {
                        addedUpdateMark = new NotificationWidget();
                        addedUpdateMark.OriginRelativeParent = new Vector2(AboutTabView.Width - 25, 7);
                        AboutTabView.AddChild(addedUpdateMark);
                    }
                    addedUpdateMark.Visible = true;
                    break;

                case UpdateControlData.UpdateStatusStates.UpToDate:
                case UpdateControlData.UpdateStatusStates.CheckingForUpdate:
                    if (addedUpdateMark != null)
                    {
                        addedUpdateMark.Visible = false;
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}

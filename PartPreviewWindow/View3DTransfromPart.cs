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
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations; //Added Namespace
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public class Cover : GuiWidget
    {
        public Cover(HAnchor hAnchor = HAnchor.None, VAnchor vAnchor = VAnchor.None)
            : base(hAnchor, vAnchor)
        {
        }
    }

    public class View3DTransformPart : PartPreview3DWidget
    {
        public WindowType windowType { get; set; }

        FlowLayoutWidget viewOptionContainer;
        FlowLayoutWidget rotateOptionContainer;
        FlowLayoutWidget scaleOptionContainer;

        List<string> pendingPartsToLoad = new List<string>();

        ProgressControl processingProgressControl;
        FlowLayoutWidget enterEditButtonsContainer;
        FlowLayoutWidget doEdittingButtonsContainer;
        bool OpenAddDialogWhenDone = false;
        
        Dictionary<string, List<GuiWidget>> transformControls = new Dictionary<string, List<GuiWidget>>();

        MHNumberEdit scaleRatioControl;

        CheckBox expandViewOptions;
        CheckBox expandRotateOptions;
        CheckBox expandScaleOptions;

        Button autoArrangeButton;
        FlowLayoutWidget saveButtons;
        Button applyScaleButton;

        PrintItemWrapper printItemWrapper;
		bool saveAsWindowIsOpen = false;
		SaveAsWindow saveAsWindow;

        List<Mesh> asynchMeshesList = new List<Mesh>();
        List<Matrix4X4> asynchMeshTransforms = new List<Matrix4X4>();
        List<PlatingMeshData> asynchPlatingDataList = new List<PlatingMeshData>();

        List<PlatingMeshData> MeshExtraData;
        bool autoRotateEnabled = true;

        public Matrix4X4 SelectedMeshTransform
        {
            get { return meshViewerWidget.SelectedMeshTransform; }
            set { meshViewerWidget.SelectedMeshTransform = value; }
        }

        public Mesh SelectedMesh
        {
            get { return meshViewerWidget.SelectedMesh; }
        }

        public int SelectedMeshIndex
        {
            get { return meshViewerWidget.SelectedMeshIndex; }
            set { meshViewerWidget.SelectedMeshIndex = value; }
        }

        public List<Mesh> Meshes
        {
            get { return meshViewerWidget.Meshes; }
        }

        public List<Matrix4X4> MeshTransforms
        {
            get { return meshViewerWidget.MeshTransforms; }
        }

        internal struct MeshSelectInfo
        {
            internal bool downOnPart;
            internal PlaneShape hitPlane;
            internal Vector3 planeDownHitPos;
            internal Vector3 lastMoveDelta;
        }

        private bool FindMeshHitPosition(Vector2 screenPosition, out int meshHitIndex)
        {
            meshHitIndex = 0;
            if (MeshExtraData.Count == 0 || MeshExtraData[0].traceableData == null)
            {
                return false;
            }

            List<IRayTraceable> mesheTraceables = new List<IRayTraceable>();
            for (int i = 0; i < MeshExtraData.Count; i++)
            {
                mesheTraceables.Add(new Transform(MeshExtraData[i].traceableData, MeshTransforms[i]));
            }
            IRayTraceable allObjects = BoundingVolumeHierarchy.CreateNewHierachy(mesheTraceables);

            Ray ray = meshViewerWidget.TrackballTumbleWidget.LastScreenRay;
            IntersectInfo info = allObjects.GetClosestIntersection(ray);
            if (info != null)
            {
                meshSelectInfo.planeDownHitPos = info.hitPosition;
                meshSelectInfo.lastMoveDelta = new Vector3();

                for (int i = 0; i < MeshExtraData.Count; i++)
                {
                    List<IRayTraceable> insideBounds = new List<IRayTraceable>();
                    MeshExtraData[i].traceableData.GetContained(insideBounds, info.closestHitObject.GetAxisAlignedBoundingBox());
                    if (insideBounds.Contains(info.closestHitObject))
                    {
                        meshHitIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

        Matrix4X4 transformOnMouseDown = Matrix4X4.Identity;
        MeshSelectInfo meshSelectInfo;
        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
            autoRotateEnabled = false;
            base.OnMouseDown(mouseEvent);
            if (meshViewerWidget.TrackballTumbleWidget.UnderMouseState == Agg.UI.UnderMouseState.FirstUnderMouse)
            {
                if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None)
                {
                    viewControls3D.partSelectButton.ClickButton(null);
                    int meshHitIndex;
                    if (FindMeshHitPosition(mouseEvent.Position, out meshHitIndex))
                    {
                        meshSelectInfo.hitPlane = new PlaneShape(Vector3.UnitZ, meshSelectInfo.planeDownHitPos.z, null);
                        SelectedMeshIndex = meshHitIndex;
                        transformOnMouseDown = SelectedMeshTransform;
                        Invalidate();
                        meshSelectInfo.downOnPart = true;

                        SetApplyScaleVisability();
                    }
                }
            }
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            hasDrawn = true;
            base.OnDraw(graphics2D);
        }

        public override void OnMouseMove(MouseEventArgs mouseEvent)
        {
            if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None && meshSelectInfo.downOnPart)
            {
                Ray ray = meshViewerWidget.TrackballTumbleWidget.LastScreenRay;
                IntersectInfo info = meshSelectInfo.hitPlane.GetClosestIntersection(ray);
                if (info != null)
                {
                    Vector3 delta = info.hitPosition - meshSelectInfo.planeDownHitPos;

                    Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(new Vector3(-meshSelectInfo.lastMoveDelta));
                    totalTransfrom *= Matrix4X4.CreateTranslation(new Vector3(delta));
                    meshSelectInfo.lastMoveDelta = delta;
                    SelectedMeshTransform *= totalTransfrom;
                    Invalidate();
                }
            }

            base.OnMouseMove(mouseEvent);
        }

        public override void OnMouseUp(MouseEventArgs mouseEvent)
        {
            if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None
                && meshSelectInfo.downOnPart
                && meshSelectInfo.lastMoveDelta != Vector3.Zero)
            {
                saveButtons.Visible = true;
            }

            meshSelectInfo.downOnPart = false;

            base.OnMouseUp(mouseEvent);
        }

        EventHandler unregisterEvents;

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }

            base.OnClosed(e);
        }

        public enum WindowType { Embeded, StandAlone };
        public enum AutoRotate { Enabled, Disabled };

        public View3DTransformPart(PrintItemWrapper printItemWrapper, Vector3 viewerVolume, MeshViewerWidget.BedShape bedShape, WindowType windowType, AutoRotate autoRotate)
        {
            this.windowType = windowType;
            autoRotateEnabled = (autoRotate == AutoRotate.Enabled);
            MeshExtraData = new List<PlatingMeshData>();
            MeshExtraData.Add(new PlatingMeshData());

            this.printItemWrapper = printItemWrapper;

            FlowLayoutWidget mainContainerTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            mainContainerTopToBottom.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            mainContainerTopToBottom.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;

            FlowLayoutWidget centerPartPreviewAndControls = new FlowLayoutWidget(FlowDirection.LeftToRight);
            centerPartPreviewAndControls.AnchorAll();

            GuiWidget viewArea = new GuiWidget();
            viewArea.AnchorAll();
            {
                meshViewerWidget = new MeshViewerWidget(viewerVolume, 1, bedShape, "Press 'Add' to select an item.".Localize());
                
                // this is to add an image to the bed
                string imagePathAndFile = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "OEMSettings", "bedimage.png");
                if (autoRotateEnabled && File.Exists(imagePathAndFile))
                {
                    ImageBuffer wattermarkImage = new ImageBuffer();
                    ImageIO.LoadImageData(imagePathAndFile, wattermarkImage);

                    ImageBuffer bedImage = meshViewerWidget.BedImage;
                    Graphics2D bedGraphics = bedImage.NewGraphics2D();
                    bedGraphics.Render(wattermarkImage, 
                        new Vector2((bedImage.Width - wattermarkImage.Width) / 2, (bedImage.Height - wattermarkImage.Height)/2));
                }

                meshViewerWidget.AnchorAll();
            }
            viewArea.AddChild(meshViewerWidget);

            centerPartPreviewAndControls.AddChild(viewArea);
            mainContainerTopToBottom.AddChild(centerPartPreviewAndControls);

            FlowLayoutWidget buttonBottomPanel = new FlowLayoutWidget(FlowDirection.LeftToRight);
            buttonBottomPanel.HAnchor = HAnchor.ParentLeftRight;
            buttonBottomPanel.Padding = new BorderDouble(3, 3);
            buttonBottomPanel.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            buttonRightPanel = CreateRightButtonPanel(viewerVolume.y);

            CreateOptionsContent();

            // add in the plater tools
            {
                FlowLayoutWidget editToolBar = new FlowLayoutWidget();

                string progressFindPartsLabel = LocalizedString.Get("Finding Parts");
                string progressFindPartsLabelFull = "{0}:".FormatWith(progressFindPartsLabel);

                processingProgressControl = new ProgressControl(progressFindPartsLabelFull);
                processingProgressControl.VAnchor = Agg.UI.VAnchor.ParentCenter;
                editToolBar.AddChild(processingProgressControl);
                editToolBar.VAnchor |= Agg.UI.VAnchor.ParentCenter;
                processingProgressControl.Visible = false;

                // If the window is embeded (in the center pannel) and there is no item loaded then don't show the add button
                enterEditButtonsContainer = new FlowLayoutWidget();
                {
                    Button addButton = textImageButtonFactory.Generate(LocalizedString.Get("Add"), "icon_circle_plus.png");
                    addButton.Margin = new BorderDouble(right: 10);
                    enterEditButtonsContainer.AddChild(addButton);
                    addButton.Click += (sender, e) =>
                    {
                        UiThread.RunOnIdle((state) =>
                        {
                            EnterEditAndSplitIntoMeshes();
                            OpenAddDialogWhenDone = true;
                        });
                    };

                    Button enterEdittingButton = textImageButtonFactory.Generate(LocalizedString.Get("Edit"));
                    enterEdittingButton.Click += (sender, e) =>
                    {
                        EnterEditAndSplitIntoMeshes();
                    };

                    enterEditButtonsContainer.AddChild(enterEdittingButton);
                }
                editToolBar.AddChild(enterEditButtonsContainer);

                doEdittingButtonsContainer = new FlowLayoutWidget();
                doEdittingButtonsContainer.Visible = false;

                {
                    Button addButton = textImageButtonFactory.Generate(LocalizedString.Get("Add"), "icon_circle_plus.png");
                    addButton.Margin = new BorderDouble(right: 10);
                    doEdittingButtonsContainer.AddChild(addButton);
                    addButton.Click += (sender, e) =>
                    {
                        UiThread.RunOnIdle((state) =>
                        {
                            OpenFileDialogParams openParams = new OpenFileDialogParams("Select an STL file|*.stl", multiSelect: true);

                            FileDialog.OpenFileDialog(ref openParams);
                            LoadAndAddPartsToPlate(openParams.FileNames);
                        });
                    };

                    Button copyButton = textImageButtonFactory.Generate(LocalizedString.Get("Copy"));
                    doEdittingButtonsContainer.AddChild(copyButton);
                    copyButton.Click += (sender, e) =>
                    {
                        MakeCopyOfMesh();
                    };

                    Button deleteButton = textImageButtonFactory.Generate(LocalizedString.Get("Delete"));
                    deleteButton.Margin = new BorderDouble(left: 20);
                    doEdittingButtonsContainer.AddChild(deleteButton);
                    deleteButton.Click += (sender, e) =>
                    {
                        DeleteSelectedMesh();
                    };
                }

                KeyDown += (sender, e) =>
                {
                    KeyEventArgs keyEvent = e as KeyEventArgs;
                    if (keyEvent != null && !keyEvent.Handled)
                    {
                        if (keyEvent.KeyCode == Keys.Delete || keyEvent.KeyCode == Keys.Back)
                        {
                            DeleteSelectedMesh();
                        }

                        if (keyEvent.KeyCode == Keys.Escape)
                        {
                            if (meshSelectInfo.downOnPart)
                            {
                                meshSelectInfo.downOnPart = false;
                                SelectedMeshTransform = transformOnMouseDown;
                                Invalidate();
                            }
                        }
                    }
                };

                editToolBar.AddChild(doEdittingButtonsContainer);
                buttonBottomPanel.AddChild(editToolBar);
            }

            autoArrangeButton.Click += (sender, e) =>
            {
                AutoArangePartsInBackground();
            };

            GuiWidget buttonRightPanelHolder = new GuiWidget(HAnchor.FitToChildren, VAnchor.ParentBottomTop);
            centerPartPreviewAndControls.AddChild(buttonRightPanelHolder);
            buttonRightPanelHolder.AddChild(buttonRightPanel);

            viewControls3D = new ViewControls3D(meshViewerWidget);

            buttonRightPanelDisabledCover = new Cover(HAnchor.ParentLeftRight, VAnchor.ParentBottomTop);
            buttonRightPanelDisabledCover.BackgroundColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryBackgroundColor, 150);
            buttonRightPanelHolder.AddChild(buttonRightPanelDisabledCover);
            LockEditControls();

            GuiWidget leftRightSpacer = new GuiWidget();
            leftRightSpacer.HAnchor = HAnchor.ParentLeftRight;
            buttonBottomPanel.AddChild(leftRightSpacer);

            if (windowType == WindowType.StandAlone)
            {
                Button closeButton = textImageButtonFactory.Generate(LocalizedString.Get("Close"));
                buttonBottomPanel.AddChild(closeButton);
                closeButton.Click += (sender, e) =>
                {
                    CloseOnIdle();
                };
            }

            mainContainerTopToBottom.AddChild(buttonBottomPanel);

            this.AddChild(mainContainerTopToBottom);
            this.AnchorAll();

            meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Rotation;
            AddChild(viewControls3D);
            viewControls3D.PartSelectVisible = false;

            AddHandlers();

            if (printItemWrapper != null)
            {
                // don't load the mesh until we get all the rest of the interface built
                meshViewerWidget.LoadMesh(printItemWrapper.FileLocation);
                meshViewerWidget.LoadDone += new EventHandler(meshViewerWidget_LoadDone);
            }

            UiThread.RunOnIdle(AutoSpin);

            if (printItemWrapper == null && windowType == WindowType.Embeded)
            {
                enterEditButtonsContainer.Visible = false;
            }

            if (windowType == WindowType.Embeded)
            {
                PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(SetEditControlsBasedOnPrinterState, ref unregisterEvents);
            }
            SetEditControlsBasedOnPrinterState(this, null);
        }

        void SetEditControlsBasedOnPrinterState(object sender, EventArgs e)
        {
            if (windowType == WindowType.Embeded)
            {
                switch (PrinterConnectionAndCommunication.Instance.CommunicationState)
                {
                    case PrinterConnectionAndCommunication.CommunicationStates.Printing:
                    case PrinterConnectionAndCommunication.CommunicationStates.Paused:
                        LockEditControls();
                        break;

                    default:
                        UnlockEditControls();
                        break;
                }
            }
            else
            {
                UnlockEditControls();
            }
        }

        bool hasDrawn = false;
        Stopwatch timeSinceLastSpin = new Stopwatch();
        void AutoSpin(object state)
        {
            if (!WidgetHasBeenClosed && autoRotateEnabled)
            {
                // add it back in to keep it running.
                UiThread.RunOnIdle(AutoSpin);

                if ((!timeSinceLastSpin.IsRunning || timeSinceLastSpin.ElapsedMilliseconds > 50)
                    && hasDrawn)
                {
                    hasDrawn = false;
                    timeSinceLastSpin.Restart();

                    Quaternion currentRotation = meshViewerWidget.TrackballTumbleWidget.TrackBallController.CurrentRotation.GetRotation();
                    Quaternion invertedRotation = Quaternion.Invert(currentRotation);

                    Quaternion rotateAboutZ = Quaternion.FromEulerAngles(new Vector3(0, 0, .01));
                    rotateAboutZ = invertedRotation * rotateAboutZ * currentRotation;
                    meshViewerWidget.TrackballTumbleWidget.TrackBallController.Rotate(rotateAboutZ);
                    Invalidate();
                }
            }
        }

        private void MakeCopyOfMesh()
        {
            if (Meshes.Count > 0)
            {
				string makingCopyLabel = LocalizedString.Get("Making Copy");
				string makingCopyLabelFull = string.Format ("{0}:", makingCopyLabel);
				processingProgressControl.textWidget.Text = makingCopyLabelFull;
                processingProgressControl.Visible = true;
                processingProgressControl.PercentComplete = 0;
                LockEditControls();

                BackgroundWorker copyPartBackgroundWorker = null;
                copyPartBackgroundWorker = new BackgroundWorker();
                copyPartBackgroundWorker.WorkerReportsProgress = true;

                copyPartBackgroundWorker.DoWork += new DoWorkEventHandler(copyPartBackgroundWorker_DoWork);
                copyPartBackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(BackgroundWorker_ProgressChanged);
                copyPartBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(copyPartBackgroundWorker_RunWorkerCompleted);

                copyPartBackgroundWorker.RunWorkerAsync();
            }
        }

        void copyPartBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UnlockEditControls();
            PullMeshDataFromAsynchLists();
            saveButtons.Visible = true;
            viewControls3D.partSelectButton.ClickButton(null);

            // now set the selection to the new copy
            MeshExtraData[Meshes.Count - 1].currentScale = MeshExtraData[SelectedMeshIndex].currentScale;
            SelectedMeshIndex = Meshes.Count - 1;
        }

        void copyPartBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            BackgroundWorker backgroundWorker = (BackgroundWorker)sender;

            PushMeshDataToAsynchLists(true);

            Mesh copyMesh = new Mesh();

            int faceCount = asynchMeshesList[SelectedMeshIndex].Faces.Count;
            for (int i = 0; i < faceCount; i++)
            {
                Face face = asynchMeshesList[SelectedMeshIndex].Faces[i];
                List<Vertex> faceVertices = new List<Vertex>();
                foreach (FaceEdge faceEdgeToAdd in face.FaceEdges())
                {
                    Vertex newVertex = copyMesh.CreateVertex(faceEdgeToAdd.firstVertex.Position, true);
                    faceVertices.Add(newVertex);
                }

                int nextPercent = (i + 1) * 80 / faceCount;
                backgroundWorker.ReportProgress(nextPercent);

                copyMesh.CreateFace(faceVertices.ToArray(), true);
            }

            PlatingHelper.FindPositionForPartAndAddToPlate(copyMesh, SelectedMeshTransform, asynchPlatingDataList, asynchMeshesList, asynchMeshTransforms);
            PlatingHelper.CreateITraceableForMesh(asynchPlatingDataList, asynchMeshesList, asynchMeshesList.Count-1);

            backgroundWorker.ReportProgress(95);
        }

        private void AutoArangePartsInBackground()
        {
            if (Meshes.Count > 0)
            {
				string progressArrangeParts = LocalizedString.Get ("Arranging Parts");
				string progressArrangePartsFull = string.Format ("{0}:", progressArrangeParts);
				processingProgressControl.textWidget.Text = progressArrangePartsFull;
                processingProgressControl.Visible = true;
                processingProgressControl.PercentComplete = 0;
                LockEditControls();

                PushMeshDataToAsynchLists(false);

                BackgroundWorker arrangePartsBackgroundWorker = null;
                arrangePartsBackgroundWorker = new BackgroundWorker();
                arrangePartsBackgroundWorker.WorkerReportsProgress = true;

                arrangePartsBackgroundWorker.DoWork += new DoWorkEventHandler(arrangePartsBackgroundWorker_DoWork);
                arrangePartsBackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(BackgroundWorker_ProgressChanged);
                arrangePartsBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(arrangePartsBackgroundWorker_RunWorkerCompleted);

                arrangePartsBackgroundWorker.RunWorkerAsync();
            }
        }

        void arrangePartsBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            if (asynchMeshesList.Count > 0)
            {
                BackgroundWorker backgroundWorker = (BackgroundWorker)sender;

                // move them all out of the way
                foreach (Mesh mesh in asynchMeshesList)
                {
                    mesh.Translate(new Vector3(1000, 1000, 0));
                }

                // sort them by size
                for (int i = 0; i < asynchMeshesList.Count; i++)
                {
                    AxisAlignedBoundingBox iAABB = asynchMeshesList[i].GetAxisAlignedBoundingBox(asynchMeshTransforms[i]);
                    for (int j = i + 1; j < asynchMeshesList.Count; j++)
                    {
                        AxisAlignedBoundingBox jAABB = asynchMeshesList[j].GetAxisAlignedBoundingBox(asynchMeshTransforms[j]);
                        if (Math.Max(iAABB.XSize, iAABB.YSize) < Math.Max(jAABB.XSize, jAABB.YSize))
                        {
                            PlatingMeshData tempData = asynchPlatingDataList[i];
                            asynchPlatingDataList[i] = asynchPlatingDataList[j];
                            asynchPlatingDataList[j] = tempData;

                            Mesh tempMesh = asynchMeshesList[i];
                            asynchMeshesList[i] = asynchMeshesList[j];
                            asynchMeshesList[j] = tempMesh;

                            Matrix4X4 tempTransform = asynchMeshTransforms[i];
                            asynchMeshTransforms[i] = asynchMeshTransforms[j];
                            asynchMeshTransforms[j] = tempTransform;

                            iAABB = jAABB;
                        }
                    }
                }

                // put them onto the plate (try the center) starting with the biggest and moving down
                for (int i = 0; i < asynchMeshesList.Count; i++)
                {
                    Mesh mesh = asynchMeshesList[i];
                    Vector3 meshCenter = mesh.GetAxisAlignedBoundingBox(asynchMeshTransforms[i]).Center;
                    asynchMeshTransforms[i] *= Matrix4X4.CreateTranslation(-meshCenter);
                    PlatingHelper.MoveMeshToOpenPosition(i, asynchPlatingDataList, asynchMeshesList, asynchMeshTransforms);

                    // and create the trace info so we can select it
                    PlatingHelper.CreateITraceableForMesh(asynchPlatingDataList, asynchMeshesList, i);

                    // and put it on the bed
                    {
                        AxisAlignedBoundingBox bounds = asynchMeshesList[i].GetAxisAlignedBoundingBox(asynchMeshTransforms[i]);
                        Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;
                        asynchMeshTransforms[i] *= Matrix4X4.CreateTranslation(new Vector3(0, 0, -boundsCenter.z + bounds.ZSize / 2));
                    }

                    int nextPercent = (i + 1) * 100 / asynchMeshesList.Count;
                    backgroundWorker.ReportProgress(nextPercent);
                }

                // and finally center whatever we have as a group
                {
                    AxisAlignedBoundingBox bounds = asynchMeshesList[0].GetAxisAlignedBoundingBox(asynchMeshTransforms[0]);
                    for (int i = 1; i < asynchMeshesList.Count; i++)
                    {
                        bounds = AxisAlignedBoundingBox.Union(bounds, asynchMeshesList[i].GetAxisAlignedBoundingBox(asynchMeshTransforms[i]));
                    }

                    Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;
                    for (int i = 0; i < asynchMeshesList.Count; i++)
                    {
                        asynchMeshTransforms[i] *= Matrix4X4.CreateTranslation(-boundsCenter + new Vector3(0, 0, bounds.ZSize / 2));
                    }
                }
            }
        }

        private void LoadAndAddPartsToPlate(string[] filesToLoad)
        {
            if (Meshes.Count > 0 && filesToLoad != null && filesToLoad.Length > 0)
            {
				string loadingPartLabel = LocalizedString.Get("Loading Parts");
				string loadingPartLabelFull = "{0}:".FormatWith(loadingPartLabel);
				processingProgressControl.textWidget.Text = loadingPartLabelFull;
                processingProgressControl.Visible = true;
                processingProgressControl.PercentComplete = 0;
                LockEditControls();

                PushMeshDataToAsynchLists(true);

                BackgroundWorker loadAndAddPartsToPlateBackgroundWorker = null;
                loadAndAddPartsToPlateBackgroundWorker = new BackgroundWorker();
                loadAndAddPartsToPlateBackgroundWorker.WorkerReportsProgress = true;

                loadAndAddPartsToPlateBackgroundWorker.DoWork += new DoWorkEventHandler(loadAndAddPartsToPlateBackgroundWorker_DoWork);
                loadAndAddPartsToPlateBackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(BackgroundWorker_ProgressChanged);
                loadAndAddPartsToPlateBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(loadAndAddPartsToPlateBackgroundWorker_RunWorkerCompleted);

                loadAndAddPartsToPlateBackgroundWorker.RunWorkerAsync(filesToLoad);
            }
        }

        private void PushMeshDataToAsynchLists(bool copyTraceInfo)
        {
            asynchMeshesList.Clear();
            asynchMeshTransforms.Clear();
            for (int i = 0; i < Meshes.Count; i++)
            {
                Mesh mesh = Meshes[i];
                asynchMeshesList.Add(new Mesh(mesh));
                asynchMeshTransforms.Add(MeshTransforms[i]);
            }
            asynchPlatingDataList.Clear();
            for (int i = 0; i < MeshExtraData.Count; i++)
            {
                PlatingMeshData meshData = new PlatingMeshData();
                meshData.currentScale = MeshExtraData[i].currentScale;
                if (copyTraceInfo)
                {
                    meshData.traceableData = MeshExtraData[i].traceableData;
                }
                asynchPlatingDataList.Add(meshData);
            }
        }

        void arrangePartsBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UnlockEditControls();
            saveButtons.Visible = true;
            viewControls3D.partSelectButton.ClickButton(null);

            PullMeshDataFromAsynchLists();
        }

        void loadAndAddPartsToPlateBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UnlockEditControls();
            saveButtons.Visible = true;
            viewControls3D.partSelectButton.ClickButton(null);

            if (asynchMeshesList.Count == Meshes.Count + 1)
            {
                // if we are only adding one part to the plate set the selection to it
                SelectedMeshIndex = asynchMeshesList.Count - 1;
            }

            PullMeshDataFromAsynchLists();
        }

        private void PullMeshDataFromAsynchLists()
        {
            Meshes.Clear();
            foreach (Mesh mesh in asynchMeshesList)
            {
                Meshes.Add(mesh);
            }
            MeshTransforms.Clear();
            foreach (Matrix4X4 transform in asynchMeshTransforms)
            {
                MeshTransforms.Add(transform);
            }
            MeshExtraData.Clear();
            foreach (PlatingMeshData meshData in asynchPlatingDataList)
            {
                MeshExtraData.Add(meshData);
            }
        }

        void loadAndAddPartsToPlateBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            BackgroundWorker backgroundWorker = (BackgroundWorker)sender;

            string[] filesToLoad = e.Argument as string[];
            if (filesToLoad != null && filesToLoad.Length > 0)
            {
                int lastPercent = 0;
                for (int i = 0; i < filesToLoad.Length; i++)
                {
                    int nextPercent = i + 1 * 100 / filesToLoad.Length;
                    int subLength = nextPercent - lastPercent;

                    string loadedFileName = filesToLoad[i];
                    Mesh copyMesh = StlProcessing.Load(Path.GetFullPath(loadedFileName));
                    if (copyMesh != null)
                    {
                        int halfNextPercent = (nextPercent - lastPercent) / 2;
                        Mesh[] subMeshes = CreateDiscreteMeshes.SplitIntoMeshes(copyMesh, meshViewerWidget.DisplayVolume, backgroundWorker, lastPercent, halfNextPercent);
                        lastPercent = halfNextPercent;

                        for (int subMeshIndex = 0; subMeshIndex < subMeshes.Length; subMeshIndex++)
                        {
                            Mesh subMesh = subMeshes[subMeshIndex];
                            Vector3 soubMeshBoundsCenter = subMesh.GetAxisAlignedBoundingBox().Center;
                            soubMeshBoundsCenter.z = 0;
                            subMesh.Translate(-soubMeshBoundsCenter);
                            PlatingHelper.FindPositionForPartAndAddToPlate(subMesh, Matrix4X4.Identity, asynchPlatingDataList, asynchMeshesList, asynchMeshTransforms);
                            PlatingHelper.CreateITraceableForMesh(asynchPlatingDataList, asynchMeshesList, asynchMeshesList.Count - 1);

                            backgroundWorker.ReportProgress(lastPercent + subMeshIndex + 1 * subLength / subMeshes.Length);
                        }

                        backgroundWorker.ReportProgress(nextPercent);
                        lastPercent = nextPercent;
                    }
                }
            }
        }

        void meshViewerWidget_LoadDone(object sender, EventArgs e)
        {
            switch (PrinterConnectionAndCommunication.Instance.CommunicationState)
            {
                case PrinterConnectionAndCommunication.CommunicationStates.Printing:
                case PrinterConnectionAndCommunication.CommunicationStates.Paused:
                    break;

                default:
                    UnlockEditControls();
                    break;
            }
        }

        bool wasInEditMode = false;
        void LockEditControls()
        {
            wasInEditMode = doEdittingButtonsContainer.Visible;
            enterEditButtonsContainer.Visible = false;
            doEdittingButtonsContainer.Visible = false;
            buttonRightPanelDisabledCover.Visible = true;
            viewControls3D.PartSelectVisible = false;
            if (meshViewerWidget.TrackballTumbleWidget.TransformState == TrackBallController.MouseDownType.None)
            {
                viewControls3D.rotateButton.ClickButton(null);
            }
        }

        void UnlockEditControls()
        {
            buttonRightPanelDisabledCover.Visible = false;
            processingProgressControl.Visible = false;

            if (wasInEditMode)
            {
                if (!enterEditButtonsContainer.Visible)
                {
                    viewControls3D.PartSelectVisible = true;
                    doEdittingButtonsContainer.Visible = true;
                    doEdittingButtonsContainer.Visible = true;
                }
            }
            else
            {
                enterEditButtonsContainer.Visible = true;
            }

            UpdateSizeInfo();
        }

        private void DeleteSelectedMesh()
        {
            // don't ever delet the last mesh
            if (Meshes.Count > 1)
            {
                Meshes.RemoveAt(SelectedMeshIndex);
                MeshExtraData.RemoveAt(SelectedMeshIndex);
                MeshTransforms.RemoveAt(SelectedMeshIndex);
                SelectedMeshIndex = Math.Min(SelectedMeshIndex, Meshes.Count - 1);
                saveButtons.Visible = true;
                Invalidate();
            }
        }

        void EnterEditAndSplitIntoMeshes()
        {
            if (enterEditButtonsContainer.Visible == true)
            {
                enterEditButtonsContainer.Visible = false;

                if (Meshes.Count > 0)
                {
                    processingProgressControl.Visible = true;
                    LockEditControls();
                    wasInEditMode = true;

                    BackgroundWorker createDiscreteMeshesBackgroundWorker = null;
                    createDiscreteMeshesBackgroundWorker = new BackgroundWorker();
                    createDiscreteMeshesBackgroundWorker.WorkerReportsProgress = true;

                    createDiscreteMeshesBackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(BackgroundWorker_ProgressChanged);
                    createDiscreteMeshesBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(createDiscreteMeshesBackgroundWorker_RunWorkerCompleted);
                    createDiscreteMeshesBackgroundWorker.DoWork += new DoWorkEventHandler(createDiscreteMeshesBackgroundWorker_DoWork);

                    createDiscreteMeshesBackgroundWorker.RunWorkerAsync();
                }
            }
        }

        void createDiscreteMeshesBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            BackgroundWorker backgroundWorker = (BackgroundWorker)sender;

            Mesh[] meshes = CreateDiscreteMeshes.SplitIntoMeshes(SelectedMesh, meshViewerWidget.DisplayVolume, backgroundWorker, 0, 50);

            asynchMeshesList.Clear();
            asynchPlatingDataList.Clear();
            asynchMeshTransforms.Clear();
            for (int i = 0; i < meshes.Length; i++)
            {
                PlatingMeshData newInfo = new PlatingMeshData();
                asynchPlatingDataList.Add(newInfo);
                asynchMeshesList.Add(meshes[i]);
                asynchMeshTransforms.Add(Matrix4X4.Identity);

                PlatingHelper.CreateITraceableForMesh(asynchPlatingDataList, asynchMeshesList, i);
                if (meshes.Length > 1)
                {
                    backgroundWorker.ReportProgress(50 + i * 50 / (meshes.Length - 1));
                }
            }
        }

        void createDiscreteMeshesBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // remember the transform we have before spliting so we can put it on each of the parts after spliting.
            Matrix4X4 transformBeforeSplit = SelectedMeshTransform;
            // remove the original mesh and replace it with these new meshes
            PullMeshDataFromAsynchLists();

            for (int i = 0; i < Meshes.Count; i++)
            {
                meshViewerWidget.MeshTransforms[i] = transformBeforeSplit;
            }

            UnlockEditControls();

            autoArrangeButton.Visible = true;
            viewControls3D.partSelectButton.ClickButton(null);

            Invalidate();

            if (OpenAddDialogWhenDone)
            {
                OpenAddDialogWhenDone = false;
                OpenFileDialogParams openParams = new OpenFileDialogParams("Select an STL file|*.stl", multiSelect: true);

                FileDialog.OpenFileDialog(ref openParams);
                LoadAndAddPartsToPlate(openParams.FileNames);
            }

            if (pendingPartsToLoad.Count > 0)
            {
                LoadAndAddPartsToPlate(pendingPartsToLoad.ToArray());
            }
        }

        void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            processingProgressControl.PercentComplete = e.ProgressPercentage;
        }

        private void CreateOptionsContent()
        {
            AddRotateControls(rotateOptionContainer);
            AddScaleControls(scaleOptionContainer);
        }

        private FlowLayoutWidget CreateRightButtonPanel(double buildHeight)
        {
            FlowLayoutWidget buttonRightPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);
            buttonRightPanel.Width = 200;
            {
                BorderDouble buttonMargin = new BorderDouble(top: 3);

                expandRotateOptions = expandMenuOptionFactory.GenerateCheckBoxButton(LocalizedString.Get("Rotate"), "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                expandRotateOptions.Margin = new BorderDouble(bottom: 2);
                buttonRightPanel.AddChild(expandRotateOptions);

                rotateOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                rotateOptionContainer.HAnchor = HAnchor.ParentLeftRight;
                rotateOptionContainer.Visible = false;
                buttonRightPanel.AddChild(rotateOptionContainer);

                expandScaleOptions = expandMenuOptionFactory.GenerateCheckBoxButton(LocalizedString.Get("Scale"), "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                expandScaleOptions.Margin = new BorderDouble(bottom: 2);
                buttonRightPanel.AddChild(expandScaleOptions);

                scaleOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                scaleOptionContainer.HAnchor = HAnchor.ParentLeftRight;
                scaleOptionContainer.Visible = false;
                buttonRightPanel.AddChild(scaleOptionContainer);

                // put in the mirror options
                {
                    CheckBox expandMirrorOptions = expandMenuOptionFactory.GenerateCheckBoxButton(LocalizedString.Get("Mirror"), "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                    expandMirrorOptions.Margin = new BorderDouble(bottom: 2);
                    buttonRightPanel.AddChild(expandMirrorOptions);

                    FlowLayoutWidget mirrorOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                    mirrorOptionContainer.HAnchor = HAnchor.ParentLeftRight;
                    mirrorOptionContainer.Visible = false;
                    buttonRightPanel.AddChild(mirrorOptionContainer);

                    expandMirrorOptions.CheckedStateChanged += (object sender, EventArgs e) =>
                    {
                        mirrorOptionContainer.Visible = expandMirrorOptions.Checked;
                    };

                    AddMirrorControls(mirrorOptionContainer);
                }

#if false // this is not finished yet so it is in here for reference in case we do finish it. LBB 2014/04/04
                // put in the part info display
                if(false)
                {
					CheckBox expandPartInfoOptions = expandMenuOptionFactory.GenerateCheckBoxButton(LocalizedString.Get("Part Info"), "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                    expandPartInfoOptions.Margin = new BorderDouble(bottom: 2);
                    buttonRightPanel.AddChild(expandPartInfoOptions);

                    FlowLayoutWidget PartInfoOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                    PartInfoOptionContainer.HAnchor = HAnchor.ParentLeftRight;
                    PartInfoOptionContainer.Visible = false;
                    buttonRightPanel.AddChild(PartInfoOptionContainer);

                    expandPartInfoOptions.CheckedStateChanged += (object sender, EventArgs e) =>
                    {
                        PartInfoOptionContainer.Visible = expandPartInfoOptions.Checked;
                    };

                    PartInfoOptionContainer.Margin = new BorderDouble(8, 3);
					string sizeInfoLabel = LocalizedString.Get("Size");
					string sizeInfoLabelFull = "{0}:".FormatWith(sizeInfoLabel);
					TextWidget sizeInfo = new TextWidget(sizeInfoLabelFull, textColor: ActiveTheme.Instance.PrimaryTextColor);
                    PartInfoOptionContainer.AddChild(sizeInfo);
                    TextWidget xSizeInfo = new TextWidget("  x 10.1", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor);
                    xSizeInfo.AutoExpandBoundsToText = true;
                    PartInfoOptionContainer.AddChild(xSizeInfo);

                    TextWidget ySizeInfo = new TextWidget("  y 10.1", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor);
                    ySizeInfo.AutoExpandBoundsToText = true;
                    PartInfoOptionContainer.AddChild(ySizeInfo);

                    TextWidget zSizeInfo = new TextWidget("  z 100.1", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor);
                    zSizeInfo.AutoExpandBoundsToText = true;
                    PartInfoOptionContainer.AddChild(zSizeInfo);
                }
#endif

                // put in the view options
                {
                    expandViewOptions = expandMenuOptionFactory.GenerateCheckBoxButton(LocalizedString.Get("Display"), "icon_arrow_right_no_border_32x32.png", "icon_arrow_down_no_border_32x32.png");
                    expandViewOptions.Margin = new BorderDouble(bottom: 2);
                    buttonRightPanel.AddChild(expandViewOptions);

                    viewOptionContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                    viewOptionContainer.HAnchor = HAnchor.ParentLeftRight;
                    viewOptionContainer.Visible = false;
                    {
                        CheckBox showBedCheckBox = new CheckBox(LocalizedString.Get("Show Print Bed"), textColor: ActiveTheme.Instance.PrimaryTextColor);
                        showBedCheckBox.Checked = true;
                        showBedCheckBox.CheckedStateChanged += (sender, e) =>
                        {
                            meshViewerWidget.RenderBed = showBedCheckBox.Checked;
                        };
                        viewOptionContainer.AddChild(showBedCheckBox);

                        if (buildHeight > 0)
                        {
                            CheckBox showBuildVolumeCheckBox = new CheckBox(LocalizedString.Get("Show Print Area"), textColor: ActiveTheme.Instance.PrimaryTextColor);
                            showBuildVolumeCheckBox.Checked = false;
                            showBuildVolumeCheckBox.CheckedStateChanged += (sender, e) =>
                            {
                                meshViewerWidget.RenderBuildVolume = showBuildVolumeCheckBox.Checked;
                            };
                            viewOptionContainer.AddChild(showBuildVolumeCheckBox);
                        }

                        viewOptionContainer.AddChild(CreateRenderTypeDropDownMenu());
                    }
                    buttonRightPanel.AddChild(viewOptionContainer);
                }

                autoArrangeButton = whiteButtonFactory.Generate(LocalizedString.Get("Auto-Arrange"), centerText: true);
                autoArrangeButton.Visible = false;
                autoArrangeButton.Cursor = Cursors.Hand;
                buttonRightPanel.AddChild(autoArrangeButton);

                GuiWidget verticalSpacer = new GuiWidget();
                verticalSpacer.VAnchor = VAnchor.ParentBottomTop;
                buttonRightPanel.AddChild(verticalSpacer);

                saveButtons = CreateSaveButtons();
                buttonRightPanel.AddChild(saveButtons);
            }

            buttonRightPanel.Padding = new BorderDouble(6, 6);
            buttonRightPanel.Margin = new BorderDouble(0, 1);
            buttonRightPanel.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            buttonRightPanel.VAnchor = VAnchor.ParentBottomTop;

            return buttonRightPanel;
        }

        private FlowLayoutWidget CreateSaveButtons()
        {
            TextImageButtonFactory saveButtonFactory = new TextImageButtonFactory();
            saveButtonFactory.FixedWidth = 56;
            saveButtonFactory.FixedHeight = 40;
            saveButtonFactory.normalFillColor = RGBA_Bytes.White;
            saveButtonFactory.normalTextColor = RGBA_Bytes.Black;
            saveButtonFactory.hoverTextColor = RGBA_Bytes.Black;
            saveButtonFactory.hoverFillColor = new RGBA_Bytes(255, 255, 255, 200);

            FlowLayoutWidget saveButtons = new FlowLayoutWidget();

            //Create Save Button
            Button saveButton = saveButtonFactory.Generate(LocalizedString.Get("Save"), centerText: true);
            saveButton.Cursor = Cursors.Hand;
            saveButtons.AddChild(saveButton);
            saveButton.Click += (sender, e) =>
            {
                MergeAndSavePartsToStl();
            };

            //Create Save As Button 	
            saveButtonFactory.FixedWidth = SideBarButtonWidth - saveButtonFactory.FixedWidth - 2;
            Button saveAsButton = saveButtonFactory.Generate("Save As".Localize(), centerText: true);
            saveAsButton.Cursor = Cursors.Hand;
            saveButtons.AddChild(saveAsButton);
            saveAsButton.Click += (sender, e) =>
            {
				if(saveAsWindowIsOpen == false)
				{
				saveAsWindow = new SaveAsWindow(MergeAndSavePartsToStl);
				this.saveAsWindowIsOpen = true;
				saveAsWindow.Closed += new EventHandler(SaveAsWindow_Closed);
				}
				else
				{
					if(saveAsWindowIsOpen != null)
					{
						saveAsWindow.BringToFront();
					}
				}
            };

            saveButtons.Visible = false;

            return saveButtons;
        }

		void SaveAsWindow_Closed(object sender, EventArgs e)
		{
			this.saveAsWindowIsOpen = false;
		}

        private void AddScaleControls(FlowLayoutWidget buttonPanel)
        {
            List<GuiWidget> scaleControls = new List<GuiWidget>();
            transformControls.Add("Scale", scaleControls);

            // Put in the scale ratio edit field
            {
                FlowLayoutWidget scaleRatioContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
                scaleRatioContainer.HAnchor = HAnchor.ParentLeftRight;
                scaleRatioContainer.Padding = new BorderDouble(5);

                string scaleRatioLabelText = LocalizedString.Get("Ratio");
                string scaleRatioLabelTextFull = "{0}:".FormatWith(scaleRatioLabelText);
                TextWidget scaleRatioLabel = new TextWidget(scaleRatioLabelTextFull, textColor: ActiveTheme.Instance.PrimaryTextColor);
                scaleRatioLabel.Margin = new BorderDouble(0, 0, 3, 0);
                scaleRatioLabel.VAnchor = VAnchor.ParentCenter;
                scaleRatioContainer.AddChild(scaleRatioLabel);

                GuiWidget horizontalSpacer = new GuiWidget();
                horizontalSpacer.HAnchor = HAnchor.ParentLeftRight;
                scaleRatioContainer.AddChild(horizontalSpacer);

                scaleRatioControl = new MHNumberEdit(1, pixelWidth: 50, allowDecimals: true, increment: .05);
                scaleRatioControl.VAnchor = VAnchor.ParentCenter;
                scaleRatioContainer.AddChild(scaleRatioControl);
                scaleRatioControl.ActuallNumberEdit.KeyPressed += (sender, e) =>
                {
                    SetApplyScaleVisability();
                };

                scaleRatioControl.ActuallNumberEdit.KeyDown += (sender, e) =>
                {
                    SetApplyScaleVisability();
                };

                scaleRatioControl.ActuallNumberEdit.EnterPressed += (object sender, KeyEventArgs keyEvent) =>
                {
                    ApplyScaleFromEditField();
                };

                scaleRatioContainer.AddChild(CreateScaleDropDownMenu());

                buttonPanel.AddChild(scaleRatioContainer);

                scaleControls.Add(scaleRatioControl);
            }

            applyScaleButton = whiteButtonFactory.Generate(LocalizedString.Get("Apply Scale"), centerText: true);
            applyScaleButton.Visible = false;
            applyScaleButton.Cursor = Cursors.Hand;
            buttonPanel.AddChild(applyScaleButton);

            scaleControls.Add(applyScaleButton);
            applyScaleButton.Click += (object sender, MouseEventArgs mouseEvent) =>
            {
                ApplyScaleFromEditField();
            };

            // add in the dimensions
            {
                buttonPanel.AddChild(createAxisScalingControl("x", 0));
                buttonPanel.AddChild(createAxisScalingControl("y", 1));
                buttonPanel.AddChild(createAxisScalingControl("z", 2));

                uniformScale = new CheckBox(LocalizedString.Get("Lock Ratio"), textColor: ActiveTheme.Instance.PrimaryTextColor);
                uniformScale.Checked = true;
                
                FlowLayoutWidget leftToRight = new FlowLayoutWidget();
                leftToRight.Padding = new BorderDouble(5, 3);

                leftToRight.AddChild(uniformScale);
                buttonPanel.AddChild(leftToRight);
            }

            buttonPanel.AddChild(generateHorizontalRule());
        }

        CheckBox uniformScale;
        EditableNumberDisplay[] sizeDisplay = new EditableNumberDisplay[3];
        private GuiWidget createAxisScalingControl(string axis, int axisIndex)
        {
            FlowLayoutWidget leftToRight = new FlowLayoutWidget();
            leftToRight.Padding = new BorderDouble(5, 3);

            TextWidget sizeDescription = new TextWidget("{0}:".FormatWith(axis), textColor: ActiveTheme.Instance.PrimaryTextColor);
            sizeDescription.VAnchor = Agg.UI.VAnchor.ParentCenter;
            leftToRight.AddChild(sizeDescription);

            sizeDisplay[axisIndex] = new EditableNumberDisplay(textImageButtonFactory, "100", "1000.00");
            sizeDisplay[axisIndex].EditComplete += (sender, e) =>
            {
                SetNewModelSize(sizeDisplay[axisIndex].GetValue(), axisIndex);
                sizeDisplay[axisIndex].SetDisplayString("{0:0.00}".FormatWith(SelectedMesh.GetAxisAlignedBoundingBox().Size[axisIndex]));
                UpdateSizeInfo();
            };

            leftToRight.AddChild(sizeDisplay[axisIndex]);

            return leftToRight;
        }

        void SetNewModelSize(double sizeInMm, int axis)
        {
            // because we remove any current scale before we change to a new one we only get the size of the base mesh data
            AxisAlignedBoundingBox originalMeshBounds = SelectedMesh.GetAxisAlignedBoundingBox();

            double currentSize = originalMeshBounds.Size[axis];
            double desiredSize = sizeDisplay[axis].GetValue();
            double scaleFactor = 1;
            if (currentSize != 0)
            {
                scaleFactor = desiredSize / currentSize;
            }

            if (uniformScale.Checked)
            {
                scaleRatioControl.ActuallNumberEdit.Value = scaleFactor;
                ApplyScaleFromEditField();
            }
            else
            {
                ScaleAxis(scaleFactor, axis);
            }
        }

        void UpdateSizeInfo()
        {
            if (sizeDisplay[0] != null && SelectedMesh != null)
            {
                AxisAlignedBoundingBox bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                sizeDisplay[0].SetDisplayString("{0:0.00}".FormatWith(bounds.Size[0]));
                sizeDisplay[1].SetDisplayString("{0:0.00}".FormatWith(bounds.Size[1]));
                sizeDisplay[2].SetDisplayString("{0:0.00}".FormatWith(bounds.Size[2]));
            }
        }

        private void SetApplyScaleVisability()
        {
            double scale = scaleRatioControl.ActuallNumberEdit.Value;
            if (scale != MeshExtraData[SelectedMeshIndex].currentScale[0]
                || scale != MeshExtraData[SelectedMeshIndex].currentScale[1]
                || scale != MeshExtraData[SelectedMeshIndex].currentScale[2])
            {
                applyScaleButton.Visible = true;
            }
            else
            {
                applyScaleButton.Visible = false;
            }

            UpdateSizeInfo();
        }

        private DropDownMenu CreateScaleDropDownMenu()
        {
            DropDownMenu presetScaleMenu = new DropDownMenu("", Direction.Down);
            presetScaleMenu.NormalArrowColor = ActiveTheme.Instance.PrimaryTextColor;
            presetScaleMenu.HoverArrowColor = ActiveTheme.Instance.PrimaryTextColor;
            presetScaleMenu.MenuAsWideAsItems = false;
            presetScaleMenu.AlignToRightEdge = true;
            //presetScaleMenu.OpenOffset = new Vector2(-50, 0);
            presetScaleMenu.HAnchor = HAnchor.None;
            presetScaleMenu.VAnchor = VAnchor.None;
            presetScaleMenu.Width = 25;
            presetScaleMenu.Height = scaleRatioControl.Height + 2;

            presetScaleMenu.AddItem("mm to in (.0393)");
            presetScaleMenu.AddItem("in to mm (25.4)");
            presetScaleMenu.AddItem("mm to cm (.1)");
            presetScaleMenu.AddItem("cm to mm (10)");
            string resetLable = LocalizedString.Get("reset");
            string resetLableFull = "{0} (1)".FormatWith(resetLable);
            presetScaleMenu.AddItem(resetLableFull);

            presetScaleMenu.SelectionChanged += (sender, e) =>
            {
                double scale = 1;
                switch (presetScaleMenu.SelectedIndex)
                {
                    case 0:
                        scale = 1.0 / 25.4;
                        break;
                    case 1:
                        scale = 25.4;
                        break;
                    case 2:
                        scale = .1;
                        break;
                    case 3:
                        scale = 10;
                        break;
                    case 4:
                        scale = 1;
                        break;
                }

                scaleRatioControl.ActuallNumberEdit.Value = scale;
                ApplyScaleFromEditField();
            };

            return presetScaleMenu;
        }

        private DropDownMenu CreateRenderTypeDropDownMenu()
        {
            DropDownMenu renderTypeMenu = new DropDownMenu(LocalizedString.Get("Render Type"), Direction.Down);
            RectangleDouble presetBounds = renderTypeMenu.LocalBounds;
            presetBounds.Inflate(new BorderDouble(5, 10, 10, 10));
            renderTypeMenu.LocalBounds = presetBounds;
            renderTypeMenu.MenuAsWideAsItems = false;
            renderTypeMenu.HAnchor |= HAnchor.ParentLeftRight;

            renderTypeMenu.AddItem("Shaded".Localize());
            //renderTypeMenu.AddItem("Outlines".Localize());
            renderTypeMenu.AddItem("Polygons".Localize());

            renderTypeMenu.SelectionChanged += (sender, e) =>
            {
                switch (renderTypeMenu.SelectedIndex)
                {
                    case 0:
                        meshViewerWidget.RenderType = RenderTypes.Shaded;
                        break;
                    case 1:
                        meshViewerWidget.RenderType = RenderTypes.Outlines;
                        break;
                    case 2:
                        meshViewerWidget.RenderType = RenderTypes.Polygons;
                        break;
                }
            };

            return renderTypeMenu;
        }

        private GuiWidget generateHorizontalRule()
        {
            GuiWidget horizontalRule = new GuiWidget();
            horizontalRule.Height = 1;
            horizontalRule.Margin = new BorderDouble(0, 1, 0, 3);
            horizontalRule.HAnchor = HAnchor.ParentLeftRight;
            horizontalRule.BackgroundColor = new RGBA_Bytes(255, 255, 255, 200);
            return horizontalRule;
        }

        private void ApplyScaleFromEditField()
        {
            double scale = scaleRatioControl.ActuallNumberEdit.Value;
            if (scale > 0)
            {
                ScaleAxis(scale, 0);
                ScaleAxis(scale, 1);
                ScaleAxis(scale, 2);
            }
        }

        public override void OnDragEnter(FileDropEventArgs fileDropEventArgs)
        {
            foreach (string file in fileDropEventArgs.DroppedFiles)
            {
                string extension = Path.GetExtension(file).ToUpper();
                if (extension == ".STL")
                {
                    fileDropEventArgs.AcceptDrop = true;
                }
            }
            base.OnDragEnter(fileDropEventArgs);
        }

        public override void OnDragOver(FileDropEventArgs fileDropEventArgs)
        {
            foreach (string file in fileDropEventArgs.DroppedFiles)
            {
                string extension = Path.GetExtension(file).ToUpper();
                if (extension == ".STL")
                {
                    fileDropEventArgs.AcceptDrop = true;
                }
            }
            base.OnDragOver(fileDropEventArgs);
        }

        public override void OnDragDrop(FileDropEventArgs fileDropEventArgs)
        {
            pendingPartsToLoad.Clear();
            foreach (string droppedFileName in fileDropEventArgs.DroppedFiles)
            {
                string extension = Path.GetExtension(droppedFileName).ToUpper();
                if (extension == ".STL")
                {
                    pendingPartsToLoad.Add(droppedFileName);
                }
            }

            bool enterEditModeBeforeAddingParts = enterEditButtonsContainer.Visible == true;
            if (enterEditModeBeforeAddingParts)
            {
                EnterEditAndSplitIntoMeshes();
            }
            else
            {
                LoadAndAddPartsToPlate(pendingPartsToLoad.ToArray());
            }

            base.OnDragDrop(fileDropEventArgs);
        }

        private void ScaleAxis(double scaleIn, int axis)
        {
            AxisAlignedBoundingBox originalMeshBounds = SelectedMesh.GetAxisAlignedBoundingBox();
            AxisAlignedBoundingBox scaledBounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);

            Vector3 center = (scaledBounds.maxXYZ - scaledBounds.minXYZ) / 2 + scaledBounds.minXYZ;
            Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(-center);

            // first we remove any scale we have applied and then scale to the new value
            Vector3 axisRemoveScalings = new Vector3();
            axisRemoveScalings.x = scaledBounds.Size.x / originalMeshBounds.Size.x;
            axisRemoveScalings.y = scaledBounds.Size.y / originalMeshBounds.Size.y;
            axisRemoveScalings.z = scaledBounds.Size.z / originalMeshBounds.Size.z;

            Matrix4X4 removeScaleMatrix = Matrix4X4.CreateScale(1 / axisRemoveScalings);
            
            Vector3 newScale = MeshExtraData[SelectedMeshIndex].currentScale;
            newScale[axis] = scaleIn;
            removeScaleMatrix *= Matrix4X4.CreateScale(newScale);
            totalTransfrom *= removeScaleMatrix;

            totalTransfrom *= Matrix4X4.CreateTranslation(center);
            SelectedMeshTransform *= totalTransfrom;

            PlatingHelper.PlaceMeshOnBed(Meshes, MeshTransforms, SelectedMeshIndex, false);
            saveButtons.Visible = true;
            Invalidate();
            MeshExtraData[SelectedMeshIndex].currentScale[axis] = scaleIn;
            SetApplyScaleVisability();
        }

        private void AddRotateControls(FlowLayoutWidget buttonPanel)
        {
            List<GuiWidget> rotateControls = new List<GuiWidget>();
			transformControls.Add(LocalizedString.Get("Rotate"), rotateControls);

            textImageButtonFactory.FixedWidth = 44;

            FlowLayoutWidget degreesContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            degreesContainer.HAnchor = HAnchor.ParentLeftRight;
            degreesContainer.Padding = new BorderDouble(5);

            GuiWidget horizontalSpacer = new GuiWidget();
            horizontalSpacer.HAnchor = HAnchor.ParentLeftRight;

			string degreesLabelText = LocalizedString.Get("Degrees");
			string degreesLabelTextFull = "{0}:".FormatWith(degreesLabelText);
            TextWidget degreesLabel = new TextWidget(degreesLabelText, textColor: ActiveTheme.Instance.PrimaryTextColor);
            degreesContainer.AddChild(degreesLabel);
            degreesContainer.AddChild(horizontalSpacer);

            MHNumberEdit degreesControl = new MHNumberEdit(45, pixelWidth: 40, allowNegatives: true, increment: 5, minValue: -360, maxValue: 360);
            degreesControl.VAnchor = Agg.UI.VAnchor.ParentTop;
            degreesContainer.AddChild(degreesControl);
            rotateControls.Add(degreesControl);

            buttonPanel.AddChild(degreesContainer);

            FlowLayoutWidget rotateButtonContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            rotateButtonContainer.HAnchor = HAnchor.ParentLeftRight;

            Button rotateXButton = textImageButtonFactory.Generate("", "icon_rotate_32x32.png");
            TextWidget centeredX = new TextWidget("X", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor); centeredX.Margin = new BorderDouble(3, 0, 0, 0); centeredX.AnchorCenter(); rotateXButton.AddChild(centeredX);
            rotateButtonContainer.AddChild(rotateXButton);
            rotateControls.Add(rotateXButton);
            rotateXButton.Click += (object sender, MouseEventArgs mouseEvent) =>
            {
                double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
                AxisAlignedBoundingBox bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                Vector3 startingCenter = bounds.Center;
                // move it to the origin so it rotates about it's center
                Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(-startingCenter);
                // rotate it
                totalTransfrom *= Matrix4X4.CreateRotationX(radians);
                SelectedMeshTransform *= totalTransfrom;
                // find the new center
                bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                // and shift it back so the new center is where the old center was
                SelectedMeshTransform *= Matrix4X4.CreateTranslation(startingCenter - bounds.Center);
                PlatingHelper.PlaceMeshOnBed(Meshes, MeshTransforms, SelectedMeshIndex, false);
                saveButtons.Visible = true;
                Invalidate();
            };

            Button rotateYButton = textImageButtonFactory.Generate("", "icon_rotate_32x32.png");
            TextWidget centeredY = new TextWidget("Y", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor); centeredY.Margin = new BorderDouble(3, 0, 0, 0); centeredY.AnchorCenter(); rotateYButton.AddChild(centeredY);
            rotateButtonContainer.AddChild(rotateYButton);
            rotateControls.Add(rotateYButton);
            rotateYButton.Click += (object sender, MouseEventArgs mouseEvent) =>
            {
                double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
                AxisAlignedBoundingBox bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                Vector3 startingCenter = bounds.Center;
                // move it to the origin so it rotates about it's center
                Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(-startingCenter);
                // rotate it
                totalTransfrom *= Matrix4X4.CreateRotationY(radians);
                SelectedMeshTransform *= totalTransfrom;
                // find the new center
                bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                // and shift it back so the new center is where the old center was
                SelectedMeshTransform *= Matrix4X4.CreateTranslation(startingCenter - bounds.Center);
                PlatingHelper.PlaceMeshOnBed(Meshes, MeshTransforms, SelectedMeshIndex, false);
                saveButtons.Visible = true;
                Invalidate();
            };

            Button rotateZButton = textImageButtonFactory.Generate("", "icon_rotate_32x32.png");
            TextWidget centeredZ = new TextWidget("Z", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor); centeredZ.Margin = new BorderDouble(3, 0, 0, 0); centeredZ.AnchorCenter(); rotateZButton.AddChild(centeredZ);
            rotateButtonContainer.AddChild(rotateZButton);
            rotateControls.Add(rotateZButton);
            rotateZButton.Click += (object sender, MouseEventArgs mouseEvent) =>
            {
                double radians = MathHelper.DegreesToRadians(degreesControl.ActuallNumberEdit.Value);
                AxisAlignedBoundingBox bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                Vector3 startingCenter = bounds.Center;
                // move it to the origin so it rotates about it's center
                Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(-startingCenter);
                // rotate it
                totalTransfrom *= Matrix4X4.CreateRotationZ(radians);
                SelectedMeshTransform *= totalTransfrom;
                // find the new center
                bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                // and shift it back so the new center is where the old center was
                SelectedMeshTransform *= Matrix4X4.CreateTranslation(startingCenter - bounds.Center);
                saveButtons.Visible = true;
                Invalidate();
            };

            buttonPanel.AddChild(rotateButtonContainer);

			Button layFlatButton = whiteButtonFactory.Generate(LocalizedString.Get("Align to Bed"), centerText: true);
            layFlatButton.Cursor = Cursors.Hand;
            buttonPanel.AddChild(layFlatButton);

            layFlatButton.Click += (object sender, MouseEventArgs mouseEvent) =>
            {
                MakeLowestFaceFlat(SelectedMeshIndex);

                saveButtons.Visible = true;
                Invalidate();
            };

            buttonPanel.AddChild(generateHorizontalRule());
            textImageButtonFactory.FixedWidth = 0;
        }

        private void AddMirrorControls(FlowLayoutWidget buttonPanel)
        {
            List<GuiWidget> mirrorControls = new List<GuiWidget>();
            transformControls.Add("Mirror", mirrorControls);

            textImageButtonFactory.FixedWidth = 44;

            FlowLayoutWidget buttonContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            buttonContainer.HAnchor = HAnchor.ParentLeftRight;

            Button mirrorXButton = textImageButtonFactory.Generate("X", centerText: true);
            buttonContainer.AddChild(mirrorXButton);
            mirrorControls.Add(mirrorXButton);
            mirrorXButton.Click += (object sender, MouseEventArgs mouseEvent) =>
            {
                SelectedMesh.ReverseFaceEdges();

                AxisAlignedBoundingBox bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                Vector3 center = (bounds.maxXYZ - bounds.minXYZ) / 2 + bounds.minXYZ;
                Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(-center);
                totalTransfrom *= Matrix4X4.CreateScale(-1, 1, 1);
                totalTransfrom *= Matrix4X4.CreateTranslation(center);

                SelectedMeshTransform *= totalTransfrom;
                saveButtons.Visible = true;
                Invalidate();
            };

            Button mirrorYButton = textImageButtonFactory.Generate("Y", centerText: true);
            buttonContainer.AddChild(mirrorYButton);
            mirrorControls.Add(mirrorYButton);
            mirrorYButton.Click += (object sender, MouseEventArgs mouseEvent) =>
            {
                SelectedMesh.ReverseFaceEdges();

                AxisAlignedBoundingBox bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                Vector3 center = (bounds.maxXYZ - bounds.minXYZ) / 2 + bounds.minXYZ;
                Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(-center);
                totalTransfrom *= Matrix4X4.CreateScale(1, -1, 1);
                totalTransfrom *= Matrix4X4.CreateTranslation(center);

                SelectedMeshTransform *= totalTransfrom;
                saveButtons.Visible = true;
                Invalidate();
            };

            Button mirrorZButton = textImageButtonFactory.Generate("Z", centerText: true);
            buttonContainer.AddChild(mirrorZButton);
            mirrorControls.Add(mirrorZButton);
            mirrorZButton.Click += (object sender, MouseEventArgs mouseEvent) =>
            {
                SelectedMesh.ReverseFaceEdges();

                AxisAlignedBoundingBox bounds = SelectedMesh.GetAxisAlignedBoundingBox(SelectedMeshTransform);
                Vector3 center = (bounds.maxXYZ - bounds.minXYZ) / 2 + bounds.minXYZ;
                Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(-center);
                totalTransfrom *= Matrix4X4.CreateScale(1, 1, -1);
                totalTransfrom *= Matrix4X4.CreateTranslation(center);

                SelectedMeshTransform *= totalTransfrom;
                PlatingHelper.PlaceMeshOnBed(Meshes, MeshTransforms, SelectedMeshIndex, false);

                saveButtons.Visible = true;
                Invalidate();
            };
            buttonPanel.AddChild(buttonContainer);
            buttonPanel.AddChild(generateHorizontalRule());
            textImageButtonFactory.FixedWidth = 0;
        }

        private void AddHandlers()
        {
            expandViewOptions.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(expandViewOptions_CheckedStateChanged);
            expandRotateOptions.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(expandRotateOptions_CheckedStateChanged);
            expandScaleOptions.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(expandScaleOptions_CheckedStateChanged);
        }


        bool partSelectButtonWasClicked = false;
        private void MergeAndSavePartsToStl(PrintItemWrapper printItemWarpperToSwitchTo = null)
        {
            if (printItemWarpperToSwitchTo != null)
            {
                printItemWrapper = printItemWarpperToSwitchTo;
            }

            if (Meshes.Count > 0)
            {
                partSelectButtonWasClicked = viewControls3D.partSelectButton.Checked;

                string progressSavingPartsLabel = LocalizedString.Get("Saving");
                string progressSavingPartsLabelFull = "{0}:".FormatWith(progressSavingPartsLabel);
                processingProgressControl.textWidget.Text = progressSavingPartsLabelFull;
                processingProgressControl.Visible = true;
                processingProgressControl.PercentComplete = 0;
                LockEditControls();

                BackgroundWorker mergeAndSavePartsBackgroundWorker = new BackgroundWorker();
                mergeAndSavePartsBackgroundWorker.WorkerReportsProgress = true;

                mergeAndSavePartsBackgroundWorker.DoWork += new DoWorkEventHandler(mergeAndSavePartsBackgroundWorker_DoWork);
                mergeAndSavePartsBackgroundWorker.ProgressChanged += new ProgressChangedEventHandler(BackgroundWorker_ProgressChanged);
                mergeAndSavePartsBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(mergeAndSavePartsBackgroundWorker_RunWorkerCompleted);

                mergeAndSavePartsBackgroundWorker.RunWorkerAsync();
            }
        }

        void mergeAndSavePartsBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // we sent the data to the asynch lists but we will not pull it back out (only use it as a temp holder).
            PushMeshDataToAsynchLists(true);

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            BackgroundWorker backgroundWorker = (BackgroundWorker)sender;
            try
            {
                // push all the transforms into the meshes
                for (int i = 0; i < asynchMeshesList.Count; i++)
                {
                    asynchMeshesList[i].Transform(MeshTransforms[i]);

                    int nextPercent = (i + 1) * 40 / asynchMeshesList.Count;
                    backgroundWorker.ReportProgress(nextPercent);
                }

                Mesh mergedMesh = PlatingHelper.DoMerge(asynchMeshesList, backgroundWorker, 40, 80);
                StlProcessing.Save(mergedMesh, printItemWrapper.FileLocation);
                printItemWrapper.OnFileHasChanged();
            }
            catch (System.UnauthorizedAccessException)
            {
                //Do something special when unauthorized?
                StyledMessageBox.ShowMessageBox("Oops! Unable to save changes.", "Unable to save");
            }
            catch
            {
                StyledMessageBox.ShowMessageBox("Oops! Unable to save changes.", "Unable to save");
            }
        }

        void mergeAndSavePartsBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UnlockEditControls();
            // NOTE: we do not pull the data back out of the asynch lists.
            saveButtons.Visible = false;

            if (partSelectButtonWasClicked)
            {
                viewControls3D.partSelectButton.ClickButton(null);
            }
        }

        void expandViewOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            viewOptionContainer.Visible = expandViewOptions.Checked;
        }

        void expandRotateOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            rotateOptionContainer.Visible = expandRotateOptions.Checked;
        }

        void expandScaleOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            scaleOptionContainer.Visible = expandScaleOptions.Checked;
        }

        bool scaleQueueMenu_Click()
        {
            return true;
        }

        bool rotateQueueMenu_Click()
        {
            return true;
        }

        private void MakeLowestFaceFlat(int indexToLayFlat)
        {
            Mesh meshToLayFlat = Meshes[indexToLayFlat];
            Vertex lowestVertex = meshToLayFlat.Vertices[0];
            Vector3 lowestVertexPosition = Vector3.Transform(lowestVertex.Position, MeshTransforms[indexToLayFlat]);
            // find the lowest point on the model
            for (int testIndex = 1; testIndex < meshToLayFlat.Vertices.Count; testIndex++)
            {
                Vertex vertex = meshToLayFlat.Vertices[testIndex];
                Vector3 vertexPosition = Vector3.Transform(vertex.Position, MeshTransforms[indexToLayFlat]);
                if (vertexPosition.z < lowestVertexPosition.z)
                {
                    lowestVertex = meshToLayFlat.Vertices[testIndex];
                    lowestVertexPosition = vertexPosition;
                }
            }

            Face faceToLayFlat = null;
            double lowestAngleOfAnyFace = double.MaxValue;
            // Check all the faces that are connected to the lowest point to find out which one to lay flat.
            foreach (Face face in lowestVertex.ConnectedFaces())
            {
                double biggestAngleToFaceVertex = double.MinValue;
                foreach (Vertex faceVertex in face.Vertices())
                {
                    if (faceVertex != lowestVertex)
                    {
                        Vector3 faceVertexPosition = Vector3.Transform(faceVertex.Position, MeshTransforms[indexToLayFlat]);
                        Vector3 pointRelLowest = faceVertexPosition - lowestVertexPosition;
                        double xLeg = new Vector2(pointRelLowest.x, pointRelLowest.y).Length;
                        double yLeg = pointRelLowest.z;
                        double angle = Math.Atan2(yLeg, xLeg);
                        if (angle > biggestAngleToFaceVertex)
                        {
                            biggestAngleToFaceVertex = angle;
                        }
                    }
                }
                if (biggestAngleToFaceVertex < lowestAngleOfAnyFace)
                {
                    lowestAngleOfAnyFace = biggestAngleToFaceVertex;
                    faceToLayFlat = face;
                }
            }

            double maxDistFrom0 = 0;
            List<Vector3> faceVertexes = new List<Vector3>();
            foreach (Vertex vertex in faceToLayFlat.Vertices())
            {
                Vector3 vertexPosition = Vector3.Transform(vertex.Position, MeshTransforms[indexToLayFlat]);
                faceVertexes.Add(vertexPosition);
                maxDistFrom0 = Math.Max(maxDistFrom0, vertexPosition.z);
            }

            if (maxDistFrom0 > .001)
            {
                Vector3 xPositive = (faceVertexes[1] - faceVertexes[0]).GetNormal();
                Vector3 yPositive = (faceVertexes[2] - faceVertexes[0]).GetNormal();
                Vector3 planeNormal = Vector3.Cross(xPositive, yPositive).GetNormal();

#if true
                // this code takes the minimum rotation required and looks much better.
                Quaternion rotation = new Quaternion(planeNormal, new Vector3(0, 0, -1));
                Matrix4X4 partLevelMatrix = Matrix4X4.CreateRotation(rotation);
#else
                double planeOffset = Vector3.Dot(planeNormal, faceVertexes[1]);

                Matrix4X4 partLevelMatrix = Matrix4X4.LookAt(Vector3.Zero, planeNormal, yPositive);
#endif

                AxisAlignedBoundingBox bounds = meshToLayFlat.GetAxisAlignedBoundingBox(MeshTransforms[indexToLayFlat]);

                Vector3 startingCenter = bounds.Center;
                // move it to the origin so it rotates about it's center
                Matrix4X4 totalTransfrom = Matrix4X4.CreateTranslation(-startingCenter);
                // rotate it
                totalTransfrom *= partLevelMatrix;
                MeshTransforms[indexToLayFlat] *= totalTransfrom;
                // find the new center
                bounds = SelectedMesh.GetAxisAlignedBoundingBox(MeshTransforms[indexToLayFlat]);
                // and shift it back so the new center is where the old center was
                MeshTransforms[indexToLayFlat] *= Matrix4X4.CreateTranslation(startingCenter - bounds.Center);
                PlatingHelper.PlaceMeshOnBed(Meshes, MeshTransforms, SelectedMeshIndex, false);
                saveButtons.Visible = true;
                Invalidate();
            }
        }
    }
}

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

using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.PolygonMesh;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class View3DWidget
	{
		private void CreateSelectionData()
		{
			string makingCopyLabel = LocalizedString.Get("Preparing Meshes");
			string makingCopyLabelFull = string.Format("{0}:", makingCopyLabel);
			processingProgressControl.ProcessType = makingCopyLabelFull;

			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			PushMeshGroupDataToAsynchLists(TraceInfoOpperation.DONT_COPY);

			asynchPlatingDatas.Clear();
			double ratioPerMeshGroup = 1.0 / asynchMeshGroups.Count;
			double currentRatioDone = 0;
			for (int i = 0; i < asynchMeshGroups.Count; i++)
			{
				PlatingMeshGroupData newInfo = new PlatingMeshGroupData();
				asynchPlatingDatas.Add(newInfo);

				MeshGroup meshGroup = asynchMeshGroups[i];

				// create the selection info
				PlatingHelper.CreateITraceableForMeshGroup(asynchPlatingDatas, asynchMeshGroups, i, (double progress0To1, string processingState, out bool continueProcessing) =>
				{
					ReportProgressChanged(progress0To1, processingState, out continueProcessing);
				});

				currentRatioDone += ratioPerMeshGroup;
			}

			bool continueProcessing2;
			ReportProgressChanged(1, "Creating GL Data", out continueProcessing2);
			meshViewerWidget.CreateGlDataForMeshes(asynchMeshGroups);
		}

		private async void EnterEditAndCreateSelectionData()
		{
			if (enterEditButtonsContainer.Visible == true)
			{
				enterEditButtonsContainer.Visible = false;
			}

			if (MeshGroups.Count > 0)
			{
				processingProgressControl.Visible = true;
				LockEditControls();
				viewIsInEditModePreLock = true;

				await Task.Run((System.Action)CreateSelectionData);

				if (WidgetHasBeenClosed)
				{
					return;
				}
				// remove the original mesh and replace it with these new meshes
				PullMeshGroupDataFromAsynchLists();

				SelectedMeshGroupIndex = 0;
				buttonRightPanel.Visible = true;
				UnlockEditControls();
				viewControls3D.partSelectButton.ClickButton(null);

				Invalidate();

				if (DoAddFileAfterCreatingEditData)
				{
					FileDialog.OpenFileDialog(
						new OpenFileDialogParams(ApplicationSettings.OpenDesignFileParams, multiSelect: true),
						(openParams) =>
						{
							LoadAndAddPartsToPlate(openParams.FileNames);
						});
					DoAddFileAfterCreatingEditData = false;
				}
				else if (pendingPartsToLoad.Count > 0)
				{
					LoadAndAddPartsToPlate(pendingPartsToLoad.ToArray());
				}
			}
		}
	}
}
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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.PolygonMesh;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PrintLibrary
{

	public interface IClickable
	{
		event EventHandler Click;
	}

	public class LibraryRowItemPart : LibraryRowItem
	{
		public bool isActivePrint = false;
		LibraryProvider libraryProvider;
		public int ItemIndex { get; private set; }
		double thumbnailWidth = 0;

		public PrintItemWrapper printItemInstance = null;

		private ExportPrintItemWindow exportingWindow;
		private PartPreviewMainWindow viewingWindow;

		public LibraryRowItemPart(LibraryProvider libraryProvider, int itemIndex, LibraryDataView libraryDataView, GuiWidget thumbnailWidget)
			: base(libraryDataView, thumbnailWidget)
		{
			thumbnailWidth = thumbnailWidget.Width;
			var widget = thumbnailWidget as IClickable;
			if (widget != null)
			{
				widget.Click += onViewPartClick;
			}

			this.ItemName = libraryProvider.GetPrintItemName(itemIndex);
			this.libraryProvider = libraryProvider;
			this.ItemIndex = itemIndex;

			CreateGuiElements();

			AddLoadingProgressBar();
		}

		public override bool Protected
		{
			get 
			{
				return libraryProvider.IsItemProtected(ItemIndex);
			}
		}

		public async Task<PrintItemWrapper> GetPrintItemWrapperAsync()
		{
			if (printItemInstance == null)
			{
				printItemInstance = await libraryProvider.GetPrintItemWrapperAsync(this.ItemIndex, ReportProgressRatio);
			}

			return printItemInstance;
		}

		void ReportProgressRatio(double progress0To1, string processingState, out bool continueProcessing)
		{
			continueProcessing = true;
			if (progress0To1 == 0)
			{
				processingProgressControl.Visible = false;
			}
			else
			{
				processingProgressControl.Visible = true;
			}

			processingProgressControl.RatioComplete = progress0To1;
		}

		ProgressBar processingProgressControl;
		private void AddLoadingProgressBar()
		{
			processingProgressControl = new ProgressBar(ActiveTheme.Instance.SecondaryAccentColor, (int)(100 * TextWidget.GlobalPointSizeScaleRatio), 5);
			processingProgressControl.BackgroundColor = RGBA_Bytes.White;
			processingProgressControl.VAnchor = VAnchor.ParentBottom;
			processingProgressControl.HAnchor = HAnchor.ParentLeft;
			processingProgressControl.Margin = new BorderDouble(thumbnailWidth + 3, 3, 3, 3);
			processingProgressControl.Visible = false;
			this.AddChild(processingProgressControl);
		}

		public async override void AddToQueue()
		{
			// create a new item that will be only in the queue
			QueueData.Instance.AddItem(await MakeCopyForQueue());
		}

		private async Task<PrintItemWrapper> MakeCopyForQueue()
		{
			var printItemWrapper = await this.GetPrintItemWrapperAsync();

			PrintItem printItemToCopy =  printItemWrapper.PrintItem;
			string fileName = Path.ChangeExtension(Path.GetRandomFileName(), Path.GetExtension(printItemToCopy.FileLocation));
			string newFileLocation = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, fileName);

			File.Copy(printItemToCopy.FileLocation, newFileLocation);

			return new PrintItemWrapper(new PrintItem(printItemToCopy.Name, newFileLocation)
			{
				Protected = printItemToCopy.Protected
			});
		}

		public override void Edit()
		{
			OpenPartViewWindow(PartPreviewWindow.View3DWidget.OpenMode.Editing);
		}

		public async override void Export()
		{
			OpenExportWindow(await this.GetPrintItemWrapperAsync());
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (this.libraryDataView.EditMode)
			{
				selectionCheckBoxContainer.Visible = true;
				rightButtonOverlay.Visible = false;
			}
			else
			{
				selectionCheckBoxContainer.Visible = false;
			}

			base.OnDraw(graphics2D);

			if (this.IsSelectedItem)
			{
				this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
				this.partLabel.TextColor = RGBA_Bytes.White;
				this.selectionCheckBox.TextColor = RGBA_Bytes.White;
			}
			else if (this.IsHoverItem)
			{
				RectangleDouble Bounds = LocalBounds;
				RoundedRect rectBorder = new RoundedRect(Bounds, 0);

				this.BackgroundColor = RGBA_Bytes.White;
				this.partLabel.TextColor = RGBA_Bytes.Black;
				this.selectionCheckBox.TextColor = RGBA_Bytes.Black;

				graphics2D.Render(new Stroke(rectBorder, 3), ActiveTheme.Instance.SecondaryAccentColor);
			}
			else
			{
				this.BackgroundColor = new RGBA_Bytes(255, 255, 255, 255);
				this.partLabel.TextColor = RGBA_Bytes.Black;
				this.selectionCheckBox.TextColor = RGBA_Bytes.Black;
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Clicks == 2)
			{
				UiThread.RunOnIdle(() =>
				{
					openPartView(View3DWidget.OpenMode.Viewing);
				});
			}
			base.OnMouseDown(mouseEvent);
		}

		public async void OpenPartViewWindow(View3DWidget.OpenMode openMode = View3DWidget.OpenMode.Viewing)
		{
			if (viewingWindow == null)
			{
				var printItemWrapper = await this.GetPrintItemWrapperAsync();
				viewingWindow = new PartPreviewMainWindow(printItemWrapper, View3DWidget.AutoRotate.Enabled, openMode);
				viewingWindow.Closed += new EventHandler(PartPreviewMainWindow_Closed);
			}
			else
			{
				viewingWindow.BringToFront();
			}
		}

		public async override void RemoveFromCollection()
		{
			libraryDataView.CurrentLibraryProvider.RemoveItem(ItemIndex);
		}

		protected override SlideWidget GetItemActionButtons()
		{
			SlideWidget buttonContainer = new SlideWidget();
			buttonContainer.VAnchor = VAnchor.ParentBottomTop;

			FlowLayoutWidget buttonFlowContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			buttonFlowContainer.VAnchor = VAnchor.ParentBottomTop;

			TextWidget printLabel = new TextWidget("Print".Localize());
			printLabel.TextColor = RGBA_Bytes.White;
			printLabel.VAnchor = VAnchor.ParentCenter;
			printLabel.HAnchor = HAnchor.ParentCenter;

			FatFlatClickWidget printButton = new FatFlatClickWidget(printLabel);
			printButton.VAnchor = VAnchor.ParentBottomTop;
			printButton.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
			printButton.Width = 100;
			printButton.Click += printButton_Click;
			// HACK: No clear immediate workaround beyond this
			printButton.Click += (s, e) => buttonContainer.SlideOut();

			TextWidget viewButtonLabel = new TextWidget("View".Localize());
			viewButtonLabel.TextColor = RGBA_Bytes.White;
			viewButtonLabel.VAnchor = VAnchor.ParentCenter;
			viewButtonLabel.HAnchor = HAnchor.ParentCenter;

			FatFlatClickWidget viewButton = new FatFlatClickWidget(viewButtonLabel);
			viewButton.VAnchor = VAnchor.ParentBottomTop;
			viewButton.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
			viewButton.Width = 100;
			viewButton.Click += onViewPartClick;

			buttonFlowContainer.AddChild(viewButton);
			buttonFlowContainer.AddChild(printButton);

			buttonContainer.AddChild(buttonFlowContainer);
			buttonContainer.Width = 200;

			return buttonContainer;
		}

		private async void printButton_Click(object sender, EventArgs e)
		{
			var newItem = await MakeCopyForQueue();

			if (!PrinterCommunication.PrinterConnectionAndCommunication.Instance.PrintIsActive)
			{
				QueueData.Instance.AddItem(newItem, indexToInsert: 0);
				QueueData.Instance.SelectedIndex = 0;
				PrinterCommunication.PrinterConnectionAndCommunication.Instance.PrintActivePartIfPossible();
			}
			else
			{
				QueueData.Instance.AddItem(newItem);
			}
			this.Invalidate();
		}

		protected async override void RemoveThisFromPrintLibrary()
		{
			// TODO: The LibraryProvider does not need a printitemwrapper to remove an item! Why not an interger like the others?
			libraryDataView.CurrentLibraryProvider.RemoveItem(ItemIndex);
		}

		private void ExportQueueItemWindow_Closed(object sender, EventArgs e)
		{
			exportingWindow = null;
		}

		private void onAddLinkClick(object sender, EventArgs e)
		{
		}

		private void onConfirmRemove(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				libraryDataView.RemoveChild(this);
			}
		}

		private void onLibraryItemClick(object sender, EventArgs e)
		{
			if (this.libraryDataView.EditMode == false)
			{
				//UiThread.RunOnIdle((state) =>
				//{
				//    openPartView(state);
				//});
			}
			else
			{
				if (this.IsSelectedItem == false)
				{
					this.selectionCheckBox.Checked = true;
					libraryDataView.SelectedItems.Add(this);
				}
				else
				{
					this.selectionCheckBox.Checked = false;
					libraryDataView.SelectedItems.Remove(this);
				}
			}
		}

		private void onOpenPartViewClick(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() => openPartView());
		}

		private void onRemoveLinkClick(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(RemoveThisFromPrintLibrary);
		}

		private void onThemeChanged(object sender, EventArgs e)
		{
			//Set background and text color to new theme
			this.Invalidate();
		}

		private void onViewPartClick(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				this.rightButtonOverlay.SlideOut();
				openPartView(View3DWidget.OpenMode.Viewing);
			});
		}

		private async void OpenExportWindow()
		{
			if (exportingWindow == null)
			{
				exportingWindow = new ExportPrintItemWindow(await this.GetPrintItemWrapperAsync());
				exportingWindow.Closed += new EventHandler(ExportQueueItemWindow_Closed);
				exportingWindow.ShowAsSystemWindow();
			}
			else
			{
				exportingWindow.BringToFront();
			}
		}

		private void OpenExportWindow(PrintItemWrapper printItem)
		{
			if (exportingWindow == null)
			{
				exportingWindow = new ExportPrintItemWindow(printItem);
				exportingWindow.Closed += new EventHandler(ExportQueueItemWindow_Closed);
				exportingWindow.ShowAsSystemWindow();
			}
			else
			{
				exportingWindow.BringToFront();
			}
		}

		private async void openPartView(View3DWidget.OpenMode openMode = View3DWidget.OpenMode.Viewing)
		{
			var printItemWrapper = await this.GetPrintItemWrapperAsync();

			string pathAndFile = printItemWrapper.FileLocation;
			if (File.Exists(pathAndFile))
			{
				OpenPartViewWindow(openMode);
			}
			else
			{
				string message = String.Format("Cannot find\n'{0}'.\nWould you like to remove it from the library?", pathAndFile);
				StyledMessageBox.ShowMessageBox(null, message, "Item not found", StyledMessageBox.MessageType.YES_NO);
			}
		}

		private void PartPreviewMainWindow_Closed(object sender, EventArgs e)
		{
			viewingWindow = null;
		}

		private void selectionCheckBox_CheckedStateChanged(object sender, EventArgs e)
		{
			if (selectionCheckBox.Checked == true)
			{
				libraryDataView.SelectedItems.Add(this);
			}
			else
			{
				libraryDataView.SelectedItems.Remove(this);
			}
		}

		private void SetDisplayAttributes()
		{
			//this.VAnchor = Agg.UI.VAnchor.FitToChildren;
			this.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			if (ActiveTheme.Instance.DisplayMode == ActiveTheme.ApplicationDisplayType.Touchscreen)
			{
				this.Height = 65;
			}
			else
			{
				this.Height = 50;
			}

			this.Padding = new BorderDouble(0);
			this.Margin = new BorderDouble(6, 0, 6, 6);
		}
	}
}
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

using MatterHackers.Localizations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.VersionManagement
{
	public class ResponseErrorEventArgs : EventArgs
	{
		public JsonResponseDictionary ResponseValues { get; set; }
	}

	public class ResponseSuccessEventArgs<ResponseType> : EventArgs
	{
		public ResponseType ResponseItem { get; set; }
	}

	public class WebRequestBase<ResponseType> where ResponseType : class
	{
		protected Dictionary<string, string> requestValues;
		protected string uri;
		public WebRequestBase()
		{
			requestValues = new Dictionary<string, string>();
		}

		public event EventHandler RequestComplete;

		public event EventHandler<ResponseErrorEventArgs> RequestFailed;

		public event EventHandler<ResponseSuccessEventArgs<ResponseType>> RequestSucceeded;
		public static void Request(string requestUrl, string[] requestStringPairs)
		{
			WebRequestBase<ResponseType> tempRequest = new WebRequestBase<ResponseType>();

			tempRequest.uri = requestUrl;
			for (int i = 0; i < requestStringPairs.Length; i += 2)
			{
				tempRequest.requestValues[requestStringPairs[i]] = requestStringPairs[i + 1];
			}

			tempRequest.Request();
		}

		public virtual void ProcessErrorResponse(JsonResponseDictionary responseValues)
		{
			string errorMessage = responseValues.get("ErrorMessage");
			if (errorMessage != null)
			{
				Console.WriteLine(string.Format("Request Failed: {0}", errorMessage));
			}
			else
			{
				Console.WriteLine(string.Format("Request Failed: Unknown Reason"));
			}
		}

		public async void Request()
		{
			await Task.Run((Action)SendRequest);
		}

		//This gets called after failure or success
		protected void OnRequestComplete()
		{
			if (RequestComplete != null)
			{
				RequestComplete(this, null);
			}
		}

		protected void OnRequestFailed(JsonResponseDictionary responseValues)
		{
			if (RequestFailed != null)
			{
				RequestFailed(this, new ResponseErrorEventArgs() { ResponseValues = responseValues });
			}
		}

		protected void OnRequestSuceeded(ResponseType responseItem)
		{
			if (RequestSucceeded != null)
			{
				RequestSucceeded(this, new ResponseSuccessEventArgs<ResponseType>() { ResponseItem = responseItem });
			}
		}
		protected void SendRequest()
		{
			RequestManager requestManager = new RequestManager();
			string jsonToSend = JsonConvert.SerializeObject(requestValues);

			System.Diagnostics.Trace.Write(string.Format("ServiceRequest: {0}\r\n  {1}\r\n", uri, string.Join("\r\n\t", jsonToSend.Split(','))));

			requestManager.SendPOSTRequest(uri, jsonToSend, "", "", false);

			ResponseType responseItem = null;
			JsonResponseDictionary errorResults = null;

			if (requestManager.LastResponse != null)
			{
				try
				{
					responseItem = JsonConvert.DeserializeObject<ResponseType>(requestManager.LastResponse);
				}
				catch
				{
					errorResults = JsonConvert.DeserializeObject<JsonResponseDictionary>(requestManager.LastResponse);
				}
			}

			if (responseItem != null)
			{
				OnRequestSuceeded(responseItem);
			}
			else
			{
				OnRequestFailed(errorResults);
			}

			OnRequestComplete();
		}
	}

	public class WebRequestBase
	{
		protected Dictionary<string, string> requestValues;
		protected string uri;
		public WebRequestBase()
		{
			requestValues = new Dictionary<string, string>();
		}

		public event EventHandler RequestComplete;

		public event EventHandler<ResponseErrorEventArgs> RequestFailed;

		public event EventHandler RequestSucceeded;
		public static void Request(string requestUrl, string[] requestStringPairs)
		{
			WebRequestBase tempRequest = new WebRequestBase();

			tempRequest.uri = requestUrl;
			for (int i = 0; i < requestStringPairs.Length; i += 2)
			{
				tempRequest.requestValues[requestStringPairs[i]] = requestStringPairs[i + 1];
			}

			tempRequest.Request();
		}

		public virtual void ProcessErrorResponse(JsonResponseDictionary responseValues)
		{
			string errorMessage = responseValues.get("ErrorMessage");
			if (errorMessage != null)
			{
				Console.WriteLine(string.Format("Request Failed: {0}", errorMessage));
			}
			else
			{
				Console.WriteLine(string.Format("Request Failed: Unknown Reason"));
			}
		}

		public virtual void ProcessSuccessResponse(JsonResponseDictionary responseValues)
		{
			//Do Stuff
		}

		public virtual void Request()
		{
			BackgroundWorker doRequestWorker = new BackgroundWorker();
			doRequestWorker.DoWork += new DoWorkEventHandler(SendRequest);
			doRequestWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(ProcessResponse);
			doRequestWorker.RunWorkerAsync();
		}

		protected virtual string getJsonToSend()
		{
			return SerializeObject(requestValues);
		}

		//This gets called after failure or success
		protected void OnRequestComplete()
		{
			if (RequestComplete != null)
			{
				RequestComplete(this, null);
			}
		}

		protected void OnRequestFailed(JsonResponseDictionary responseValues)
		{
			if (RequestFailed != null)
			{
				RequestFailed(this, new ResponseErrorEventArgs() { ResponseValues = responseValues });
			}
		}

		protected void OnRequestSuceeded()
		{
			if (RequestSucceeded != null)
			{
				RequestSucceeded(this, null);
			}
		}
		protected virtual void ProcessResponse(object sender, RunWorkerCompletedEventArgs e)
		{
			JsonResponseDictionary responseValues = e.Result as JsonResponseDictionary;
			if (responseValues != null)
			{
				string requestSuccessStatus = responseValues.get("Status");
				if (responseValues != null && requestSuccessStatus != null && requestSuccessStatus == "success")
				{
					ProcessSuccessResponse(responseValues);
					OnRequestSuceeded();
				}
				else
				{
					ProcessErrorResponse(responseValues);
					OnRequestFailed(responseValues);
				}

				OnRequestComplete();
			}
			else
			{
				// Don't do anything, there was no respones.
			}
		}

		protected virtual void SendRequest(object sender, DoWorkEventArgs e)
		{
			JsonResponseDictionary responseValues;

			RequestManager requestManager = new RequestManager();
			string jsonToSend = getJsonToSend();

			System.Diagnostics.Trace.Write(string.Format("ServiceRequest: {0}\r\n  {1}\r\n", uri, string.Join("\r\n\t", jsonToSend.Split(','))));

			requestManager.SendPOSTRequest(uri, jsonToSend, "", "", false);
			if (requestManager.LastResponse == null)
			{
				responseValues = new JsonResponseDictionary();
				responseValues["Status"] = "error";
				responseValues["ErrorMessage"] = "Unable to connect to server";
				responseValues["ErrorCode"] = "00";
			}
			else
			{
				try
				{
					responseValues = JsonConvert.DeserializeObject<JsonResponseDictionary>(requestManager.LastResponse);

					string errorMessage;
					if (responseValues.TryGetValue("ErrorMessage", out errorMessage) && errorMessage.IndexOf("expired session") != -1)
					{
						// TODO: Map more error conditions (beyond just session expired) to CredentialsInvalid status
						UserSettings.Instance.set("CredentialsInvalid", "true");
						UserSettings.Instance.set("CredentialsInvalidReason", "Session Expired".Localize());

						// Notify connection status changed and now invalid
						ApplicationController.Instance.ChangeCloudSyncStatus(false);
					}
				}
				catch
				{
					responseValues = new JsonResponseDictionary();
					responseValues["Status"] = "error";
					responseValues["ErrorMessage"] = "Unexpected response";
					responseValues["ErrorCode"] = "01";
				}
			}

			e.Result = responseValues;
		}

		protected string SerializeObject(object requestObject)
		{
			return Newtonsoft.Json.JsonConvert.SerializeObject(requestObject);
		}
	}
}
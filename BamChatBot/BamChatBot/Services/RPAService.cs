using BamChatBot.Models;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Drawing;
using System.Threading.Tasks;

namespace BamChatBot.Services
{
	public class RPAService
	{
		public string UserName { get; private set; }
		public string Pass { get; private set; }
		public RPAService()
		{
			UserName = "integration_bamchatbot";
			Pass = "dc4e1953dbf1c45897b930cf9d961992";
		}

		internal string ClearResponse(string result)
		{
			//get rid of {"result":} wrapper from response
			result = result.Remove(0, 1);
			result = result.Substring(result.IndexOf(':') + 1);
			result = result.Remove(result.LastIndexOf('}'));
			return result;
		}

		internal string GetInstanceName()
		{
			var instance = "bayview";
			return instance;
		}

		internal void AddAuthorization(HttpClient client)
		{
			//get authorized user
			//userName
			//get credentials

			var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", this.UserName, this.Pass)));
			//add basic authorization
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
		}

		internal APIResponse GetApiResult(string endPoint, string userId)
		{
			var apiResponse = new APIResponse();
			var user = new User();
			//get user first name  
			var apiPath = GetApiPath();
			var url = apiPath + endPoint;
			var userName = user.GetLoginUserName();
			var urlParameters = "?sys_user=" + userId;

			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);
			//get credentials

			//add basic authorization
			AddAuthorization(client);

			// Add an Accept header for JSON format.
			client.DefaultRequestHeaders.Accept.Add(
			new MediaTypeWithQualityHeaderValue("application/json"));

			try
			{
				// List data response.
				HttpResponseMessage response = client.GetAsync(urlParameters).Result;

				var obj = response.Content.ReadAsStringAsync();
				//get rid of {"result":} wrapper from response
				var result = ClearResponse(obj.Result);
				apiResponse = new APIResponse
				{
					Content = result,
					IsSuccess = response.IsSuccessStatusCode
				};
			}
			catch (Exception ex)
			{
				
			}

			return apiResponse;
		}

		internal APIResponse StartProcessWithParams(ProcessModel processSelected)
		{
			var apiPath = GetApiPath();
			var url = apiPath + "startProcessWithParams";
			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);
			// var urlParameters = "?user=" + data.UserId + "&sys_id=" + data.Sys_id;
			//add basic authorization
			AddAuthorization(client);
			var content = new StringContent(JsonConvert.SerializeObject(processSelected), Encoding.UTF8, "application/json");
			var response = client.PostAsync(url, content).Result;
			var obj = response.Content.ReadAsStringAsync();

			//get rid of {"result":} wrapper from response
			var result = ClearResponse(obj.Result);
			var apiResponse = new APIResponse
			{
				Content = result,
				IsSuccess = response.IsSuccessStatusCode
			};

			return apiResponse;
		}

		internal void DeactivatedConversationFlow(string sys_Id, string ConversationId)
		{
			//get user first name  
			var apiPath = GetApiPath();
			var url = apiPath + "putConversationFlow";
			var urlParameters = "?flow_sys_id=" + sys_Id + "&conv_sys_id=" + ConversationId;

			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);
			//get credentials

			//add basic authorization
			AddAuthorization(client);

			// Add an Accept header for JSON format.
			client.DefaultRequestHeaders.Accept.Add(
			new MediaTypeWithQualityHeaderValue("application/json"));

			// List data response.
			HttpResponseMessage response = client.GetAsync(urlParameters).Result;

		}

		internal APIResponse GetConversationFlowInputs(string convId, string releaseId)
		{
			var apiPath = GetApiPath();
			var url = apiPath + "getProcessParams";
			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);
			var urlParameters = "?release_sys_id=" + releaseId + "&conv_sys_id=" + convId;
			//add basic authorization
			AddAuthorization(client);
			// Add an Accept header for JSON format.
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			HttpResponseMessage response = client.GetAsync(urlParameters).Result;
			var obj = response.Content.ReadAsStringAsync();
			//get rid of {"result":} wrapper from response
			var result = ClearResponse(obj.Result);
			var apiResponse = new APIResponse
			{
				Content = result,
				IsSuccess = response.IsSuccessStatusCode
			};

			return apiResponse;
		}

		private string GetApiPath()
		{
			var instance = GetInstanceName();
			var apiPath = "https://" + instance + ".service-now.com/api/baam/bam_chat_bot/";
			return apiPath;
		}

		internal APIResponse StartProcess(ProcessModel data, string conversationId)
		{
			var apiResponse = new APIResponse();
			var apiPath = GetApiPath();
			var url = apiPath + "startProcess";
			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);
			var urlParameters = "?user=" + data.UserId + "&sys_id=" + data.Sys_id + "&conversationId=" + conversationId;
			//add basic authorization
			AddAuthorization(client);

			// Add an Accept header for JSON format.
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			try
			{
				HttpResponseMessage response = client.GetAsync(urlParameters).Result;
				var obj = response.Content.ReadAsStringAsync();
				//get rid of {"result":} wrapper from response
				var result = ClearResponse(obj.Result);
				apiResponse = new APIResponse
				{
					Content = result,
					IsSuccess = response.IsSuccessStatusCode
				};
			}
			catch (Exception ex)
			{
				apiResponse.Error = ex.Message;
			}

			return apiResponse;

		}

		internal void DeleteConversationFlowInputs(string conversationId)
		{
			var apiResponse = new APIResponse();
			var apiPath = GetApiPath();
			var url = apiPath + "deleteConversationFlowInputs";
			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);
			var urlParameters = "?sys_id=" + conversationId;
			//add basic authorization
			AddAuthorization(client);
			// Add an Accept header for JSON format.
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			try
			{
				HttpResponseMessage response = client.GetAsync(urlParameters).Result;
				var obj = response.Content.ReadAsStringAsync();
			}
			catch (Exception ex)
			{
				apiResponse.Content = ex.Message;
			}

		}

		internal List<Asset> HasAnyAsset(ProcessModel process)
		{
			var assetsWithValueFromChild = new List<Asset>();
			//if any asset has value from child
			foreach (var r in process.Releases)
			{
				foreach (var a in r.assets)
				{
					if (a.ValueFromChild)
					{
						assetsWithValueFromChild.Add(new Asset
						{
							sys_id = a.sys_id,
							PerRobot = a.PerRobot,
							UserId = process.UserId
						});
					}
				}
			}
			return assetsWithValueFromChild;
		}

		internal Incident CreateRPAIncident(ProcessModel processSelected)
		{
			var incident = new Incident();
			var apiPath = GetApiPath();
			var url = apiPath + "createRPAIncident";
			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);
			//add basic authorization
			AddAuthorization(client);
			var content = new StringContent(JsonConvert.SerializeObject(processSelected), Encoding.UTF8, "application/json");
			var response = client.PostAsync(url, content).Result;
			var obj = response.Content.ReadAsStringAsync();
			var result = string.Empty;
			if (!string.IsNullOrEmpty(obj.Result))
			{
			 //get rid of {"result":} wrapper from response
				result = ClearResponse(obj.Result);
				
			}
			if (response.IsSuccessStatusCode)
				incident = JsonConvert.DeserializeObject<Incident>(result);

			return incident;
		}

		internal APIResponse MakeAssetFromChild(List<Asset> assetsWithValueFromChild)
		{
			var apiResponse = new APIResponse();
			var apiPath = GetApiPath();
			var url = apiPath + "getAssetFromChild";
			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);
			//add basic authorization
			AddAuthorization(client);
			var content = new StringContent(JsonConvert.SerializeObject(assetsWithValueFromChild), Encoding.UTF8, "application/json");
			var response = client.PostAsync(url, content).Result;
			var obj = response.Content.ReadAsStringAsync();
			var result = string.Empty;
			if (!string.IsNullOrEmpty(obj.Result))
			{
				//get rid of {"result":} wrapper from response
				result = ClearResponse(obj.Result);
				apiResponse = JsonConvert.DeserializeObject<APIResponse>(result);
			}

			return apiResponse;
		}

		internal void SaveConversationFlowInput(ConversationFlowInput conversationFlowInput)
		{
			var instance = GetInstanceName();
			var url = "https://" + instance + ".service-now.com/api/now/table/u_chatbot_conversation_flow_inputs";
			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);

			//add basic authorization
			AddAuthorization(client);
			var json = JsonConvert.SerializeObject(conversationFlowInput);
			var content = new StringContent(json, Encoding.UTF8, "application/json");
			var response = client.PostAsync(url, content).Result;
			var obj = response.Content.ReadAsStringAsync();
		}

		internal APIResponse UpdateUser(User user, string conversationId)
		{
			var newUser = new User
			{
				u_user = user.u_user,
				u_last_index = user.u_last_index,
				u_conversation_id = conversationId
			};

			var apiPath = GetApiPath();
			var url = apiPath + "updateUser";
			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);
			//add basic authorization
			AddAuthorization(client);
			var content = new StringContent(JsonConvert.SerializeObject(newUser), Encoding.UTF8, "application/json");
			var response = client.PostAsync(url, content).Result;
			var obj = response.Content.ReadAsStringAsync();
			var result = string.Empty;
			if (!string.IsNullOrEmpty(obj.Result))
			{
				//get rid of {"result":} wrapper from response
				result = ClearResponse(obj.Result);
			}

			var apiResponse = new APIResponse
			{
				Content = result,
				IsSuccess = response.IsSuccessStatusCode
			};

			return apiResponse;

		}

		internal APIResponse ProcessStatus(APIRequest data)
		{
			var apiPath = GetApiPath();
			var url = apiPath + "processStatus";
			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);
			// var urlParameters = "?user=" + data.UserId + "&sys_id=" + data.Sys_id;
			//add basic authorization
			AddAuthorization(client);
			var content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
			var response = client.PostAsync(url, content).Result;
			var obj = response.Content.ReadAsStringAsync();

			//get rid of {"result":} wrapper from response
			var result = ClearResponse(obj.Result);
			var apiResponse = new APIResponse
			{
				Content = result,
				IsSuccess = response.IsSuccessStatusCode
			};

			return apiResponse;

		}

		internal APIResponse StopProcess(string processId, int strategy)
		{
			var apiResponse = new APIResponse();
			var apiPath = GetApiPath();
			var url = apiPath + "stopProcess";
			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);
			var urlParameters = "?sys_id=" + processId+ "&strategy="+ strategy;
			//add basic authorization
			AddAuthorization(client);
			// Add an Accept header for JSON format.
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			try
			{
				HttpResponseMessage response = client.GetAsync(urlParameters).Result;
				var obj = response.Content.ReadAsStringAsync();

				apiResponse = new APIResponse
				{
					Content = obj.Result,
					IsSuccess = response.IsSuccessStatusCode
				};
				if (!apiResponse.IsSuccess)
				{
					//get rid of {"result":} wrapper from response
					var result = ClearResponse(obj.Result);
					apiResponse = JsonConvert.DeserializeObject<APIResponse>(result);
				}

			}
			catch (Exception ex)
			{

				apiResponse.Content = ex.Message;
			}
			return apiResponse;
		}
		internal APIResponse CancelQueuedProcess(ProcessModel process)
		{
			var apiResponse = new APIResponse();
			var apiPath = GetApiPath();
			var url = apiPath + "cancelQueuedProcess";
			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);
			//add basic authorization
			AddAuthorization(client);
			try
			{
				var content = new StringContent(JsonConvert.SerializeObject(process), Encoding.UTF8, "application/json");
				var response = client.PostAsync(url, content).Result;
				var obj = response.Content.ReadAsStringAsync();
				apiResponse = new APIResponse
				{
					Content = obj.Result,
					IsSuccess = response.IsSuccessStatusCode
				};
				if (!apiResponse.IsSuccess)
				{
					//get rid of {"result":} wrapper from response
					var result = ClearResponse(obj.Result);
					apiResponse = JsonConvert.DeserializeObject<APIResponse>(result);
				}
			}
			catch (Exception ex)
			{
				apiResponse.Content = ex.Message;
			}
			
			return apiResponse;
		}

		internal void SaveUser(User user)
		{
			var apiPath = GetApiPath();
			var url = apiPath + "saveUser";
			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);
			//add basic authorization
			AddAuthorization(client);
			try
			{
				var content = new StringContent(JsonConvert.SerializeObject(user), Encoding.UTF8, "application/json");
				var response = client.PostAsync(url, content).Result;
			}
			catch (Exception ex)
			{
			}

		}

		internal Choice GetMainMenuOption()
		{
			var value = JsonConvert.SerializeObject(new PromptOption { Id = "mainMenu", Value = "Main Menu" });
			return new Choice
			{
				Value = "Main Menu",
				Action = new CardAction(ActionTypes.PostBack, "Main Menu", null, "Main Menu", "Main Menu", value: value, null)
			};
		}

		internal APIResponse GetUser(string conversationId)
		{
			var apiPath = GetApiPath();
			var url = apiPath + "getUser";
			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);
			var urlParameters = "?conversation_sys_id=" + conversationId;
			//add basic authorization
			AddAuthorization(client);
			// Add an Accept header for JSON format.
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			HttpResponseMessage response = client.GetAsync(urlParameters).Result;
			var obj = response.Content.ReadAsStringAsync();

			var result = ClearResponse(obj.Result);
			var apiResponse = new APIResponse
			{
				Content = result,
				IsSuccess = response.IsSuccessStatusCode
			};

			return apiResponse;

		}

		internal void SaveConversationFlow(ProcessModel ProcessSelected, string conversationId)
		{
			var count = 0;
			foreach (var r in ProcessSelected.Releases)
			{
				if (r.parameters_required)
				{
					foreach (var p in r.parameters)
					{
						if (p.required)
						{
							if (p.parmType.Contains("Object"))
							{
								foreach (var o in p.obj)
								{
									if (o.parmType.Contains("String[]"))
									{
										var repeatedArr = new List<ProcessParameters>();
										foreach (var a in o.array)
										{
											var parmName = a.parmName;
											if (a.parmName != o.parmName)
											{
												parmName = o.parmName + '[' + a.parmName + ']';
											}
											var _conversationFlow = new ConversationFlow
											{
												u_conversation_id = conversationId,
												u_release_id = r.sys_id,
												u_param_name = parmName,
												u_last_question_index = count,
												u_type = a.parmType,
												u_active = true,
												u_parent_id = a.parentId,
												u_is_object = true

											};

											SendConversationFlow(_conversationFlow);
											count++;

											if (a.length > 1)
											{
												var lenght = 1;
												while (lenght < a.length)
												{
													_conversationFlow = new ConversationFlow
													{
														u_conversation_id = conversationId,
														u_release_id = r.sys_id,
														u_param_name = parmName,
														u_last_question_index = count,
														u_type = a.parmType,
														u_active = true,
														u_parent_id = a.parentId,
														u_is_object = true
													};

													SendConversationFlow(_conversationFlow);
													count++;
													lenght++;
													repeatedArr.Add(a);
												}
											}
										}
										if (repeatedArr.Count > 0)
										{
											foreach (var ra in repeatedArr)
											{
												o.array.Add(ra);
											}
										}
									}
									else
									{
										var _conversationFlow = new ConversationFlow
										{
											u_conversation_id = conversationId,
											u_release_id = r.sys_id,
											u_param_name = o.parmName,
											u_last_question_index = count,
											u_type = o.parmType,
											u_active = true,
											u_parent_id = o.parentId,
											u_is_object = true
										};

										SendConversationFlow(_conversationFlow);
										count++;
									}

								}

							}
							else if (p.parmType.Contains("String[]"))
							{
								var repeatedArr = new List<ProcessParameters>();
								foreach (var a in p.array)
								{
									var parmName = a.parmName;
									if (a.parmName != p.parmName)
									{
										parmName = p.parmName + '[' + a.parmName + ']';
									}
									var _conversationFlow = new ConversationFlow
									{
										u_conversation_id = conversationId,
										u_release_id = r.sys_id,
										u_param_name = parmName,
										u_last_question_index = count,
										u_type = a.parmType,
										u_active = true,
										u_parent_id = a.parentId,
										u_is_array = true,

									};

									SendConversationFlow(_conversationFlow);
									count++;
									if (a.length > 1)
									{
										var lenght = 1;
										while (lenght < a.length)
										{
											_conversationFlow = new ConversationFlow
											{
												u_conversation_id = conversationId,
												u_release_id = r.sys_id,
												u_param_name = parmName,
												u_last_question_index = count,
												u_type = a.parmType,
												u_active = true,
												u_parent_id = a.parentId,
												u_is_array = true
											};

											SendConversationFlow(_conversationFlow);
											count++;
											lenght++;
											repeatedArr.Add(a);
										}
									}
								}
								if (repeatedArr.Count > 0)
								{
									foreach (var ra in repeatedArr)
									{
										p.array.Add(ra);
									}
								}
							}
							else
							{
								//save the params
								var _conversationFlow = new ConversationFlow
								{
									u_conversation_id = conversationId,
									u_release_id = r.sys_id,
									u_param_name = p.parmName,
									u_last_question_index = count,
									u_type = p.parmType,
									u_active = true,
									u_parent_id = p.parentId
								};

								SendConversationFlow(_conversationFlow);
								count++;
							}
						}

					}
				}
			}


		}

		internal void SendConversationFlow(ConversationFlow _conversationFlow)
		{
			var instance = GetInstanceName();
			var url = "https://" + instance + ".service-now.com/api/now/table/u_chatbot_conversation_flow";
			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);

			//add basic authorization
			AddAuthorization(client);
			var json = JsonConvert.SerializeObject(_conversationFlow);
			var content = new StringContent(json, Encoding.UTF8, "application/json");
			var response = client.PostAsync(url, content).Result;
			var obj = response.Content.ReadAsStringAsync();
		}

		internal APIResponse GetConversationFlow(string conversationId)
		{
			var apiPath = GetApiPath();
			var url = apiPath + "getConversationFlow";
			HttpClient client = new HttpClient();
			client.BaseAddress = new Uri(url);
			var urlParameters = "?conversationId=" + conversationId;
			//add basic authorization
			AddAuthorization(client);
			// Add an Accept header for JSON format.
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			HttpResponseMessage response = client.GetAsync(urlParameters).Result;
			var obj = response.Content.ReadAsStringAsync();
			//get rid of {"result":} wrapper from response
			var result = ClearResponse(obj.Result);
			var apiResponse = new APIResponse
			{
				Content = result,
				IsSuccess = response.IsSuccessStatusCode
			};

			return apiResponse;
		}

		internal TopProcess GetListOfProcess(List<ProcessModel> processes, int lastIdx)
		{
			var tempProcesses = new List<ProcessModel>();
			var maxProcessDisplayed = 10;

			//initialize list of Choices
			var choices = new List<Choice>();



			if (processes.Count > maxProcessDisplayed)
			{
				var count = 0;
				for (var p = lastIdx; p < processes.Count; p++)
				{
					if (count == maxProcessDisplayed)
					{
						lastIdx = p;
						break;
					}
					else if (p == processes.Count - 1)
					{
						lastIdx = p;
					}
					tempProcesses.Add(processes[p]);
					count++;
				}


				if (lastIdx != processes.Count - 1)
				{
					tempProcesses.Add(new ProcessModel
					{
						Sys_id = "Load_More",
						Name = "Load More"
					});
				}

			}
			else
			{
				tempProcesses = processes;
			}

			foreach (var process in tempProcesses)
			{
				var value = process.Sys_id;
				if (process.Name.Contains("[Queued]"))
				{
					value += "[Queued]";
				}
				var choiceValue = JsonConvert.SerializeObject(new PromptOption { Id = "availableProcesses", Value = value });
				var name = process.Name;
				if (process.Sys_id != "Load_More")
				{
					//name+= " [" + process.LastRun.State + "]";
				}
				
				choices.Add(new Choice
				{
					Value = value,
					Action = new CardAction(ActionTypes.PostBack, name, null, process.Name, process.Name, value: choiceValue, null)
				});
			}

			return new TopProcess
			{
				Choices = choices,
				LastIndex = lastIdx
			};
		}

		internal ProcessModel GetSelectedProcess(List<ProcessModel> processes, string value)
		{

			var processSelected = new ProcessModel();
			foreach (var item in processes)
			{
				if (value.Contains("[Queued]"))
				{
					var id = value.Replace("[Queued]", "");
					if (id.Trim() == item.Sys_id && item.Name.Contains("[Queued]"))
					{
						processSelected = item;
					}
				}
				else if (value.Trim() == item.Sys_id && !item.Name.Contains("[Queued]"))
				{
					processSelected = item;
				}
			}
			return processSelected;
		}

		internal Choice GetRPASupportOption()
		{
			var value = JsonConvert.SerializeObject(new PromptOption { Id = "rpaSuport", Value = "RPASupport@bayview.com" });
			return new Choice
			{
				Value = "rpaSupport",//RPASupport@bayview.com
				Action = new CardAction(ActionTypes.PostBack, "**Contact RPA Support**", null, "**Contact RPA Support**", "openEmail", value: value, null)

			};
		}

		internal List<Choice> GetConfirmChoices()
		{
			var yesOption = JsonConvert.SerializeObject(new PromptOption { Id = "Confirm", Value = "Yes" });
			var noOption = JsonConvert.SerializeObject(new PromptOption { Id = "Confirm", Value = "No" });
			var choices = new List<Choice> {
						new Choice
							{
								Value = "Yes",
								Action = new CardAction(ActionTypes.PostBack, "Yes", null, "Yes", "Yes", value: yesOption, null)
							},
						new Choice
							{
								Value = "No",
								Action = new CardAction(ActionTypes.PostBack, "No", null, "No", "No", value: noOption, null)
							} };
			return choices;
		}
	}
}

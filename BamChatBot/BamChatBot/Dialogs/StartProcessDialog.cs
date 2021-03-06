using BamChatBot.Models;
using BamChatBot.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BamChatBot.Dialogs
{
	public class StartProcessDialog : CancelAndHelpDialog
	{

		public readonly IStatePropertyAccessor<ConversationFlow> _conversationFlow;
		public StartProcessDialog(IStatePropertyAccessor<ConversationFlow> conversationFlow)
			: base(nameof(StartProcessDialog))
		{
			_conversationFlow = conversationFlow;
			AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
			AddDialog(new StartProcessErrorDialog());
			AddDialog(new ParametersProcessDialog());
			AddDialog(new StartProcessSharedDialog());
			AddDialog(new RobotsDialog());
			var Steps = new WaterfallStep[]
			{
				IntroStepAsync,
				ShowProcessStepAsync,
				ConfirmStartProcessStepAsync,
				StartProcessStepAsync,
				GoServicenowStepAsync
			};
			AddDialog(new WaterfallDialog(nameof(WaterfallDialog), Steps));

			// The initial child Dialog to run.
			InitialDialogId = nameof(WaterfallDialog);

		}


		private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			var processDetails = (ProcessDetails)stepContext.Options;
			new User().GetUserProcess(processDetails);
			return await stepContext.NextAsync(processDetails, cancellationToken);
		}

		private async Task<DialogTurnResult> ShowProcessStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{

			var processDetails = (ProcessDetails)stepContext.Options;
			var processes = processDetails.Processes;
			var text = "Here are your available processes.";
			if (processDetails.LoadMore)
			{
				text = string.Empty;
				processDetails.LoadMore = false;
			}
			if (processes.Count > 0)
			{
				var rpaService = new RPAService();
				var response = rpaService.GetUser(stepContext.Context.Activity.Conversation.Id);
				var user = new List<User>();
				if (response.IsSuccess)
					user = JsonConvert.DeserializeObject<List<User>>(response.Content);
				//var _user = await _userAccessor.GetAsync(stepContext.Context, () => new User(), cancellationToken);
				var result = rpaService.GetListOfProcess(processes, Convert.ToInt32(user[0].u_last_index));
				var choices = result.Choices;
				var rpaSupportChoice = rpaService.GetRPASupportOption();
				choices.Add(rpaSupportChoice);
				//save index
				user[0].u_last_index = result.LastIndex.ToString();
				rpaService.UpdateUser(user[0], stepContext.Context.Activity.Conversation.Id);
				//_user.u_last_index = result.LastIndex;
				//await this._userAccessor.SetAsync(stepContext.Context, _user, cancellationToken);

				return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
				{
					Prompt = (Activity)ChoiceFactory.HeroCard(choices, text + Environment.NewLine + "Click the process you would like to trigger.")
					/*Prompt = MessageFactory.Text(text + Environment.NewLine + "Click the process you would like to trigger."),
					Choices = choices,
					Style = ListStyle.Auto*/
				}, cancellationToken);

			}
			else
			{
				processDetails.Action = "error";
				return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);
			}

		}

		private async Task<DialogTurnResult> ConfirmStartProcessStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			var promptOption = new PromptOption();
			var rpaService = new RPAService();
			var processDetails = (ProcessDetails)stepContext.Options;
			var user = new List<User>();
			var result = stepContext.Result.ToString();
			try
			{
				promptOption = JsonConvert.DeserializeObject<PromptOption>(stepContext.Result.ToString());
			}
			catch (Exception) { }

			if (!string.IsNullOrEmpty(promptOption.Id))
			{
				if (promptOption.Id != "availableProcesses" && promptOption.Id != "rpaSuport")
				{
					processDetails.Action = "pastMenu";
					return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);
				}
				result = promptOption.Value;
			}
			
			var response = rpaService.GetUser(stepContext.Context.Activity.Conversation.Id);
			if (response.IsSuccess)
				user = JsonConvert.DeserializeObject<List<User>>(response.Content);
			//var _user = await _userAccessor.GetAsync(stepContext.Context, () => new User(), cancellationToken);
			if (result.ToLower() == "rpasupport@bayview.com")
			{
				//save index
				user[0].u_last_index = "0";
				rpaService.UpdateUser(user[0], stepContext.Context.Activity.Conversation.Id);
				//_user.u_last_index = 0;
				//await _userAccessor.SetAsync(stepContext.Context, _user, cancellationToken);
				processDetails.Action = string.Empty;
				return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);
			}
			else if (result.ToLower() == "load_more")
			{
				processDetails.LoadMore = true;
				return await stepContext.ReplaceDialogAsync(nameof(StartProcessDialog), processDetails, cancellationToken);
			}
			else
			{
				processDetails.ProcessSelected = rpaService.GetSelectedProcess(processDetails.Processes, result);
				//check if a process was selected, or something was written
				if (!string.IsNullOrEmpty(processDetails.ProcessSelected.Sys_id))
				{
					//save index
					user[0].u_last_index = "0";
					rpaService.UpdateUser(user[0], stepContext.Context.Activity.Conversation.Id);
					//_user.u_last_index = 0;
					//await _userAccessor.SetAsync(stepContext.Context, _user, cancellationToken);

					processDetails.ProcessSelected.StartedBy = "chat_bot";

					var choices = rpaService.GetConfirmChoices();

					return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
					{
						Prompt = (Activity)ChoiceFactory.SuggestedAction(choices, "You have selected " + processDetails.ProcessSelected.Name + ". Would you like to start this process?"),
						/*Prompt = MessageFactory.Text("You have selected " + processDetails.ProcessSelected.Name + ". Start this process?"),
						Choices = ChoiceFactory.ToChoices(new List<string> { "Yes", "No" })*/
					}, cancellationToken);
				}
				else//start main dialog 
				{
					return await stepContext.ReplaceDialogAsync(nameof(MainDialog), null, cancellationToken);
				}
			}

		}

		private async Task<DialogTurnResult> StartProcessStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			var processDetails = (ProcessDetails)stepContext.Options;
			var result = stepContext.Result.ToString();
			var promptOption = new PromptOption();
			try
			{
				promptOption = JsonConvert.DeserializeObject<PromptOption>(stepContext.Result.ToString());
			}
			catch (Exception){}

			if (!string.IsNullOrEmpty(promptOption.Id))
			{
				if (promptOption.Id != "Confirm")
				{
					processDetails.Action = "pastMenu";
					return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);
				}
				result = promptOption.Value;
			}

			if (result.ToLower() == "yes" || result.ToLower() == "y")
			{
				var rpaService = new RPAService();
				//save activity id for when process finish
				var activityId = stepContext.Context.Activity.Id;
				//check if the process can start
				if (processDetails.ProcessSelected.LastRun.State == "Faulted" || processDetails.ProcessSelected.LastRun.State == "Successful" || processDetails.ProcessSelected.LastRun.State == "Stopped" || string.IsNullOrEmpty(processDetails.ProcessSelected.LastRun.State))
				{
					//check if has asset
					var assetsWithValueFromChild = rpaService.HasAnyAsset(processDetails.ProcessSelected);
					if (assetsWithValueFromChild.Count > 0)
					{
						var response = rpaService.MakeAssetFromChild(assetsWithValueFromChild);
						if (response.Body != "Success")
						{
							var choices = new List<Choice>();
							var rpaOption = rpaService.GetMainMenuOption();
							if (response.MissingAsset)
							{
								//create incident
								var incident = rpaService.CreateRPAIncident(processDetails.ProcessSelected);
								var incidentValue = JsonConvert.SerializeObject(new PromptOption { Id = "incident", Value = "bam?id=rpa_request&table=u_robot_incident&sys_id=" + incident.Sys_Id });
								choices = new List<Choice>
								{
                                 new Choice
								 {
									 Value = "bam?id=rpa_request&table=u_robot_incident&sys_id="+ incident.Sys_Id,
									 Action = new CardAction(ActionTypes.PostBack, incident.Number, null, incident.Number, "openUrl", value: incidentValue , null)
								 }
								};
								choices.Add(rpaOption);

								return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
								{
									Prompt = (Activity)ChoiceFactory.SuggestedAction(choices, "Process " + processDetails.ProcessSelected.Name + " requires an asset associated to your user, an incident has been opened to RPA Support." + Environment.NewLine + "Click incident number below to open it")
								}, cancellationToken);
							}
							else
							{
								var assetValue = JsonConvert.SerializeObject(new PromptOption { Id = "asset", Value = "bam?id=rpa_process_assets&process=" + processDetails.ProcessSelected.Sys_id });
								choices = new List<Choice>
								{
								 new Choice
								 {
									 Value = "bam?id=rpa_process_assets&process="+ processDetails.ProcessSelected.Sys_id,
									 Action = new CardAction(ActionTypes.PostBack, "Update Asset", null, "Update Asset", "openUrl", value: assetValue, null)
								 } };
								choices.Add(rpaOption);
								//send the user to SN UI page
								return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
								{
									Prompt = (Activity)ChoiceFactory.SuggestedAction(choices, response.Body + Environment.NewLine + "Please enter them before running the process by pressing Update Asset button below.")
								}, cancellationToken);
							}

						}
					}
					if (processDetails.ProcessSelected.Releases.Any(r => r.robots.Count > 1))
					{
						processDetails.ProcessSelected.FirstBot = true;
						return await stepContext.ReplaceDialogAsync(nameof(RobotsDialog), processDetails, cancellationToken);
					}//check if process need params
					else if (processDetails.ProcessSelected.Releases.Any(r => r.parameters_required == true))
					{
						//set all params for this conversation to false(maybe was interrupted by a notification)
						rpaService.DeactivatedConversationFlow(string.Empty, stepContext.Context.Activity.Conversation.Id);
						rpaService.SaveConversationFlow(processDetails.ProcessSelected, stepContext.Context.Activity.Conversation.Id);
						return await stepContext.ReplaceDialogAsync(nameof(ParametersProcessDialog), processDetails, cancellationToken);
					}
					else
					{
						return await stepContext.ReplaceDialogAsync(nameof(StartProcessSharedDialog), processDetails, cancellationToken);

					}
				}
				else
				{
					processDetails.Action = "error";
					processDetails.Error = "Cannot start " + processDetails.ProcessSelected.Name + " because the process is already running.";
					return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);
				}
			}
			else if (result.ToLower() == "no" || result.ToLower() == "n")//when no is selected
			{
				processDetails.Action = string.Empty;
				return await stepContext.ReplaceDialogAsync(nameof(MainDialog), null, cancellationToken);

			}
			else //when something is typed
			{
				processDetails.Action = string.Empty;
				return await stepContext.ReplaceDialogAsync(nameof(MainDialog), null, cancellationToken);
			}
		}

		private async Task<DialogTurnResult> GoServicenowStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			var rpaService = new RPAService();
			var processDetails = (ProcessDetails)stepContext.Options;
			processDetails.Action = string.Empty;
			var option = stepContext.Result.ToString();
			var promptOption = new PromptOption();
			try
			{
				promptOption = JsonConvert.DeserializeObject<PromptOption>(stepContext.Result.ToString());
			}
			catch (Exception) { }

			if (!string.IsNullOrEmpty(promptOption.Id))
			{
				if (promptOption.Id != "mainMenu" && promptOption.Id != "incident" && promptOption.Id != "asset")
				{
					processDetails.Action = "pastMenu";
					return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);
				}
				option = promptOption.Value;
			}

			if (option.ToLower() == "main menu" || option.ToLower() == "m")
			{
				return await stepContext.ReplaceDialogAsync(nameof(MainDialog), null, cancellationToken);
			}
			else
			{
				return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);
			}
		}

	}
}

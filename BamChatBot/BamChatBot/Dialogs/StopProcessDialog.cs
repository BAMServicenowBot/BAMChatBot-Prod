using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BamChatBot.Models;
using Newtonsoft.Json;
using BamChatBot.Services;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;

namespace BamChatBot.Dialogs
{
	public class StopProcessDialog : CancelAndHelpDialog
	{
		public StopProcessDialog() : base(nameof(StopProcessDialog))
		{
			AddDialog(new TextPrompt(nameof(TextPrompt)));
			AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
			AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
			AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
			{
				IntroStepAsync,
				ShowProcessStepAsync,
				ConfirmStopProcessStepAsync,
				SelectStrategyStepAsync,
				StopProcessStepAsync

			}));

			// The initial child Dialog to run.
			InitialDialogId = nameof(WaterfallDialog);
		}

		private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			var processDetails = (ProcessDetails)stepContext.Options;
			new User().GetUserRunningProcess(processDetails);
			return await stepContext.NextAsync(processDetails, cancellationToken);
		}

		private async Task<DialogTurnResult> ShowProcessStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			var processDetails = (ProcessDetails)stepContext.Options;
			var processes = processDetails.Processes;
			var text = "Here are your processes in progress. ";
			if (processDetails.LoadMore)
			{
				text = string.Empty;
				processDetails.LoadMore = false;
			}
			if (processes.Count > 0)
			{
				var rpaService = new RPAService();
				//var _user = await _userAccessor.GetAsync(stepContext.Context, () => new User(), cancellationToken);
				//get last index
				var response = rpaService.GetUser(stepContext.Context.Activity.Conversation.Id);
				var user = new List<User>();
				if (response.IsSuccess)
					user = JsonConvert.DeserializeObject<List<User>>(response.Content);
				var result = rpaService.GetListOfProcess(processes, Convert.ToInt32(user[0].u_last_index));
				var choices = result.Choices;
				//add one choice for rpa support
				var rpaSupportChoice = rpaService.GetRPASupportOption();
				choices.Add(rpaSupportChoice);
				//save index
				user[0].u_last_index = result.LastIndex.ToString();
				rpaService.UpdateUser(user[0], stepContext.Context.Activity.Conversation.Id);
				//_user.u_last_index = result.LastIndex;
				//await this._userAccessor.SetAsync(stepContext.Context, _user, cancellationToken);

				return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
				{
					Prompt = (Activity)ChoiceFactory.HeroCard(choices, text + "Which one would you like to stop?")
					/*Prompt = MessageFactory.Text(text+ "Which one would you like to stop?"),
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

		private async Task<DialogTurnResult> ConfirmStopProcessStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			var rpaService = new RPAService();
			var processDetails = (ProcessDetails)stepContext.Options;
			var result = stepContext.Result.ToString();
			var promptOption = new PromptOption();
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
			var user = new List<User>();
			if (response.IsSuccess)
				user = JsonConvert.DeserializeObject<List<User>>(response.Content);
			//var _user = await _userAccessor.GetAsync(stepContext.Context, () => new User(), cancellationToken);
			switch (result.ToLower())
			{
				case "load_more":
					processDetails.LoadMore = true;
					return await stepContext.ReplaceDialogAsync(nameof(StopProcessDialog), processDetails, cancellationToken);

				case "rpasupport@bayview.com":
					//save index
					user[0].u_last_index = "0";
					rpaService.UpdateUser(user[0], stepContext.Context.Activity.Conversation.Id);
					//_user.u_last_index = 0;
					//await _userAccessor.SetAsync(stepContext.Context, _user, cancellationToken);
					processDetails.Action = string.Empty;
					return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);

				default:
					processDetails.ProcessSelected = rpaService.GetSelectedProcess(processDetails.Processes, result);
					if (!string.IsNullOrEmpty(processDetails.ProcessSelected.Sys_id))
					{
						//save index
						user[0].u_last_index = "0";
						rpaService.UpdateUser(user[0], stepContext.Context.Activity.Conversation.Id);
						//_user.u_last_index = 0;
						//await _userAccessor.SetAsync(stepContext.Context, _user, cancellationToken);
						var choices = rpaService.GetConfirmChoices();
						return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
						{
							Prompt = (Activity)ChoiceFactory.SuggestedAction(choices, "You have selected " + processDetails.ProcessSelected.Name + ". Stop this process?")
							/*Prompt = MessageFactory.Text("You have selected " + processDetails.ProcessSelected.Name + ". Stop this process?"),
							Choices = ChoiceFactory.ToChoices(new List<string> { "Yes", "No" })*/
						}, cancellationToken);
					}
					else
					{
						return await stepContext.ReplaceDialogAsync(nameof(MainDialog), null, cancellationToken);
					}
			}
		}

		private async Task<DialogTurnResult> SelectStrategyStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			var msg = string.Empty;
			var result = stepContext.Result.ToString();
			var processDetails = (ProcessDetails)stepContext.Options;
			processDetails.Action = string.Empty;
			var rpaService = new RPAService();
			var promptOption = new PromptOption();
			try
			{
				promptOption = JsonConvert.DeserializeObject<PromptOption>(stepContext.Result.ToString());
			}
			catch (Exception) { }

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
				//check if process is queued
				if (!string.IsNullOrEmpty(processDetails.ProcessSelected.queuedId))
				{
					var cancel = JsonConvert.SerializeObject(new PromptOption { Id = "Stop", Value = "3" });
					var choices = new List<Choice>
				{new Choice{
				Value = "3",
				Action = new CardAction(ActionTypes.PostBack, "Cancel Queued Process", null, "Cancel Queued Process", "Cancel Queued Process",value: cancel, null)
				} };
					choices.Add(rpaService.GetMainMenuOption());
					return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
					{
						Prompt = (Activity)ChoiceFactory.SuggestedAction(choices, "Process "+ processDetails.ProcessSelected.Name + " will be deleted from the queue and will not run." + Environment.NewLine+"Please click on below button to Cancel Queued Process")
					}, cancellationToken);
				}
				else
				{
					var stop = JsonConvert.SerializeObject(new PromptOption { Id = "Stop", Value = "1" });
					var terminate = JsonConvert.SerializeObject(new PromptOption { Id = "Stop", Value = "2" });
					var choices = new List<Choice>
				{new Choice{
				Value = "1",
				Action = new CardAction(ActionTypes.PostBack, "Safely Stop Run", null, "Safely Stop Run", "Safely Stop Run",value: stop, null)
				},
				new Choice{
				Value = "2",
				Action = new CardAction(ActionTypes.PostBack, "Terminate Process", null, "Terminate Process", "Terminate Process", value: terminate, null)
				}};
					return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
					{
						Prompt = (Activity)ChoiceFactory.SuggestedAction(choices, "Please select one of the buttons below to Stop the Process:")
					}, cancellationToken);
				}
			}
			else if (result.ToLower() == "no" || result.ToLower() == "n")
			{
				return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);
			}
			else
			{
				return await stepContext.ReplaceDialogAsync(nameof(MainDialog), null, cancellationToken);
			}
		}

		private async Task<DialogTurnResult> StopProcessStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			var msg = string.Empty;
			var result = stepContext.Result.ToString();
			var processDetails = (ProcessDetails)stepContext.Options;
			processDetails.Action = string.Empty;
			var rpaService = new RPAService();
			var promptOption = new PromptOption();
			try
			{
				promptOption = JsonConvert.DeserializeObject<PromptOption>(stepContext.Result.ToString());
			}
			catch (Exception) { }

			if (!string.IsNullOrEmpty(promptOption.Id))
			{
				if (promptOption.Id != "Stop" && promptOption.Id !="mainMenu")
				{
					processDetails.Action = "pastMenu";
					return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);
				}
				result = promptOption.Value;
			}

			if (result.ToLower() == "1" || result.ToLower() == "2" || result.ToLower() == "3")
			{
				var response = new APIResponse();
				if(result.ToLower() == "3")
				{
					response = rpaService.CancelQueuedProcess(processDetails.ProcessSelected);
				}
				else
				{
					response = rpaService.StopProcess(processDetails.ProcessSelected.Sys_id, Convert.ToInt32(result));
				}
				
				if (response.IsSuccess)
				{
					if (!string.IsNullOrEmpty(response.Content))
					{
						msg = response.Content;
					}
					else
					{
						if (result.ToLower() == "3")
							msg = "Process " + processDetails.ProcessSelected.Name + " has been successfully deleted from the queue.";
						else
							msg = "Request to Stop Process " + processDetails.ProcessSelected.Name + " Submitted. Please allow a few minutes for the status to refresh.";
					}
				}
				else
				{
					if (!string.IsNullOrEmpty(response.Error))
					{
						msg = response.Error;
					}
					else
					{
						msg = response.Message;
					}

				}
				await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
				{
					Prompt = MessageFactory.Text(msg)
				}, cancellationToken);
				return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);
			}
			

				return await stepContext.ReplaceDialogAsync(nameof(MainDialog), null, cancellationToken);

		}

	}
}

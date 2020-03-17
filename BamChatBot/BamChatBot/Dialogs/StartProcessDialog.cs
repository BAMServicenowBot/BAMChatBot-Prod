﻿using BamChatBot.Models;
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
		protected readonly IStatePropertyAccessor<User> _userAccessor;
		public readonly IStatePropertyAccessor<ConversationFlow> _conversationFlow;
		public StartProcessDialog(IStatePropertyAccessor<User> userAccessor, IStatePropertyAccessor<ConversationFlow> conversationFlow)
			: base(nameof(StartProcessDialog))
		{
			_userAccessor = userAccessor;
			_conversationFlow = conversationFlow;
			AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
			AddDialog(new StartProcessErrorDialog());
			AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
			{
				IntroStepAsync,
				ShowProcessStepAsync,
				ConfirmStartProcessStepAsync,
				StartProcessStepAsync,
				StartAnotherProcessStepAsync
			}));

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
				var user = JsonConvert.DeserializeObject<List<User>>(response.Content);
				//var _user = await _userAccessor.GetAsync(stepContext.Context, () => new User(), cancellationToken);
				var result = rpaService.GetListOfProcess(processes, user[0].u_last_index);
				var choices = result.Choices;
				var rpaSupportChoice = rpaService.GetRPASupportOption();
				choices.Add(rpaSupportChoice);
				//save index
				user[0].u_last_index = result.LastIndex;
				rpaService.UpdateUser(user[0]);
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
			var rpaService = new RPAService();
			var processDetails = (ProcessDetails)stepContext.Options;
			var result = stepContext.Result.ToString();
			var response = rpaService.GetUser(stepContext.Context.Activity.Conversation.Id);
			var user = JsonConvert.DeserializeObject<List<User>>(response.Content);
			//var _user = await _userAccessor.GetAsync(stepContext.Context, () => new User(), cancellationToken);
			if (result == "RPASupport@bayview.com")
			{
				//save index
				user[0].u_last_index = 0;
				rpaService.UpdateUser(user[0]);
				//_user.u_last_index = 0;
				//await _userAccessor.SetAsync(stepContext.Context, _user, cancellationToken);
				processDetails.Action = string.Empty;
				return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);
			}
			else if (result == "Load_More")
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
					user[0].u_last_index = 0;
					rpaService.UpdateUser(user[0]);
					//_user.u_last_index = 0;
					//await _userAccessor.SetAsync(stepContext.Context, _user, cancellationToken);

					processDetails.ProcessSelected.StartedBy = "chat_bot";
					return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
					{
						Prompt = (Activity)ChoiceFactory.SuggestedAction(ChoiceFactory.ToChoices(new List<string> { "Yes", "No" }), "You have selected " + processDetails.ProcessSelected.Name + ". Start this process?")
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
			if (result == "Yes")
			{
				//get conversationflow obj
				var conversationFlow = await this._conversationFlow.GetAsync(stepContext.Context, () => new ConversationFlow());
				var rpaService = new RPAService();
				//save activity id for when process finish
				var activityId = stepContext.Context.Activity.Id;
				//check if the process can start
				if (processDetails.ProcessSelected.LastRun.State == "Faulted" || processDetails.ProcessSelected.LastRun.State == "Successful" || processDetails.ProcessSelected.LastRun.State == "Stopped")
				{
					//check if process need params
					if (processDetails.ProcessSelected.Releases.Any(r => r.Parameters_Required == true))
					{
						//group parameters by release
						foreach (var r in processDetails.ProcessSelected.Releases)
						{
							if (processDetails.ProcessSelected.ProcessParameters.ContainsKey(r.Sys_id))
							{
								processDetails.ProcessSelected.ProcessParameters[r.Sys_id].AddRange(r.Parameters);
							}
							else
							{
								processDetails.ProcessSelected.ProcessParameters.Add(r.Sys_id, r.Parameters);
							}
						}

						conversationFlow.AskingForParameters = true;
						conversationFlow.ProcessParameters = processDetails.ProcessSelected.ProcessParameters;
						conversationFlow.GroupLastIndex = 0;
						conversationFlow.ParamLastIndex = 0;
						await this._conversationFlow.SetAsync(stepContext.Context, conversationFlow);
						return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
						{ Prompt = MessageFactory.Text("This process needs input parameters, please enter ") }, cancellationToken);

						//test
						//restart this Dialog
						//return await stepContext.ReplaceDialogAsync(nameof(StartProcessDialog), processDetails, cancellationToken);
					}
					else
					{
						conversationFlow.AskingForParameters = false;
						await this._conversationFlow.SetAsync(stepContext.Context, conversationFlow);
						var response = rpaService.StartProcess(processDetails.ProcessSelected);
						var error = false;
						if (string.IsNullOrEmpty(response.Content) || !response.IsSuccess)
						{
							error = true;
						}
						if (error)
						{
							return await stepContext.ReplaceDialogAsync(nameof(StartProcessErrorDialog), processDetails, cancellationToken);
						}
						else
						{
							processDetails.Jobs = JsonConvert.DeserializeObject<List<Job>>(response.Content);

							return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
							{
								Prompt = (Activity)ChoiceFactory.SuggestedAction(ChoiceFactory.ToChoices(new List<string> { "Yes", "No" }), processDetails.ProcessSelected.Name + " process  has started, you will be notified when it finishes. Do you want to run another process?")
								/*Prompt = MessageFactory.Text(processDetails.ProcessSelected.Name + " process  has started, you will be notified when it finishes. Do you want to run another process?"),
								Choices = ChoiceFactory.ToChoices(new List<string> { "Yes", "No" })*/
							}, cancellationToken);
						}
					}
				}
				else
				{
					processDetails.Action = "error";
					processDetails.Error = "Cannot start " + processDetails.ProcessSelected.Name + " because is running already.";
					return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);
				}
			}
			else if(result == "No")//when no is selected
			{
				processDetails.Action = string.Empty;
				return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);

			}
			else //when something is typed
			{
				processDetails.Action = string.Empty;
				return await stepContext.ReplaceDialogAsync(nameof(MainDialog), null, cancellationToken);
			}
		}

		private async Task<DialogTurnResult> StartAnotherProcessStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			var processDetails = (ProcessDetails)stepContext.Options;

			var action = stepContext.Result.ToString();
			if (action == "Yes")
			{
				//restart this Dialog
				return await stepContext.ReplaceDialogAsync(nameof(StartProcessDialog), processDetails, cancellationToken);
			}
			else if(action == "No")//go back to main Dialog
			{
				processDetails.Action = string.Empty;
				return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);
			}
			else//go back to main Dialog with null
			{
				processDetails.Action = string.Empty;
				return await stepContext.ReplaceDialogAsync(nameof(MainDialog), null, cancellationToken);
			}
		}

		private static async Task<DialogTurnResult> FillOutParameters(WaterfallStepContext stepContext, ProcessParameters pp)
		{
			return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
			{ Prompt = MessageFactory.Text("Enter " + pp.ParmName) });
		}

	}
}

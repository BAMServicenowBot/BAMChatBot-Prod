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
			AddDialog(new ParametersProcessDialog());
			AddDialog(new StartProcessSharedDialog());
			AddDialog(new RobotsDialog());
			AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
			{
				IntroStepAsync,
				ShowProcessStepAsync,
				ConfirmStartProcessStepAsync,
				StartProcessStepAsync
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
				var user = new List<User>();
				if(response.IsSuccess)
				 user = JsonConvert.DeserializeObject<List<User>>(response.Content);
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
			var user =  new List<User>();
			var result = stepContext.Result.ToString();
			var response = rpaService.GetUser(stepContext.Context.Activity.Conversation.Id);
			if (response.IsSuccess)
				 user = JsonConvert.DeserializeObject<List<User>>(response.Content);
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
						Prompt = (Activity)ChoiceFactory.SuggestedAction(ChoiceFactory.ToChoices(new List<string> { "Yes", "No" }), "You have selected " + processDetails.ProcessSelected.Name + ". Would you like to start this process?")
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
				var rpaService = new RPAService();
				//save activity id for when process finish
				var activityId = stepContext.Context.Activity.Id;
				//check if the process can start
				if (processDetails.ProcessSelected.LastRun.State == "Faulted" || processDetails.ProcessSelected.LastRun.State == "Successful" || processDetails.ProcessSelected.LastRun.State == "Stopped" || string.IsNullOrEmpty(processDetails.ProcessSelected.LastRun.State))
				{
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
						/*var count = 0;
						foreach (var r in processDetails.ProcessSelected.Releases)
						{
							if (r.parameters_required)
							{
								foreach (var p in r.parameters)
								{
									//save the params
									var _conversationFlow = new ConversationFlow
									{
										u_conversation_id = stepContext.Context.Activity.Conversation.Id,
										u_release_id = r.sys_id,
										u_param_name = p.parmName,
										u_last_question_index = count,
										u_type = p.parmType,
										u_active = true
									};

									rpaService.SaveConversationFlow(_conversationFlow);
									count++;
								}
							}
						}*/

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
					processDetails.Error = "Cannot start " + processDetails.ProcessSelected.Name + " because is running already.";
					return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);
				}
			}
			else if (result == "No")//when no is selected
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

	}
}

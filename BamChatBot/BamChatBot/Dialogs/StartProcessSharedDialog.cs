using BamChatBot.Models;
using BamChatBot.Services;
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
	public class StartProcessSharedDialog : CancelAndHelpDialog
	{
		public StartProcessSharedDialog() : base(nameof(StartProcessSharedDialog))
		{
			AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
		{
				IntroStepAsync,
				StartAnotherProcessStepAsync
		}));
			AddDialog(new StartProcessWithParamsErrorDialog());
			AddDialog(new StartProcessErrorDialog());

			// The initial child Dialog to run.
			InitialDialogId = nameof(WaterfallDialog);
		}

		private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			var processDetails = (ProcessDetails)stepContext.Options;
			/*conversationFlow.AskingForParameters = false;
			await this._conversationFlow.SetAsync(stepContext.Context, conversationFlow);*/
			if (processDetails.ProcessSelected.LastRun.State == "Faulted" || processDetails.ProcessSelected.LastRun.State == "Successful" || processDetails.ProcessSelected.LastRun.State == "Stopped" || string.IsNullOrEmpty(processDetails.ProcessSelected.LastRun.State))
			{
				var rpaService = new RPAService();
				var response = new APIResponse();
				var hasInputParams = false;
				if (processDetails.ProcessSelected.Releases.Any(r => r.parameters_required == true))
				{
					processDetails.ProcessSelected.ConversationId = stepContext.Context.Activity.Conversation.Id;
					response = rpaService.StartProcessWithParams(processDetails.ProcessSelected);
					hasInputParams = true;
				}
				else
				{
					response = rpaService.StartProcess(processDetails.ProcessSelected,stepContext.Context.Activity.Conversation.Id);

				}

				var error = false;
				if (string.IsNullOrEmpty(response.Content) || !response.IsSuccess)
				{
					error = true;
				}
				if (error)
				{
					if (hasInputParams)
					{
						processDetails.AttemptCount = processDetails.AttemptCount + 1;
						if (processDetails.AttemptCount == 3)
						{
							processDetails.AttemptCount = 0;
							//contact rpa support
							return await stepContext.ReplaceDialogAsync(nameof(StartProcessWithParamsErrorDialog), processDetails, cancellationToken);
						}
						else
						{
							rpaService.SaveConversationFlow(processDetails.ProcessSelected, stepContext.Context.Activity.Conversation.Id);
							return await stepContext.ReplaceDialogAsync(nameof(ParametersProcessDialog), processDetails, cancellationToken);

						}
					}
					else
					{
						return await stepContext.ReplaceDialogAsync(nameof(StartProcessErrorDialog), processDetails, cancellationToken);

					}
				}
				else
				{
					processDetails.AttemptCount = 0;
					processDetails.Jobs = JsonConvert.DeserializeObject<List<Job>>(response.Content);
					var choices = rpaService.GetConfirmChoices();
					return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
					{
						Prompt = (Activity)ChoiceFactory.SuggestedAction(choices, processDetails.ProcessSelected.Name + "  process has started and you will be notified when it finishes."+Environment.NewLine+" Do you want to run another process?")
					}, cancellationToken);
				}
			}
			else
			{
				processDetails.AttemptCount = 0;
				processDetails.Action = "error";
				processDetails.Error = "Cannot start " + processDetails.ProcessSelected.Name + " because the process is already running.";
				return await stepContext.ReplaceDialogAsync(nameof(MainDialog), processDetails, cancellationToken);
			}
		}

		private async Task<DialogTurnResult> StartAnotherProcessStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			var processDetails = (ProcessDetails)stepContext.Options;
			var action = stepContext.Result.ToString();
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
				action = promptOption.Value;
			}

			if (action.ToLower() == "yes" || action.ToLower() == "y")
			{
				//start StartProcessDialog Dialog
				return await stepContext.ReplaceDialogAsync(nameof(StartProcessDialog), processDetails, cancellationToken);
			}
			else if (action.ToLower() == "no" || action.ToLower() == "n")//go back to main Dialog
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
	}
}

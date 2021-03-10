// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio CoreBot v4.6.2

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BamChatBot.Dialogs;
using BamChatBot.Models;
using BamChatBot.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BamChatBot.Bots
{
	// This IBot implementation can run any type of Dialog. The use of type parameterization is to allows multiple different bots
	// to be run at different endpoints within the same project. This can be achieved by defining distinct Controller types
	// each with dependency on distinct IBot types, this way ASP Dependency Injection can glue everything together without ambiguity.
	// The ConversationState is used by the Dialog system. The UserState isn't, however, it might have been used in a Dialog implementation,
	// and the requirement is that all BotState objects are saved at the end of a turn.
	public class DialogBot<T> : ActivityHandler
		where T : Dialog
	{
		protected readonly Dialog Dialog;
		protected readonly BotState ConversationState;
		protected readonly BotState UserState;
		protected readonly ILogger Logger;
		// Dependency injected dictionary for storing ConversationReference objects used in NotifyController to proactively message users
		protected ConcurrentDictionary<string, ConversationReference> _conversationReferences;
		public User _user;
		public readonly IStatePropertyAccessor<User> _userAccessor;
		public readonly IStatePropertyAccessor<ConversationFlow> _conversationFlow;

		public DialogBot(ConversationState conversationState, UserState userState, T dialog, ILogger<DialogBot<T>> logger, ConcurrentDictionary<string, ConversationReference> conversationReferences, User user)
		{
			ConversationState = conversationState;
			UserState = userState;
			Dialog = dialog;
			Logger = logger;
			_conversationReferences = conversationReferences;
			_userAccessor = userState.CreateProperty<User>("User");
			_conversationFlow = conversationState.CreateProperty<ConversationFlow>("ConversationFlow");
			_user = user;
		}

		public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
		{
			//This calls the rigth handler based on the type of activity received.
			await base.OnTurnAsync(turnContext, cancellationToken);

			// Save any state changes that might have occured during the turn.
			await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
			await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
			
		}

		protected void AddConversationReference(Activity activity)
		{
			var conversationReference = activity.GetConversationReference();
			_conversationReferences.AddOrUpdate(conversationReference.User.Id, conversationReference, (key, newValue) => conversationReference);
		}

		protected override async Task OnConversationUpdateActivityAsync(ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
		{
			//test
			/*var rpaService = new RPAService();
			rpaService.SaveUser(new User { u_user = "f8e33eb11b94b384dbc4c91e1e4bcb9b", u_conversation_id = turnContext.Activity.Conversation.Id });*/
			//test

			await base.OnConversationUpdateActivityAsync(turnContext, cancellationToken);
		}


		protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
		{
			var commands = new Commands();
			var startCommands = commands.GetStartCommands();
			var promptOption = new PromptOption();
			try
			{
				promptOption = JsonConvert.DeserializeObject<PromptOption>(turnContext.Activity.Text);
			}
			catch (Exception){}

			if (!string.IsNullOrEmpty(promptOption.Value))
			{
				if (startCommands.Contains(promptOption.Value.ToLower()))
				{
					AddConversationReference(turnContext.Activity as Activity);
				}
			}
			else
			{
				if (startCommands.Contains(turnContext.Activity.Text.ToLower()))
				{
					AddConversationReference(turnContext.Activity as Activity);
				}
			}
			
			
			Logger.LogInformation("Running dialog with Message Activity.");

			// Run the Dialog with the new message Activity.
			await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>("DialogState"), cancellationToken);

		}

		protected override async Task OnEventActivityAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
		{
			if (turnContext.Activity.Type == ActivityTypes.Event)
			{
				var user = new User();
				if (turnContext.Activity.Name == "urlClickedEvent")
				{

				}
				else
				{
					//get params sent from SN
					var userParam = turnContext.Activity.From.Properties["userparam"].ToString();
					user = JsonConvert.DeserializeObject<User>(userParam);
					//save user 
					var rpaService = new RPAService();
					rpaService.SaveUser(new User { u_user = user.UserId, u_conversation_id = turnContext.Activity.Conversation.Id });

				}

			}
		}

	}
}

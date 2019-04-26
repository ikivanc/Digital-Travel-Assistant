using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using System;
using AdaptiveCards;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TravelBot.Dialogs.Translation;
using TravelBot.Models;
using TravelBot.Dialogs.LUISDialog;

namespace TravelBot.Dialogs.WelcomeDialog
{
    public class WelcomeUserBot : IBot
    {
        // Welcome Message
        private readonly string welcomeMessage = @"I'm your digital Travel Assistant. Please ask your any question to me";

        // The bot state accessor object. Use this to access specific state properties.
        private readonly WelcomeUserStateAccessors _welcomeUserStateAccessors;

        private readonly MicrosoftTranslator _translator;

        private readonly DialogSet _dialogs;

        private readonly LuisServices _luisServices;

        private static readonly string LuisKey = "LuisBot";

        private HotelReservation hotelsearchstate;

        public WelcomeUserBot(WelcomeUserStateAccessors statePropertyAccessor, MicrosoftTranslator translator, LuisServices luisservices)
        {
            this._welcomeUserStateAccessors = statePropertyAccessor ?? throw new System.ArgumentNullException("state accessor can't be null");

            // Translator
            this._translator = translator;

            // LUIS definition
            this._luisServices = luisservices;

            // Dialog Waterfalls
            this._dialogs = new DialogSet(statePropertyAccessor.ConversationDialogState);
            this._dialogs.Add(new WaterfallDialog("introDialog", new WaterfallStep[] { ChoiceCardStepAsync, ShowCardStepAsync }));
            this._dialogs.Add(new WaterfallDialog("hotelDialog", new WaterfallStep[] { LocationStepAsync, HotelStepAsync, GuestsStepAsync, RoomSelectionStepAsync, CheckInStepAsync, CheckOutStepAsync, LastStepAsync}));

            // Prompts
            this._dialogs.Add(new ChoicePrompt("regionPrompt"));
            this._dialogs.Add(new TextPrompt("hotelPrompt"));
            this._dialogs.Add(new ChoicePrompt("roomPrompt"));
            this._dialogs.Add(new ChoicePrompt("cardPrompt"));
            this._dialogs.Add(new NumberPrompt<int>("guestPrompt"));
            this._dialogs.Add(new DateTimePrompt("checkinPrompt"));
            this._dialogs.Add(new DateTimePrompt("checkoutPrompt"));
        }

        public async Task OnTurnAsync(ITurnContext turnContext,  CancellationToken cancellationToken = new CancellationToken())
        {
            // use state accessor to extract the didBotWelcomeUser flag
            var didBotWelcomeUser = await this._welcomeUserStateAccessors.DidBotWelcomeUser.GetAsync(turnContext, () => false);

            // Handle Message activity type, which is the main activity type for shown within a conversational interface
            // Message activities may contain text, speech, interactive cards, and binary or unknown attachments.
            // see https://aka.ms/about-bot-activity-message to learn more about the message and other activity types
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                // Your bot should proactively send a welcome message to a personal chat the first time
                // (and only the first time) a user initiates a personal chat with your bot.
                if (didBotWelcomeUser == false)
                {
                    // Update user state flag to reflect bot handled first user interaction.
                    await this._welcomeUserStateAccessors.DidBotWelcomeUser.SetAsync(turnContext, true);
                    await this._welcomeUserStateAccessors.UserState.SaveChangesAsync(turnContext);

                    this.hotelsearchstate = await this._welcomeUserStateAccessors.HotelSearchState.GetAsync(turnContext, () => new HotelReservation(), cancellationToken);
                    var dialogContext = await this._dialogs.CreateContextAsync(turnContext, cancellationToken);
                    var results = await dialogContext.ContinueDialogAsync(cancellationToken);

                    if (results.Status == DialogTurnStatus.Empty)
                    {
                        // await dialogContext.BeginDialogAsync("introDialog", cancellationToken: cancellationToken);
                    }
                }
                else
                {
                    var dialogContext = await this._dialogs.CreateContextAsync(turnContext, cancellationToken);
                    var results = await dialogContext.ContinueDialogAsync(cancellationToken);

                    // Check if message is a command
                    if (turnContext.Activity.Text == "cancel")
                    {
                        results = await dialogContext.CancelAllDialogsAsync(cancellationToken);
                    }
                    else if (turnContext.Activity.Text == "Hey" || turnContext.Activity.Text == "New" || turnContext.Activity.Text == "New Search")
                    {
                        await dialogContext.BeginDialogAsync("introDialog", cancellationToken: cancellationToken);
                    }
                    else if (results.Status == DialogTurnStatus.Empty)
                    {
                        // Translate user input to English if input is in a different language
                        string textTranslated = this._translator.TranslateAsync(turnContext.Activity.Text, "en", cancellationToken).Result;
                        turnContext.Activity.Text = textTranslated;

                        var luisResults = await this._luisServices.LuisServicesDic[LuisKey].RecognizeAsync<LuisResultModel>(turnContext, cancellationToken);

                        if (luisResults != null && luisResults.TopIntent().intent.ToString() != null & luisResults.TopIntent().score > 0.5)
                        {
                            // Parse LUIS Entities and assign into Search
                            var luisentities = luisResults;
                            this.hotelsearchstate = await this._welcomeUserStateAccessors.HotelSearchState.GetAsync(turnContext, () => new HotelReservation(), cancellationToken);

                            if (luisentities.Entities != null && luisentities.Entities._instance.Hotel_Region != null)
                            {
                                this.hotelsearchstate.Region = luisentities.Entities._instance.Hotel_Region[0].Text;
                            }

                            if (luisentities.Entities != null && luisentities.Entities._instance.GuestCount != null)
                            {
                                this.hotelsearchstate.GuestCount = (int)luisentities.Entities.GuestCount[0].number[0];
                            }

                            if (luisentities.Entities != null && luisentities.Entities._instance.HotelName != null)
                            {
                                this.hotelsearchstate.HotelName = luisentities.Entities._instance.HotelName[0].Text;
                            }

                            if (luisentities.Entities != null && luisentities.Entities._instance.RoomType != null)
                            {
                                this.hotelsearchstate.Room = luisentities.Entities._instance.RoomType[0].Text;
                            }

                            if (luisentities.Entities != null && luisentities.Entities._instance.datetime != null)
                            {
                                if (luisentities.Entities.datetime[0] != null && luisentities.Entities.datetime[1] != null)
                                {
                                    // If you want to handle LUIS DateTimeSpec you need to parse here.
                                    this.hotelsearchstate.CheckInDate = Convert.ToDateTime(luisentities.Entities._instance.datetime[0].Text);
                                    this.hotelsearchstate.CheckOutDate = Convert.ToDateTime(luisentities.Entities._instance.datetime[1].Text);
                                }
                                else if (luisentities.Entities._instance.RoomType[0] != null)
                                {
                                    this.hotelsearchstate.CheckInDate = Convert.ToDateTime(luisentities.Entities._instance.datetime[0].Text);
                                }
                            }

                            await dialogContext.BeginDialogAsync("hotelDialog", null, cancellationToken);
                        }
                    }
                    else if (results.Status == DialogTurnStatus.Cancelled)
                    {
                        await turnContext.SendActivityAsync($"Dialog flow cancelled", cancellationToken: cancellationToken);
                    }
                }
            }
            else if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
            {
                if (turnContext.Activity.MembersAdded.Any())
                {
                    // Iterate over all new members added to the conversation
                    foreach (var member in turnContext.Activity.MembersAdded)
                    {
                        if (member.Id != turnContext.Activity.Recipient.Id)
                        {
                            await turnContext.SendActivityAsync($"Hi {member.Name}! {this.welcomeMessage}", cancellationToken: cancellationToken);
                            var dialogContext = await this._dialogs.CreateContextAsync(turnContext, cancellationToken);
                        }
                    }
                }
            }
            else
            {
                // Default behavior for all other type of activities.
                await turnContext.SendActivityAsync($"{turnContext.Activity.Type} activity detected");
            }

            // save any state changes made to your state objects.
            await this._welcomeUserStateAccessors.UserState.SaveChangesAsync(turnContext);
        }

        private static PromptOptions GenerateOptions(Activity activity)
        {
            // Create options for the prompt
            var options = new PromptOptions()
            {
                Prompt = activity.CreateReply("How can I help you?"),
                Choices = new List<Choice>(),
            };

            // Add the choices for the prompt.
            options.Choices.Add(new Choice() { Value = "Hotel Search" });
            options.Choices.Add(new Choice() { Value = "Flight Search" });
            options.Choices.Add(new Choice() { Value = "Tour Search" });

            return options;
        }

        private static PromptOptions GenerateRoomOptions(Activity activity)
        {
            // Create options for the prompt
            var options = new PromptOptions()
            {
                Prompt = activity.CreateReply("Please select your room size!"),
                Choices = new List<Choice>(),
            };

            // Add the choices for the prompt.
            options.Choices.Add(new Choice() { Value = "Single Room" });
            options.Choices.Add(new Choice() { Value = "King Size" });
            options.Choices.Add(new Choice() { Value = "Suite" });

            return options;
        }

        private static async Task<DialogTurnResult> ChoiceCardStepAsync(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            return await step.PromptAsync("cardPrompt", GenerateOptions(step.Context.Activity), cancellationToken);
        }

        private async Task<DialogTurnResult> ShowCardStepAsync(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            // Get the text from the activity to use to show the correct card
            var text = step.Context.Activity.Text;

            // Reply to the activity we received with an activity.
            var reply = step.Context.Activity.CreateReply();

            switch (text)
            {
               case "Hotel Search":
                    reply.Text = $"You clicked: {text}";
                    await step.Context.SendActivityAsync(reply, cancellationToken);
                    return await step.BeginDialogAsync("hotelDialog", null, cancellationToken);
               case "Flight Search":
                    reply.Text = $"You clicked: {text}";
                    await step.Context.SendActivityAsync(reply, cancellationToken);
                    return await step.BeginDialogAsync("FlightDialog", null, cancellationToken);
               case "Tur ARama":
                    reply.Text = $"You clicked {text}";
                    await step.Context.SendActivityAsync(reply, cancellationToken);
                    return await step.BeginDialogAsync("TourDialog", null, cancellationToken);
               default:
                    reply.Text = "Please select an option below";
                    await step.PromptAsync("cardPrompt", GenerateOptions(step.Context.Activity), cancellationToken);
                    return await step.ContinueDialogAsync(cancellationToken: cancellationToken);
            }
        }

        private static PromptOptions GenerateLocationOptions(Activity activity)
        {
            // Create options for the prompt
            var options = new PromptOptions()
            {
                Prompt = activity.CreateReply("Please select a region for your travel"),
                Choices = new List<Choice>(),
            };

            // Add the choices for the prompt.
            options.Choices.Add(new Choice() { Value = "Mediterranean" });
            options.Choices.Add(new Choice() { Value = "Aegean" });
            options.Choices.Add(new Choice() { Value = "Center Anatolia" });
            options.Choices.Add(new Choice() { Value = "Eastern Anatolia" });
            options.Choices.Add(new Choice() { Value = "South E. Anatolia" });
            options.Choices.Add(new Choice() { Value = "Marmara" });
            options.Choices.Add(new Choice() { Value = "BlackSea" });

            return options;
        }

        private async Task<DialogTurnResult> LocationStepAsync(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            this.hotelsearchstate = await this._welcomeUserStateAccessors.HotelSearchState.GetAsync(step.Context, () => new HotelReservation(), cancellationToken);
            if (this.hotelsearchstate != null && !string.IsNullOrEmpty(this.hotelsearchstate.Region))
            {
                return await step.NextAsync(this.hotelsearchstate.Region, cancellationToken);
            }
            else
            {
                return await step.PromptAsync("regionPrompt", GenerateLocationOptions(step.Context.Activity), cancellationToken);
            }
        }

        private async Task<DialogTurnResult> HotelStepAsync(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            this.hotelsearchstate = await this._welcomeUserStateAccessors.HotelSearchState.GetAsync(step.Context, () => new HotelReservation(), cancellationToken);
            if (this.hotelsearchstate != null && string.IsNullOrEmpty(this.hotelsearchstate.Region))
            {
                this.hotelsearchstate.Region = step.Context.Activity.Text;
            }

            if (this.hotelsearchstate != null && !string.IsNullOrEmpty(this.hotelsearchstate.HotelName))
            {
                return await step.NextAsync(this.hotelsearchstate.HotelName, cancellationToken);
            }
            else
            {
                return await step.PromptAsync("hotelPrompt", new PromptOptions { Prompt = MessageFactory.Text("Please enter your hotel name") }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> GuestsStepAsync(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            this.hotelsearchstate = await this._welcomeUserStateAccessors.HotelSearchState.GetAsync(step.Context, () => new HotelReservation(), cancellationToken);
            if (this.hotelsearchstate != null && string.IsNullOrEmpty(this.hotelsearchstate.HotelName))
            {
                this.hotelsearchstate.HotelName = step.Context.Activity.Text;
            }

            if (this.hotelsearchstate != null && this.hotelsearchstate.GuestCount != null)
            {
                return await step.NextAsync(this.hotelsearchstate.GuestCount, cancellationToken);
            }
            else
            {
                return await step.PromptAsync("guestPrompt", new PromptOptions { Prompt = MessageFactory.Text("How many guests will be there?") }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> RoomSelectionStepAsync(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            this.hotelsearchstate = await this._welcomeUserStateAccessors.HotelSearchState.GetAsync(step.Context, () => new HotelReservation(), cancellationToken);
            if (this.hotelsearchstate != null && this.hotelsearchstate.GuestCount == null)
            {
                this.hotelsearchstate.GuestCount = Convert.ToInt32(step.Context.Activity.Text);
            }

            if (this.hotelsearchstate != null && !string.IsNullOrEmpty(this.hotelsearchstate.Room))
            {
                return await step.NextAsync(this.hotelsearchstate.Room, cancellationToken);
            }
            else
            {
                return await step.PromptAsync("roomPrompt", GenerateRoomOptions(step.Context.Activity), cancellationToken);
            }
        }

        private async Task<DialogTurnResult> CheckInStepAsync(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            this.hotelsearchstate = await this._welcomeUserStateAccessors.HotelSearchState.GetAsync(step.Context, () => new HotelReservation(), cancellationToken);
            if (this.hotelsearchstate != null && string.IsNullOrEmpty(this.hotelsearchstate.Room))
            {
                this.hotelsearchstate.Room = step.Context.Activity.Text;
            }

            if (this.hotelsearchstate != null && this.hotelsearchstate.CheckInDate != null)
            {
                 return await step.NextAsync(this.hotelsearchstate.CheckInDate, cancellationToken);
            }
            else
            {
                return await step.PromptAsync("checkinPrompt", new PromptOptions { Prompt = MessageFactory.Text("Check-in Date?") }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> CheckOutStepAsync(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            this.hotelsearchstate = await this._welcomeUserStateAccessors.HotelSearchState.GetAsync(step.Context, () => new HotelReservation(), cancellationToken);
            if (this.hotelsearchstate != null && this.hotelsearchstate.CheckInDate == null)
            {
                this.hotelsearchstate.CheckInDate = Convert.ToDateTime(step.Context.Activity.Text);
            }

            if (this.hotelsearchstate != null && this.hotelsearchstate.CheckOutDate != null)
            {
                return await step.NextAsync(this.hotelsearchstate.CheckOutDate, cancellationToken);
            }
            else
            {
                 return await step.PromptAsync("checkoutPrompt", new PromptOptions { Prompt = MessageFactory.Text("Check-Out Date?") }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> LastStepAsync(WaterfallStepContext step, CancellationToken cancellationToken)
        {
            this.hotelsearchstate = await this._welcomeUserStateAccessors.HotelSearchState.GetAsync(step.Context, () => new HotelReservation(), cancellationToken);
            if (this.hotelsearchstate != null && this.hotelsearchstate.CheckOutDate == null)
            {
                this.hotelsearchstate.CheckOutDate = Convert.ToDateTime(step.Context.Activity.Text);
            }

            // Save the state of the user
            await this._welcomeUserStateAccessors.HotelSearchState.SetAsync(step.Context, this.hotelsearchstate);
            await this._welcomeUserStateAccessors.UserState.SaveChangesAsync(step.Context);

            // Create Adaptive Card for confirmation view
            var cardAttachment = await this.CreateAdaptiveCardAttachmentAsync(@".\Resources\ResultCard.json", step.Context);
            var reply = step.Context.Activity.CreateReply();
            reply.Attachments = new List<Attachment>() { cardAttachment };
            await step.Context.SendActivityAsync(reply, cancellationToken);

            // Delete after click confirm
            await this._welcomeUserStateAccessors.HotelSearchState.SetAsync(step.Context, new HotelReservation());
            await this._welcomeUserStateAccessors.UserState.SaveChangesAsync(step.Context);

            return await step.EndDialogAsync(cancellationToken);
        }

        private async Task<Attachment> CreateAdaptiveCardAttachmentAsync(string filePath, ITurnContext turnContext)
        {
            var adaptiveCardJson = File.ReadAllText(filePath);

            this.hotelsearchstate = await this._welcomeUserStateAccessors.HotelSearchState.GetAsync(turnContext, () => new HotelReservation());

            // Assign values retrieved from users into json adaptive card
            adaptiveCardJson = adaptiveCardJson.ToString().Replace("#Region", this.hotelsearchstate.Region);
            adaptiveCardJson = adaptiveCardJson.ToString().Replace("#HotelName", this.hotelsearchstate.HotelName);
            adaptiveCardJson = adaptiveCardJson.ToString().Replace("#Room", this.hotelsearchstate.Room + " for " + this.hotelsearchstate.GuestCount);
            adaptiveCardJson = adaptiveCardJson.ToString().Replace("#CheckIn", this.hotelsearchstate.CheckInDate.Value.ToShortDateString());
            adaptiveCardJson = adaptiveCardJson.ToString().Replace("#CheckOut", this.hotelsearchstate.CheckOutDate.Value.ToShortDateString());

            var adaptiveCardAttachment = new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(adaptiveCardJson),
            };
            return adaptiveCardAttachment;
        }
    }
}
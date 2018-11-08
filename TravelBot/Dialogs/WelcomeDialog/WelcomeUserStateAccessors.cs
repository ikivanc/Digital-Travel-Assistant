using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TravelBot.Models;

namespace TravelBot.Dialogs.WelcomeDialog
{
    public class WelcomeUserStateAccessors
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WelcomeUserStateAccessors"/> class.
        /// Contains the <see cref="UserState"/> and associated <see cref="IStatePropertyAccessor{T}"/>.
        /// </summary>
        /// <param name="userState">The state object that stores the counter.</param>
        public WelcomeUserStateAccessors(UserState userState)
        {
            this.UserState = userState ?? throw new ArgumentNullException(nameof(userState));
        }

        /// <summary>
        /// Gets or sets the <see cref="IStatePropertyAccessor{T}"/> for DidBotWelcome.
        /// </summary>
        /// <value>
        /// The accessor stores if the bot has welcomed the user or not.
        /// </value>
        public IStatePropertyAccessor<bool> DidBotWelcomeUser { get; set; }

        public IStatePropertyAccessor<bool> IsLUISDialogFlow { get; set; }

        public IStatePropertyAccessor<DialogState> ConversationDialogState { get; set; }

        public IStatePropertyAccessor<HotelReservation> HotelSearchState { get; set; }

        /// <summary>
        /// Gets the <see cref="UserState"/> object for the conversation.
        /// </summary>
        /// <value>The <see cref="UserState"/> object.</value>
        public UserState UserState { get; }
    }
}
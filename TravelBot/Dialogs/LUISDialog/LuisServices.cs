using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TravelBot.Dialogs.LUISDialog
{
    public class LuisServices
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LuisServices"/> class.
        /// </summary>
        /// <param name="botConfiguration">A dictionary of named <see cref="BotConfiguration"/> instances for usage within the bot.</param>
        public LuisServices(BotConfiguration botConfiguration)
        {
            foreach (var service in botConfiguration.Services)
            {
                switch (service.Type)
                {
                    case ServiceTypes.Luis:
                        {
                            var luis = (LuisService)service;
                            if (luis == null)
                            {
                                throw new InvalidOperationException("The LUIS service is not configured correctly in your '.bot' file.");
                            }

                            var app = new LuisApplication(luis.AppId, luis.AuthoringKey, luis.GetEndpoint());
                            var recognizer = new LuisRecognizer(app);
                            this.LuisServicesDic.Add(luis.Name, recognizer);
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// Gets the set of LUIS Services used.
        /// Given there can be multiple <see cref="LuisRecognizer"/> services used in a single bot,
        /// LuisServices is represented as a dictionary.  This is also modeled in the
        /// ".bot" file since the elements are named.
        /// </summary>
        /// <remarks>The LUIS services collection should not be modified while the bot is running.</remarks>
        /// <value>
        /// A <see cref="LuisRecognizer"/> client instance created based on configuration in the .bot file.
        /// </value>
        public Dictionary<string, LuisRecognizer> LuisServicesDic { get; } = new Dictionary<string, LuisRecognizer>();

    }
}
﻿using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Twitch.Base.Models.NewAPI.Clips;

namespace MixItUp.Base.Actions
{
    [Obsolete]
    [DataContract]
    public class ClipsAction : ActionBase
    {
        public const string ClipURLSpecialIdentifier = "clipurl";

        private static SemaphoreSlim asyncSemaphore = new SemaphoreSlim(1);

        protected override SemaphoreSlim AsyncSemaphore { get { return ClipsAction.asyncSemaphore; } }

        [DataMember]
        public bool IncludeDelay { get; set; }

        [DataMember]
        public bool ShowClipInfoInChat { get; set; }

        public ClipsAction() : base(ActionTypeEnum.Clips) { }

        public ClipsAction(bool includeDelay, bool showClipInfoInChat)
            : this()
        {
            this.IncludeDelay = includeDelay;
            this.ShowClipInfoInChat = showClipInfoInChat;
        }

        protected override async Task PerformInternal(UserViewModel user, IEnumerable<string> arguments)
        {
            ClipCreationModel clipCreation = await ServiceManager.Get<TwitchSessionService>().UserConnection.CreateClip(ServiceManager.Get<TwitchSessionService>().UserNewAPI, this.IncludeDelay);
            if (clipCreation != null)
            {
                for (int i = 0; i < 12; i++)
                {
                    await Task.Delay(5000);

                    ClipModel clip = await ServiceManager.Get<TwitchSessionService>().UserConnection.GetClip(clipCreation);
                    if (clip != null && !string.IsNullOrEmpty(clip.url))
                    {
                        await this.ProcessClip(clip);
                        return;
                    }
                }
            }
            await ChannelSession.Services.Chat.SendMessage(MixItUp.Base.Resources.ClipCreationFailed);
        }

        private async Task ProcessClip(ClipModel clip)
        {
            if (this.ShowClipInfoInChat)
            {
                await ChannelSession.Services.Chat.SendMessage("Clip Created: " + clip.url);
            }
            this.extraSpecialIdentifiers[ClipURLSpecialIdentifier] = clip.url;

            GlobalEvents.TwitchClipCreated(clip);
        }
    }
}

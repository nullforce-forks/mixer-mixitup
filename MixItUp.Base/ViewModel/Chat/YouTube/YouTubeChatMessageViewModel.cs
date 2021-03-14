﻿using Google.Apis.YouTube.v3.Data;
using MixItUp.Base.Model;
using MixItUp.Base.ViewModel.User;
using System;

namespace MixItUp.Base.ViewModel.Chat.YouTube
{
    public class YouTubeChatMessageViewModel : UserChatMessageViewModel
    {
        // YouTube Emojis:
        // https://emojipedia.org/youtube/
        // https://emojis.wiki/youtube/
        // https://stackoverflow.com/questions/64726611/how-to-get-a-list-of-youtube-channel-emojis

        public YouTubeChatMessageViewModel(LiveChatMessage message, UserViewModel user = null)
            : base(message.Id, StreamingPlatformTypeEnum.YouTube, (user != null) ? user : new UserViewModel(message))
        {
            string[] parts = message.Snippet.DisplayMessage.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                this.AddStringMessagePart(part);
            }
        }
    }
}
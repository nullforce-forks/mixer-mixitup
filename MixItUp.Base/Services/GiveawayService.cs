﻿using Mixer.Base.Util;
using MixItUp.Base.Commands;
using MixItUp.Base.Model.User;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Requirement;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services
{
    public enum GiveawayEntryTypeEnum
    {
        Command,
        [Name("Donation/Tips")]
        DonationTips,
    }

    public enum GiveawayDonationEntryQualificationTypeEnum
    {
        [Name("One Entry Per User")]
        OneEntryPerUser,
        [Name("One Entry Per Amount")]
        OneEntryPerAmount,
        [Name("Minimum Amount Required")]
        MinimumAmountRequired,
    }

    public class GiveawayUser
    {
        public UserViewModel User { get; set; }
        public int Entries { get; set; }
        public double DonationAmount { get; set; }
    }

    public interface IGiveawayService
    {
        bool IsRunning { get; }

        string Item { get; }
        GiveawayEntryTypeEnum EntryType { get; }
        GiveawayDonationEntryQualificationTypeEnum EntryQualificationType { get; }
        int TimeLeft { get; }
        IEnumerable<GiveawayUser> Users { get; }
        UserViewModel Winner { get; }

        Task<string> Start(string item, GiveawayEntryTypeEnum entryType, GiveawayDonationEntryQualificationTypeEnum entryQualificationType);

        Task End();
    }

    public class GiveawayService : IGiveawayService
    {
        public bool IsRunning { get; private set; }

        public string Item { get; private set; }
        public GiveawayEntryTypeEnum EntryType { get; private set; }
        public GiveawayDonationEntryQualificationTypeEnum EntryQualificationType { get; private set; }
        public int TimeLeft { get; private set; }
        public IEnumerable<GiveawayUser> Users { get { return this.enteredUsers.Values.ToList(); } }
        public UserViewModel Winner { get; private set; }

        private ChatCommand giveawayCommand = null;

        private Dictionary<uint, GiveawayUser> enteredUsers = new Dictionary<uint, GiveawayUser>();

        private List<uint> pastWinners = new List<uint>();

        private CancellationTokenSource backgroundThreadCancellationTokenSource = new CancellationTokenSource();

        public async Task<string> Start(string item, GiveawayEntryTypeEnum entryType, GiveawayDonationEntryQualificationTypeEnum entryQualificationType)
        {
            if (this.IsRunning)
            {
                return "A giveaway is already underway";
            }

            if (string.IsNullOrEmpty(item))
            {
                return "The name of the giveaway item must be specified";
            }
            this.Item = item;

            if (ChannelSession.Settings.GiveawayTimer <= 0)
            {
                return "The giveaway length must be greater than 0";
            }

            if (ChannelSession.Settings.GiveawayReminderInterval < 0)
            {
                return "The giveaway reminder must be 0 or greater";
            }

            if (ChannelSession.Settings.GiveawayMaximumEntries <= 0)
            {
                return "The maximum entries must be greater than 0";
            }

            if (entryType == GiveawayEntryTypeEnum.Command)
            {
                if (string.IsNullOrEmpty(ChannelSession.Settings.GiveawayCommand))
                {
                    return "Giveaway command must be specified";
                }

                if (ChannelSession.Settings.GiveawayCommand.Any(c => !Char.IsLetterOrDigit(c)))
                {
                    return "Giveaway Command can only contain letters and numbers";
                }

                ChannelSession.Settings.GiveawayDonationRequiredAmount = false;
                ChannelSession.Settings.GiveawayDonationAmount = 0.0;
            }
            else
            {
                if (entryQualificationType == GiveawayDonationEntryQualificationTypeEnum.MinimumAmountRequired || entryQualificationType == GiveawayDonationEntryQualificationTypeEnum.OneEntryPerAmount)
                {
                    if (ChannelSession.Settings.GiveawayDonationAmount <= 0.0)
                    {
                        return "The giveaway donation/tip amount must greater than 0.0";
                    }
                }

                ChannelSession.Settings.GiveawayCommand = null;
                ChannelSession.Settings.GiveawayDonationRequiredAmount = (entryQualificationType == GiveawayDonationEntryQualificationTypeEnum.MinimumAmountRequired);
            }

            await ChannelSession.SaveSettings();

            this.IsRunning = true;

            this.giveawayCommand = new ChatCommand("Giveaway Command", ChannelSession.Settings.GiveawayCommand, new RequirementViewModel());
            if (ChannelSession.Settings.GiveawayAllowPastWinners)
            {
                this.pastWinners.Clear();
            }

            this.TimeLeft = ChannelSession.Settings.GiveawayTimer * 60;
            this.enteredUsers.Clear();

            GlobalEvents.GiveawaysChangedOccurred();

            this.backgroundThreadCancellationTokenSource = new CancellationTokenSource();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () => { await this.GiveawayTimerBackground(); }, this.backgroundThreadCancellationTokenSource.Token);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            await ChannelSession.Settings.GiveawayStartedReminderCommand.Perform(extraSpecialIdentifiers: this.GetSpecialIdentifiers());

            return null;
        }

        public Task End()
        {
            this.backgroundThreadCancellationTokenSource.Cancel();

            if (this.Winner != null)
            {
                pastWinners.Add(this.Winner.ID);
            }

            this.TimeLeft = 0;
            this.Winner = null;

            this.giveawayCommand = null;
            this.enteredUsers.Clear();

            this.IsRunning = false;

            GlobalEvents.GiveawaysChangedOccurred();

            GlobalEvents.OnChatCommandMessageReceived -= GlobalEvents_OnChatCommandMessageReceived;
            GlobalEvents.OnDonationOccurred -= GlobalEvents_OnDonationOccurred;

            return Task.FromResult(0);
        }

        private async Task GiveawayTimerBackground()
        {
            GlobalEvents.OnChatCommandMessageReceived += GlobalEvents_OnChatCommandMessageReceived;
            GlobalEvents.OnDonationOccurred += GlobalEvents_OnDonationOccurred;

            try
            {
                while (this.TimeLeft > 0)
                {
                    await Task.Delay(1000);
                    this.TimeLeft--;
                    if ((ChannelSession.Settings.GiveawayTimer - this.TimeLeft) % ChannelSession.Settings.GiveawayReminderInterval == 0)
                    {
                        await ChannelSession.Settings.GiveawayStartedReminderCommand.Perform(extraSpecialIdentifiers: this.GetSpecialIdentifiers());
                    }

                    if (this.backgroundThreadCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await this.End();
                        return;
                    }

                    GlobalEvents.GiveawaysChangedOccurred();
                }

                while (true)
                {
                    this.Winner = null;
                    if (this.enteredUsers.Count > 0)
                    {
                        int totalEntries = this.enteredUsers.Values.Sum(u => u.Entries);
                        int entryNumber = RandomHelper.GenerateRandomNumber(totalEntries);

                        int currentEntry = 0;
                        foreach (var kvp in this.enteredUsers.Values)
                        {
                            currentEntry += kvp.Entries;
                            if (entryNumber < currentEntry)
                            {
                                this.enteredUsers.Remove(kvp.User.ID);
                                this.Winner = kvp.User;
                                break;
                            }
                        }
                    }

                    if (this.Winner != null)
                    {
                        if (!ChannelSession.Settings.GiveawayRequireClaim)
                        {
                            await ChannelSession.Settings.GiveawayWinnerSelectedCommand.Perform(this.Winner, extraSpecialIdentifiers: this.GetSpecialIdentifiers());
                            await this.End();
                            return;
                        }
                        else
                        {
                            await ChannelSession.Chat.SendMessage(string.Format("@{0} you've won the giveaway; type \"!claim\" in chat!.", this.Winner.UserName));

                            this.TimeLeft = 60;
                            while (this.TimeLeft > 0)
                            {
                                await Task.Delay(1000);
                                this.TimeLeft--;

                                if (this.backgroundThreadCancellationTokenSource.Token.IsCancellationRequested)
                                {
                                    await this.End();
                                    return;
                                }

                                GlobalEvents.GiveawaysChangedOccurred();
                            }
                        }
                    }
                    else
                    {
                        await ChannelSession.Chat.SendMessage("There are no users that entered/left in the giveaway");
                        await this.End();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MixItUp.Base.Util.Logger.Log(ex);
            }
        }

        private async void GlobalEvents_OnChatCommandMessageReceived(object sender, ChatMessageViewModel message)
        {
            try
            {
                if (this.TimeLeft > 0 && this.Winner == null && this.giveawayCommand.MatchesOrContainsCommand(message.Message))
                {
                    int entries = 1;

                    if (pastWinners.Contains(message.User.ID))
                    {
                        await ChannelSession.Chat.Whisper(message.User.UserName, "You have already won a giveaway and can not enter this one");
                        return;
                    }

                    IEnumerable<string> arguments = this.giveawayCommand.GetArgumentsFromText(message.Message);
                    if (arguments.Count() > 0)
                    {
                        int.TryParse(arguments.ElementAt(0), out entries);
                    }

                    int currentEntries = 0;
                    if (this.enteredUsers.ContainsKey(message.User.ID))
                    {
                        currentEntries = this.enteredUsers[message.User.ID].Entries;
                    }

                    if ((entries + currentEntries) > ChannelSession.Settings.GiveawayMaximumEntries)
                    {
                        await ChannelSession.Chat.Whisper(message.User.UserName, string.Format("You may only enter {0} time(s), you currently have entered {1} time(s)", ChannelSession.Settings.GiveawayMaximumEntries, currentEntries));
                        return;
                    }

                    if (await ChannelSession.Settings.GiveawayRequirements.DoesMeetUserRoleRequirement(message.User))
                    {
                        if (ChannelSession.Settings.GiveawayRequirements.Rank != null && ChannelSession.Settings.GiveawayRequirements.Rank.GetCurrency() != null)
                        {
                            if (!ChannelSession.Settings.GiveawayRequirements.DoesMeetRankRequirement(message.User))
                            {
                                await ChannelSession.Settings.GiveawayRequirements.Rank.SendRankNotMetWhisper(message.User);
                                return;
                            }
                        }

                        if (ChannelSession.Settings.GiveawayRequirements.Currency != null && ChannelSession.Settings.GiveawayRequirements.Currency.GetCurrency() != null)
                        {
                            int totalAmount = ChannelSession.Settings.GiveawayRequirements.Currency.RequiredAmount * entries;
                            if (!ChannelSession.Settings.GiveawayRequirements.TrySubtractCurrencyAmount(message.User, totalAmount))
                            {
                                await ChannelSession.Chat.Whisper(message.User.UserName, string.Format("You do not have the required {0} {1} to do this", totalAmount, ChannelSession.Settings.GiveawayRequirements.Currency.GetCurrency().Name));
                                return;
                            }
                        }

                        if (ChannelSession.Settings.GiveawayRequirements.Inventory != null)
                        {
                            if (!ChannelSession.Settings.GiveawayRequirements.TrySubtractInventoryAmount(message.User))
                            {
                                await ChannelSession.Settings.GiveawayRequirements.Inventory.SendNotMetWhisper(message.User);
                                return;
                            }
                        }

                        if (!this.enteredUsers.ContainsKey(message.User.ID))
                        {
                            this.enteredUsers[message.User.ID] = new GiveawayUser() { User = message.User, Entries = 0 };
                        }
                        GiveawayUser giveawayUser = this.enteredUsers[message.User.ID];

                        if (giveawayUser != null)
                        {
                            giveawayUser.Entries += entries;

                            await ChannelSession.Settings.GiveawayUserJoinedCommand.Perform(message.User, extraSpecialIdentifiers: this.GetSpecialIdentifiers());

                            GlobalEvents.GiveawaysChangedOccurred();
                        }
                    }
                    else
                    {
                        await ChannelSession.Chat.Whisper(message.User.UserName, string.Format("You are not able to enter this giveaway as it is only for {0}s", ChannelSession.Settings.GiveawayRequirements.Role.RoleNameString));
                    }
                }
                else if (this.Winner != null && this.Winner.Equals(message.User) && message.Message.Equals("!claim", StringComparison.InvariantCultureIgnoreCase))
                {
                    await ChannelSession.Settings.GiveawayWinnerSelectedCommand.Perform(this.Winner, extraSpecialIdentifiers: this.GetSpecialIdentifiers());
                    await this.End();
                }
            }
            catch (Exception ex)
            {
                MixItUp.Base.Util.Logger.Log(ex);
            }
        }

        private async void GlobalEvents_OnDonationOccurred(object sender, UserDonationModel donation)
        {
            if (this.EntryType == GiveawayEntryTypeEnum.DonationTips && this.TimeLeft > 0 && this.Winner == null)
            {
                UserViewModel user = donation.User;
                if (user != null)
                {
                    if (!this.enteredUsers.ContainsKey(user.ID))
                    {
                        this.enteredUsers[user.ID] = new GiveawayUser() { User = user, Entries = 0 };
                    }
                    GiveawayUser giveawayUser = this.enteredUsers[user.ID];

                    giveawayUser.DonationAmount += donation.Amount;

                    int newEntries = 0;
                    if (ChannelSession.Settings.GiveawayDonationAmount > 0.0)
                    {
                        if (ChannelSession.Settings.GiveawayDonationRequiredAmount && giveawayUser.DonationAmount >= ChannelSession.Settings.GiveawayDonationAmount)
                        {
                            newEntries = 1;
                        }
                        else
                        {
                            newEntries = (int)(giveawayUser.DonationAmount / ChannelSession.Settings.GiveawayDonationAmount);
                        }
                    }
                    else
                    {
                        newEntries = 1;
                    }

                    newEntries = Math.Min(giveawayUser.Entries + newEntries, ChannelSession.Settings.GiveawayMaximumEntries);
                    giveawayUser.Entries += newEntries;

                    if (newEntries > 0)
                    {
                        await ChannelSession.Chat.Whisper(user.UserName, string.Format("You've gotten {0} entr(ies) into the giveaway, stay tuned to see who wins!", newEntries));
                        GlobalEvents.GiveawaysChangedOccurred();
                    }
                }
            }
        }

        private Dictionary<string, string> GetSpecialIdentifiers()
        {
            return new Dictionary<string, string>()
            {
                { "giveawayitem", this.Item },
                { "giveawaycommand", ChannelSession.Settings.GiveawayCommand },
                { "giveawaytimelimit", (this.TimeLeft / 60).ToString() }
            };
        }
    }
}
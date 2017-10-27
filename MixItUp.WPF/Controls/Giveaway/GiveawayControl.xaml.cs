﻿using Mixer.Base.Util;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.User;
using MixItUp.WPF.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Controls.Giveaway
{
    /// <summary>
    /// Interaction logic for GiveawayControl.xaml
    /// </summary>
    public partial class GiveawayControl : MainControlBase
    {
        public bool giveawayEnabled;
        public string giveawayItem;

        private ObservableCollection<UserViewModel> enteredUsers = new ObservableCollection<UserViewModel>();
        private UserViewModel selectedWinner = null;

        private int timeLeft = 120;
        private CancellationTokenSource backgroundThreadCancellationTokenSource = new CancellationTokenSource();

        public GiveawayControl()
        {
            InitializeComponent();
        }

        protected override Task InitializeInternal()
        {
            this.GiveawayTypeComboBox.ItemsSource = EnumHelper.GetEnumNames(new UserRole[] { UserRole.User, UserRole.Follower, UserRole.Subscriber });
            this.EnteredUsersListView.ItemsSource = this.enteredUsers;

            this.GiveawayTypeComboBox.SelectedItem = EnumHelper.GetEnumName(ChannelSession.Settings.GiveawayUserRole);
            this.GiveawayCommandTextBox.Text = ChannelSession.Settings.GiveawayCommand;
            this.GiveawayTimerTextBox.Text = ChannelSession.Settings.GiveawayTimer.ToString();

            GlobalEvents.OnChatCommandMessageReceived += GlobalEvents_OnChatCommandMessageReceived;

            return base.InitializeInternal();
        }

        private async void EnableGiveawayButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(this.GiveawayItemTextBox.Text))
            {
                await MessageBoxHelper.ShowMessageDialog("An item to give away must be specified");
                return;
            }

            if (this.GiveawayTypeComboBox.SelectedIndex < 0)
            {
                await MessageBoxHelper.ShowMessageDialog("The allowed winners must be specified");
                return;
            }

            this.timeLeft = 0;
            if (string.IsNullOrEmpty(this.GiveawayTimerTextBox.Text) || !int.TryParse(this.GiveawayTimerTextBox.Text, out this.timeLeft) || this.timeLeft <= 0)
            {
                await MessageBoxHelper.ShowMessageDialog("Timer must be greater than 0");
                return;
            }

            if (string.IsNullOrEmpty(this.GiveawayCommandTextBox.Text))
            {
                await MessageBoxHelper.ShowMessageDialog("Giveaway command must be specified");
                return;
            }

            if (this.GiveawayCommandTextBox.Text.Any(c => !Char.IsLetterOrDigit(c)))
            {
                await MessageBoxHelper.ShowMessageDialog("Giveaway Command can only contain letters and numbers");
                return;
            }

            this.giveawayEnabled = true;
            this.giveawayItem = this.GiveawayItemTextBox.Text;
            ChannelSession.Settings.GiveawayUserRole = EnumHelper.GetEnumValueFromString<UserRole>((string)this.GiveawayTypeComboBox.SelectedItem);
            ChannelSession.Settings.GiveawayCommand = this.GiveawayCommandTextBox.Text.ToLower();
            ChannelSession.Settings.GiveawayTimer = this.timeLeft;
            this.TimeLeftTextBlock.Text = this.timeLeft.ToString();

            await ChannelSession.Settings.Save();

            this.GiveawayWinnerTextBlock.Text = "";
            this.EnableGiveawayButton.Visibility = Visibility.Collapsed;
            this.DisableGiveawayButton.Visibility = Visibility.Visible;

            await ChannelSession.BotChat.SendMessage(string.Format("A giveaway for {0} for {1}s has started! Type \"!{2}\" in chat in the next {3} seconds to enter!", this.giveawayItem, ChannelSession.Settings.GiveawayUserRole, ChannelSession.Settings.GiveawayCommand, ChannelSession.Settings.GiveawayTimer));

            this.backgroundThreadCancellationTokenSource = new CancellationTokenSource();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () => { await this.GiveawayTimerBackground(); }, this.backgroundThreadCancellationTokenSource.Token);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private async void DisableGiveawayButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            await this.EndGiveaway();
        }

        private async Task EndGiveaway()
        {
            this.backgroundThreadCancellationTokenSource.Cancel();

            this.giveawayEnabled = false;
            await this.Dispatcher.BeginInvoke(new Action(() =>
            {
                this.selectedWinner = null;
                this.TimeLeftTextBlock.Text = "";
                this.DisableGiveawayButton.Visibility = Visibility.Collapsed;
                this.EnableGiveawayButton.Visibility = Visibility.Visible;

                this.enteredUsers.Clear();
            }));
        }

        private async Task GiveawayTimerBackground()
        {
            while (this.timeLeft > 0)
            {
                await Task.Delay(1000);
                this.timeLeft--;
                await this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    this.TimeLeftTextBlock.Text = this.timeLeft.ToString();
                }));

                if (this.backgroundThreadCancellationTokenSource.Token.IsCancellationRequested)
                {
                    await this.EndGiveaway();
                    return;
                }
            }

            while (true)
            {
                if (this.enteredUsers.Count() > 0)
                {
                    Random random = new Random();
                    int index = random.Next(this.enteredUsers.Count());
                    this.selectedWinner = this.enteredUsers.ElementAt(index);
                    await this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        this.enteredUsers.Remove(this.selectedWinner);
                        this.GiveawayWinnerTextBlock.Text = this.selectedWinner.UserName;
                    }));

                    await ChannelSession.BotChat.SendMessage(string.Format("Congratulations @{0}, you won {1}! Type \"!claim\" in chat in the next 60 seconds to claim your prize!", this.selectedWinner.UserName, this.giveawayItem));

                    this.timeLeft = 60;
                    while (this.timeLeft > 0)
                    {
                        await Task.Delay(1000);
                        this.timeLeft--;
                        await this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            this.TimeLeftTextBlock.Text = this.timeLeft.ToString();
                        }));

                        if (this.backgroundThreadCancellationTokenSource.Token.IsCancellationRequested)
                        {
                            await this.EndGiveaway();
                            return;
                        }
                    }
                }
                else
                {
                    await ChannelSession.BotChat.SendMessage("There are no users that entered/left in the giveaway");
                    await this.EndGiveaway();
                    return;
                }
            }
        }

        private async void GlobalEvents_OnChatCommandMessageReceived(object sender, ChatMessageCommandViewModel e)
        {
            if (this.giveawayEnabled)
            {
                if (this.selectedWinner == null && e.CommandName.Equals(ChannelSession.Settings.GiveawayCommand))
                {
                    bool isUserValid = true;
                    if (ChannelSession.Settings.GiveawayUserRole == UserRole.Follower)
                    {
                        isUserValid = e.User.IsFollower;
                    }
                    else if (ChannelSession.Settings.GiveawayUserRole == UserRole.Subscriber)
                    {
                        isUserValid = e.User.Roles.Contains(UserRole.Subscriber);
                    }

                    if (isUserValid)
                    {
                        await ChannelSession.BotChat.Whisper(e.User.UserName, "You have been entered into the giveaway, stay tuned to see who wins!");
                        await this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (!this.enteredUsers.Contains(e.User))
                            {
                                this.enteredUsers.Add(e.User);
                            }
                        }));
                    }
                    else
                    {
                        await ChannelSession.BotChat.Whisper(e.User.UserName, string.Format("You are not able to enter this giveaway as it is only for {0}", ChannelSession.Settings.GiveawayUserRole));
                    }
                }
                else if (this.selectedWinner != null && e.CommandName.Equals("claim") && this.selectedWinner.Equals(e.User))
                {
                    await ChannelSession.BotChat.SendMessage(string.Format("@{0} has claimed their prize! Listen closely to the streamer for instructions on getting your prize.", e.User.UserName));
                    await this.EndGiveaway();
                }
            }
        }
    }
}

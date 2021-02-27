﻿using MixItUp.Base.Model;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Util;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Controls.MainControls
{
    /// <summary>
    /// Interaction logic for AboutControl.xaml
    /// </summary>
    public partial class AboutControl : MainControlBase
    {
        public AboutControl()
        {
            InitializeComponent();
        }

        protected override Task InitializeInternal()
        {
            this.VersionTextBlock.Text = Assembly.GetEntryAssembly().GetName().Version.ToString();

            return base.InitializeInternal();
        }

        private void IssueReportHyperlink_Click(object sender, RoutedEventArgs e)
        {
            ProcessHelper.LaunchProgram("MixItUp.Reporter.exe", string.Format("{0} {1} {2} {3}", FileLoggerHandler.CurrentLogFilePath, (int)StreamingPlatformTypeEnum.Twitch, ServiceManager.Get<TwitchSessionService>().UserNewAPI?.id, ServiceManager.Get<TwitchSessionService>().UserNewAPI?.login));
        }

        private void TwitterButton_Click(object sender, RoutedEventArgs e) { ProcessHelper.LaunchLink("https://twitter.com/MixItUpApp"); }

        private void DiscordButton_Click(object sender, RoutedEventArgs e) { ProcessHelper.LaunchLink("https://mixitupapp.com/discord"); }

        private void YouTubeButton_Click(object sender, RoutedEventArgs e) { ProcessHelper.LaunchLink("https://www.youtube.com/channel/UCcY0vKI9yqcMTgh8OzSnRSA"); }

        private void TwitchButton_Click(object sender, RoutedEventArgs e) { ProcessHelper.LaunchLink("https://twitch.tv/MixItUpApp"); }

        private void WikiButton_Click(object sender, RoutedEventArgs e) { ProcessHelper.LaunchLink("https://github.com/SaviorXTanren/mixer-mixitup/wiki"); }

        private void GithubButton_Click(object sender, RoutedEventArgs e) { ProcessHelper.LaunchLink("https://github.com/SaviorXTanren/mixer-mixitup"); }

        private void SaviorXTanrenButton_Click(object sender, RoutedEventArgs e) { ProcessHelper.LaunchLink("https://twitch.tv/SaviorXTanren"); }

        private void VerbatimTButton_Click(object sender, RoutedEventArgs e) { ProcessHelper.LaunchLink("https://twitch.tv/VerbatimT"); }

        private void TyrenDesButton_Click(object sender, RoutedEventArgs e) { ProcessHelper.LaunchLink("https://twitch.tv/TyrenDes"); }
    }
}

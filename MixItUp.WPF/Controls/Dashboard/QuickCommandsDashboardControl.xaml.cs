﻿using MixItUp.Base.Commands;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Controls.Dashboard;
using MixItUp.WPF.Controls.Dialogs;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Dashboard
{
    /// <summary>
    /// Interaction logic for QuickCommandsDashboardControl.xaml
    /// </summary>
    public partial class QuickCommandsDashboardControl : DashboardControlBase
    {
        private QuickCommandsDashboardControlViewModel viewModel;

        public QuickCommandsDashboardControl()
        {
            InitializeComponent();
        }

        protected override async Task InitializeInternal()
        {
            this.DataContext = this.viewModel = new QuickCommandsDashboardControlViewModel(this.Window.ViewModel);
            await this.viewModel.OnLoaded();

            await base.InitializeInternal();
        }

        private async void EditCommandOneButton_Click(object sender, System.Windows.RoutedEventArgs e) { this.viewModel.CommandOne = await this.ShowCommandSelector(); }

        private async void EditCommandTwoButton_Click(object sender, System.Windows.RoutedEventArgs e) { this.viewModel.CommandTwo = await this.ShowCommandSelector(); }

        private async void EditCommandThreeButton_Click(object sender, System.Windows.RoutedEventArgs e) { this.viewModel.CommandThree = await this.ShowCommandSelector(); }

        private async void EditCommandFourButton_Click(object sender, System.Windows.RoutedEventArgs e) { this.viewModel.CommandFour = await this.ShowCommandSelector(); }

        private async void EditCommandFiveButton_Click(object sender, System.Windows.RoutedEventArgs e) { this.viewModel.CommandFive = await this.ShowCommandSelector(); }

        private async Task<CommandBase> ShowCommandSelector()
        {
            CommandSelectorDialogControl dialogControl = new CommandSelectorDialogControl();
            await DialogHelper.ShowCustom(dialogControl);
            return dialogControl.Command;
        }
    }
}

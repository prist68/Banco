using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Banco.Vendita.Points;

namespace Banco.UI.Wpf.Views;

public partial class RewardSelectionDialogWindow : Window
{
    public RewardSelectionDialogWindow(
        string eyebrow,
        string dialogTitle,
        string dialogMessage,
        IReadOnlyList<PointsRewardRule> rewardRules)
    {
        InitializeComponent();
        Eyebrow = eyebrow;
        DialogTitle = dialogTitle;
        DialogMessage = dialogMessage;
        RewardRules = rewardRules
            .Select(rule => new RewardSelectionItem(rule))
            .ToList();
        DataContext = this;
        RewardsListBox.ItemsSource = RewardRules;
        RewardsListBox.SelectedIndex = RewardRules.Count > 0 ? 0 : -1;
    }

    public string Eyebrow { get; }

    public string DialogTitle { get; }

    public string DialogMessage { get; }

    public IReadOnlyList<RewardSelectionItem> RewardRules { get; }

    public PointsRewardRule? SelectedRewardRule => (RewardsListBox.SelectedItem as RewardSelectionItem)?.Rule;

    private void PrimaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedRewardRule is null)
        {
            return;
        }

        DialogResult = true;
    }

    private void SecondaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    public sealed class RewardSelectionItem
    {
        public RewardSelectionItem(PointsRewardRule rule)
        {
            Rule = rule;
        }

        public PointsRewardRule Rule { get; }

        public string RuleName => Rule.RuleName;

        public string RewardDescription => Rule.RewardDescription;

        public string RequiredPointsLabel => $"Soglia: {Rule.RequiredPoints.GetValueOrDefault():N0} punti";
    }
}

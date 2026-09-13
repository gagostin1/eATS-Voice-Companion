using System.Windows;

namespace EatsVoiceCompanion.App;

public partial class ContributionNoticeWindow : Window
{
    public ContributionNoticeWindow(bool participationEnabled)
    {
        InitializeComponent();
        ParticipationCheckBox.IsChecked = participationEnabled;
        ParticipationEnabled = participationEnabled;
    }

    public bool ParticipationEnabled { get; private set; }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        ParticipationEnabled = ParticipationCheckBox.IsChecked == true;
        DialogResult = true;
    }

    private void DoNotParticipate_Click(object sender, RoutedEventArgs e)
    {
        ParticipationEnabled = false;
        DialogResult = false;
    }
}

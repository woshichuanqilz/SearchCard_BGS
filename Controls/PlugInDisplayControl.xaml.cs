using Hearthstone_Deck_Tracker.API;
using System.Windows.Controls;
using Card = Hearthstone_Deck_Tracker.Hearthstone.Card;

namespace CardSearcher.Controls
{
    /// <summary>
    /// Interaction logic for PlugInDisplayControl.xaml
    /// </summary>
    public partial class PlugInDisplayControl : StackPanel
    {
        public PlugInDisplayControl()
        {
            InitializeComponent();
            FakeLogic();
        }

        public void FakeLogic()
        {
            GameEvents.OnPlayerHandMouseOver.Add(PlayerHandMouseOver);
            GameEvents.OnMouseOverOff.Add(OnMouseOff);
        }

        public void OnMouseOff()
        {
            this.Visibility = System.Windows.Visibility.Collapsed;
        }

        public void PlayerHandMouseOver(Card card)
        {

            this.Visibility = System.Windows.Visibility.Visible;
            this.LblTextArea1.Content = card.Name;
            this.LblTextArea2.Content = card.Artist;
        }
    }
}
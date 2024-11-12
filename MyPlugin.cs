using CardSearcher.Controls;
using CardSearcher.Logic;
using CardSearcher.Properties;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Logging;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hearthstone_Deck_Tracker.Utility;
using Card = Hearthstone_Deck_Tracker.Hearthstone.Card;
using Core = Hearthstone_Deck_Tracker.API.Core;
using System.Windows.Media.Imaging;
using System.IO;
using System.Net;
using System.Security.Policy;
using HearthDb;
using HearthDb.Enums;
using System.Collections.Generic;
using System.Linq;

namespace CardSearcher
{
    /// <summary>
    /// This is where we put the logic for our Plug-in
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class CardSearcher : IDisposable
    {
        // ToDo: The window shouldn't be statically named
        private static string panelName = "pluginStackPanelView";

        /// <summary>
        /// The class that allows us to let the user move the panel
        /// </summary>
        public static InputMoveManager inputMoveManager;

        /// <summary>
        /// The panel reference we will display our plug-in magic within
        /// </summary>
        public PlugInDisplayControl stackPanel;

        /// <summary>
        /// Initializes a new instance of the <see cref="CardSearcher"/> class.
        /// </summary>
        public CardSearcher()
        {
            // We are adding the Panel here for simplicity.  It would be better to add it under InitLogic()
            InitViewPanel();

            GameEvents.OnGameStart.Add(GameTypeCheck);
            GameEvents.OnGameEnd.Add(CleanUp);
        }

        /// <summary>
        /// Check the game type to see if our Plug-in is used.
        /// </summary>
        private void GameTypeCheck()
        {
            // ToDo : Enable toggle Props
            if (Core.Game.CurrentGameType == HearthDb.Enums.GameType.GT_RANKED ||
                Core.Game.CurrentGameType == HearthDb.Enums.GameType.GT_CASUAL ||
                Core.Game.CurrentGameType == HearthDb.Enums.GameType.GT_FSG_BRAWL ||
                Core.Game.CurrentGameType == HearthDb.Enums.GameType.GT_ARENA)
            {
                InitLogic();
            }
        }

        private void InitLogic()
        {
            // Here you can begin to work your Plug-in magic
        }

        private void InitViewPanel()
        {
            stackPanel = new PlugInDisplayControl();
            stackPanel.Name = panelName;
            stackPanel.Visibility = System.Windows.Visibility.Collapsed;
            Core.OverlayCanvas.Children.Add(stackPanel);

            Canvas.SetTop(stackPanel, Settings.Default.Top);
            Canvas.SetLeft(stackPanel, Settings.Default.Left);

            inputMoveManager = new InputMoveManager(stackPanel);

            Settings.Default.PropertyChanged += SettingsChanged;
            SettingsChanged(null, null);
        }

        private void SettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            stackPanel.RenderTransform = new ScaleTransform(Settings.Default.Scale / 100, Settings.Default.Scale / 100);
            stackPanel.Opacity = Settings.Default.Opacity / 100;
        }

        public void CleanUp()
        {
            if (stackPanel != null)
            {
                Core.OverlayCanvas.Children.Remove(stackPanel);
                Dispose();
            }
        }

        public void Dispose()
        {
            inputMoveManager.Dispose();
        }

        [STAThread]
        static void Main()
        {
            Application app = new Application();

            SearchWindow sw = new SearchWindow();
            //sw.Show();
            //app.Run();

            // get Tag
            var overrides = new Dictionary<int, Tuple<GameTag, int>>();
            Func<HearthDb.Card, GameTag, int> getTag = (HearthDb.Card card, GameTag tag) =>
            {
                if (overrides.TryGetValue(card.DbfId, out var tagOverride) && tagOverride.Item1 == tag)
                    return tagOverride.Item2;
                return card.Entity.GetTag(tag);
            };

            var baconCards = Cards.All.Values
                .Where(x =>
                    getTag(x, GameTag.TECH_LEVEL) > 0
                    && getTag(x, GameTag.IS_BACON_POOL_MINION) > 0
                );
            
            // Get value from baconCards where cardId is BGS_081
            var baconCard = baconCards.FirstOrDefault(x => x.Id == "BGS_081");

            //var card = Database.GetCardFromId("BG26_147");
            var hdt_card = Database.GetCardFromId("BGS_081");

            const string cardId = "BGS_081";
            //var url = $"https://static.zerotoheroes.com/hearthstone/cardart/256x/{cardId}.jpg";
            //var client = new WebClient();
            //client.DownloadFile(url, "ori_image.jpg");

            //// Get LocName
            HearthDb.Card dbCard;
            Cards.All.TryGetValue(cardId, out dbCard);
            var name = dbCard.GetLocName(Locale.zhCN);

            var filteredList = Cards.All.Where(kvp => kvp.Key.Contains(cardId)).ToList();
            Log.Info("done");

            // get all card which method GetLocText result not null and contains "Gold"
            var goldCards = Cards.All.Where(kvp => kvp.Value.GetLocText(Locale.enUS) != null && kvp.Value.GetLocText(Locale.enUS).Contains("Gold")).ToList();
            Log.Info($"goldCards: {goldCards.Count}");
        }

        public BitmapImage GetCardImage(string cardId)
        {
            // 如果image文件夹不存在，则创建
            if (!Directory.Exists("image"))
            {
                Directory.CreateDirectory("image");
            }

            string imagePath = Path.Combine("image", $"{cardId}.jpg");

            // 检查文件是否存在
            if (File.Exists(imagePath))
            {
                return new BitmapImage(new Uri(imagePath, UriKind.Relative));
            }
            else
            {
                // 下载图片并保存
                var url = $"https://static.zerotoheroes.com/hearthstone/cardart/256x/{cardId}.jpg";
                using (var client = new WebClient())
                {
                    client.DownloadFile(url, imagePath);
                }
                return new BitmapImage(new Uri(imagePath, UriKind.Relative));
            }
        }
    }
}